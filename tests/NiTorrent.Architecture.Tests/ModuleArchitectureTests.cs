using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;
using static NiTorrent.Architecture.Tests.ArchitecturePolicy;

namespace NiTorrent.Architecture.Tests;

public sealed class ModuleArchitectureTests
{
    private static readonly string Root = FindRoot();
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif
    private static readonly string Output = Path.Combine(Root, "src/NiTorrent.App/bin/x64", Configuration, "net11.0-windows10.0.26100.0");
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NiTorrent.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void EveryProductionProjectIsRegisteredAndHasAnExplicitPolicy()
    {
        var actual = Directory.EnumerateFiles(Path.Combine(Root, "src/Modules"), "*.csproj", SearchOption.AllDirectories)
            .Append(Path.Combine(Root, "src/NiTorrent.App/NiTorrent.App.csproj"))
            .Select(Path.GetFullPath).Order().ToArray();
        Assert.Equal(actual, Modules.Select(m => Path.GetFullPath(Path.Combine(Root, "src", m.Path))).Order());
        var registered = XDocument.Load(Path.Combine(Root, "NiTorrent.slnx")).Descendants("Project")
            .Select(p => Path.GetFullPath(Path.Combine(Root, p.Attribute("Path")!.Value))).ToHashSet();
        Assert.All(actual, path => Assert.Contains(path, registered));
    }

    [Fact]
    public void ProjectReferencesFollowApprovedGraphWithoutCycles()
    {
        var graph = new Dictionary<string, string[]>();
        foreach (var module in Modules)
        {
            var project = Path.Combine(Root, "src", module.Path);
            var references = XDocument.Load(project).Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/'))).Order().ToArray();
            Assert.Equal(module.ProjectDependencies.Order(), references);
            graph.Add(module.Assembly, references);
        }
        Assert.Empty(FindCycle(graph));
    }

    [Fact]
    public void CompiledModulesCannotBypassContractsThroughTransitiveReferences()
    {
        foreach (var module in Modules)
        {
            var path = Path.Combine(Output, module.Assembly + ".dll");
            Assert.True(File.Exists(path), $"Missing architecture input: {path}");
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            Assert.True(metadata.TypeDefinitions.Count > 1, $"No types analyzed in {module.Assembly}");
            using var references = File.OpenRead(path);
            foreach (var name in ReferencedAssemblies(references))
            {
                Assert.True(Allows(module.Assembly, name), $"Forbidden dependency: {module.Assembly} -> {name}");
                if (name.StartsWith("MonoTorrent", StringComparison.Ordinal)) Assert.Equal("NiTorrent.Torrents.MonoTorrent", module.Assembly);
                if (name.StartsWith("Microsoft.UI", StringComparison.Ordinal) || name.StartsWith("WinUI", StringComparison.Ordinal))
                    Assert.Contains(module.Assembly, new[] { "NiTorrent.App", "NiTorrent.Desktop.WinUI", "NiTorrent.Updates.Windows" });
            }
        }
    }

