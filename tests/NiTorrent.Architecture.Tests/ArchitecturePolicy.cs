using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NiTorrent.Architecture.Tests;

internal static class ArchitecturePolicy
{
    internal static string[] ReferencedAssemblies(Stream stream)
    {
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var metadata = pe.GetMetadataReader();
        return metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)).ToArray();
    }
    internal sealed record Module(string Path, string Assembly, string[] ProjectDependencies, string[] AssemblyDependencies);
    internal static readonly Module[] Modules =
    [
        new("Modules/Torrents/Contracts/NiTorrent.Torrents.Contracts.csproj", "NiTorrent.Torrents.Contracts", [], []),
        new("Modules/Torrents/Domain/NiTorrent.Domain.csproj", "NiTorrent.Domain", ["NiTorrent.Torrents.Contracts"], ["NiTorrent.Torrents.Contracts"]),
        new("Modules/Torrents/Application/NiTorrent.Application.csproj", "NiTorrent.Application", ["NiTorrent.Domain", "NiTorrent.Torrents.Contracts"], ["NiTorrent.Domain", "NiTorrent.Torrents.Contracts"]),
        new("Modules/Torrents/Engines/MonoTorrent/NiTorrent.Torrents.MonoTorrent.csproj", "NiTorrent.Torrents.MonoTorrent", ["NiTorrent.Application"], ["NiTorrent.Application", "NiTorrent.Domain", "NiTorrent.Torrents.Contracts"]),
        new("Modules/Torrents/Infrastructure/NiTorrent.Infrastructure.csproj", "NiTorrent.Infrastructure", ["NiTorrent.Application"], ["NiTorrent.Application", "NiTorrent.Domain", "NiTorrent.Torrents.Contracts"]),
        new("Modules/Desktop/Contracts/NiTorrent.Desktop.Contracts.csproj", "NiTorrent.Desktop.Contracts", [], []),
        new("Modules/Desktop/Presentation/NiTorrent.Presentation.csproj", "NiTorrent.Presentation", ["NiTorrent.Torrents.Contracts", "NiTorrent.Desktop.Contracts", "NiTorrent.Updates.Contracts"], ["NiTorrent.Torrents.Contracts", "NiTorrent.Desktop.Contracts", "NiTorrent.Updates.Contracts"]),
        new("Modules/Desktop/WinUI/NiTorrent.Desktop.WinUI.csproj", "NiTorrent.Desktop.WinUI", ["NiTorrent.Presentation"], ["NiTorrent.Presentation", "NiTorrent.Torrents.Contracts", "NiTorrent.Desktop.Contracts", "NiTorrent.Updates.Contracts"]),
        new("Modules/Updates/Contracts/NiTorrent.Updates.Contracts.csproj", "NiTorrent.Updates.Contracts", [], []),
        new("Modules/Updates/Windows/NiTorrent.Updates.Windows.csproj", "NiTorrent.Updates.Windows", ["NiTorrent.Updates.Contracts"], ["NiTorrent.Updates.Contracts"]),
        new("NiTorrent.App/NiTorrent.App.csproj", "NiTorrent.App", ["NiTorrent.Infrastructure", "NiTorrent.Torrents.MonoTorrent", "NiTorrent.Desktop.WinUI", "NiTorrent.Updates.Windows"], ["NiTorrent.Infrastructure", "NiTorrent.Torrents.MonoTorrent", "NiTorrent.Desktop.WinUI", "NiTorrent.Updates.Windows", "NiTorrent.Presentation", "NiTorrent.Torrents.Contracts", "NiTorrent.Desktop.Contracts", "NiTorrent.Updates.Contracts"])
    ];

    internal static bool Allows(string assembly, string dependency) =>
        !dependency.StartsWith("NiTorrent", StringComparison.Ordinal) ||
        Modules.Single(m => m.Assembly == assembly).AssemblyDependencies.Contains(dependency, StringComparer.Ordinal);

    internal static string[] FindCycle(IReadOnlyDictionary<string, string[]> graph)
    {
        var active = new HashSet<string>();
        var complete = new HashSet<string>();
        var path = new List<string>();
        string[] Visit(string node)
        {
            if (active.Contains(node)) return path.SkipWhile(p => p != node).Append(node).ToArray();
            if (!complete.Add(node)) return [];
            active.Add(node); path.Add(node);
            foreach (var next in graph[node])
            {
                var cycle = Visit(next);
                if (cycle.Length != 0) return cycle;
            }
            path.RemoveAt(path.Count - 1); active.Remove(node);
            return [];
        }
        foreach (var node in graph.Keys)
        {
            var cycle = Visit(node);
            if (cycle.Length != 0) return cycle;
        }
        return [];
    }

    internal static string[] ReferencedTypes(string file)
    {
        using var stream = File.OpenRead(file);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return metadata.TypeReferences.Select(handle => metadata.GetTypeReference(handle))
            .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name)).ToArray();
    }
}