    [Fact]
    public void MonoTorrentPackageAndTypesStayInsideItsAdapter()
    {
        foreach (var module in Modules)
        {
            var packages = XDocument.Load(Path.Combine(Root, "src", module.Path)).Descendants("PackageReference")
                .Select(p => p.Attribute("Include")!.Value);
            if (module.Assembly == "NiTorrent.Torrents.MonoTorrent") Assert.Contains("MonoTorrent", packages);
            else
            {
                Assert.DoesNotContain(packages, p => p.StartsWith("MonoTorrent", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(ReferencedTypes(Path.Combine(Output, module.Assembly + ".dll")), name => name.StartsWith("MonoTorrent.", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void AppReferencesInfrastructureOnlyForModuleRegistration()
    {
        var types = ReferencedTypes(Path.Combine(Output, "NiTorrent.App.dll"));
        Assert.Contains("NiTorrent.Infrastructure.DI.DependencyInjection", types);
        Assert.DoesNotContain(types, name => name.StartsWith("NiTorrent.Infrastructure.", StringComparison.Ordinal)
            && name != "NiTorrent.Infrastructure.DI.DependencyInjection");
        Assert.DoesNotContain(types, name => name == "NiTorrent.Application.Torrents.TorrentSession" || name.EndsWith("UseCase", StringComparison.Ordinal));
        Assert.Contains("NiTorrent.Torrents.Engines.MonoTorrent.MonoTorrentRegistration", types);
        Assert.DoesNotContain(types, name => name.StartsWith("NiTorrent.Torrents.Engines.MonoTorrent.", StringComparison.Ordinal)
            && name != "NiTorrent.Torrents.Engines.MonoTorrent.MonoTorrentRegistration");
    }

    [Fact]
    public void EngineImplementationHasOnlyOnePublicRegistrationEntry()
    {
        using var stream = File.OpenRead(Path.Combine(Output, "NiTorrent.Torrents.MonoTorrent.dll"));
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var exported = metadata.TypeDefinitions.Select(h => metadata.GetTypeDefinition(h))
            .Where(t => (t.Attributes & System.Reflection.TypeAttributes.VisibilityMask) == System.Reflection.TypeAttributes.Public)
            .Select(t => metadata.GetString(t.Name));
        Assert.Equal(new[] { "MonoTorrentRegistration" }, exported);
    }

    [Theory]
    [InlineData("NiTorrent.Presentation", "NiTorrent.Infrastructure")]
    [InlineData("NiTorrent.Desktop.WinUI", "NiTorrent.Application")]
    [InlineData("NiTorrent.Torrents.Contracts", "NiTorrent.Domain")]
    [InlineData("NiTorrent.Updates.Windows", "NiTorrent.Presentation")]
    [InlineData("NiTorrent.Application", "NiTorrent.App")]
    public void PolicyRejectsControlledForbiddenDependencies(string from, string to)
    {
        // Compile an isolated IL fixture with a forbidden field type. Analyze its
        // actual assembly references with the same reader as the production check.
        using var fixture = CompileDependency(from, to);
        var dependencies = ReferencedAssemblies(fixture);
        Assert.Contains(to, dependencies);
        Assert.Contains(dependencies, dependency => !Allows(from, dependency));
    }

    [Fact]
    public void PolicyAcceptsCompiledContractDependency()
    {
        using var fixture = CompileDependency("NiTorrent.Presentation", "NiTorrent.Torrents.Contracts");
        var dependencies = ReferencedAssemblies(fixture);
        Assert.Contains("NiTorrent.Torrents.Contracts", dependencies);
        Assert.All(dependencies, dependency => Assert.True(Allows("NiTorrent.Presentation", dependency)));
    }

    private static MemoryStream CompileDependency(string from, string to)
    {
        var dependency = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(to), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("dependency").DefineType("Control.ExternalType", TypeAttributes.Public).CreateType()!;
        var assembly = new PersistedAssemblyBuilder(new AssemblyName(from), typeof(object).Assembly);
        var type = assembly.DefineDynamicModule("control").DefineType("Control.Consumer", TypeAttributes.Public);
        type.DefineField("Dependency", dependency, FieldAttributes.Public);
        type.CreateType();
        var output = new MemoryStream();
        assembly.Save(output);
        output.Position = 0;
        return output;
    }

    [Fact]
    public void CycleDetectorRejectsControlledCycleAndAcceptsDiamond()
    {
        var graph = new Dictionary<string, string[]> { ["A"] = ["B", "C"], ["B"] = ["D"], ["C"] = ["D"], ["D"] = [] };
        Assert.Empty(FindCycle(graph));
        graph["D"] = ["A"];
        Assert.Equal(new[] { "A", "B", "D", "A" }, FindCycle(graph));
    }
}
