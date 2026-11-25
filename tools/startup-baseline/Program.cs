using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Domain.Torrents;
using NiTorrent.Infrastructure.Torrents;

var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
for (var i = 0; i < args.Length; i += 2)
{
    if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException("Use --output PATH [--catalog PATH] [--samples 7] [--warmups 2].");
    if (!arguments.TryAdd(args[i], args[i + 1]))
        throw new ArgumentException($"Duplicate argument: {args[i]}");
}
var supported = new[] { "--output", "--catalog", "--samples", "--warmups" };
if (arguments.Keys.Any(key => !supported.Contains(key)))
    throw new ArgumentException("Unknown argument.");
if (!arguments.TryGetValue("--output", out var outputArgument))
    throw new ArgumentException("--output is required; use a new directory.");
var output = Path.GetFullPath(outputArgument);
if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
    throw new IOException("The output directory must be empty. Existing results are never overwritten.");
Directory.CreateDirectory(output);
var samples = arguments.TryGetValue("--samples", out var samplesText) ? int.Parse(samplesText) : 7;
var warmups = arguments.TryGetValue("--warmups", out var warmupsText) ? int.Parse(warmupsText) : 2;
if (samples is < 3 or > 100 || warmups is < 1 or > 20)
    throw new ArgumentOutOfRangeException(nameof(samples), "Samples: 3..100; warmups: 1..20.");

// Snapshot once; never construct a repository over the user's live directory.
byte[]? snapshot = null;
string? originalHash = null;
TorrentRepositoryDocument? sourceDocument = null;
if (arguments.TryGetValue("--catalog", out var catalogArgument))
{
    snapshot = await File.ReadAllBytesAsync(Path.GetFullPath(catalogArgument));
    originalHash = Convert.ToHexString(SHA256.HashData(snapshot));
    sourceDocument = JsonSerializer.Deserialize(snapshot,
        JsonTorrentRepositoryDocumentContext.Default.TorrentRepositoryDocument)
        ?? throw new InvalidDataException("Catalog is null.");
    await File.WriteAllBytesAsync(Path.Combine(output, "input-snapshot.json"), snapshot);
}

var syntheticLight = CreateTemplate(4, 1024);
var syntheticHeavy = CreateTemplate(1000, 1024 * 1024);
var scenarios = new List<Scenario>
{
    new("empty", new TorrentRepositoryDocument(), "synthetic"),
    new("light-1", Repeat(syntheticLight, 1), "synthetic"),
    new("light-100", Repeat(syntheticLight, 100), "synthetic"),
    new("heavy-1", Repeat(syntheticHeavy, 1), "synthetic; 1000 files and 1 MiB source"),
};
if (sourceDocument is not null)
{
    scenarios.Add(new("profile", sourceDocument, "copied real catalog"));
    if (sourceDocument.Items.Count > 0)
    {
        var largest = sourceDocument.Items.MaxBy(item => item.Source?.Value.Length ?? 0)!;
        scenarios.Add(new("profile-template-10", Repeat(largest, 10), "synthetic copies of largest real entry"));
        scenarios.Add(new("profile-template-100", Repeat(largest, 100), "synthetic copies of largest real entry"));
    }
}

var results = new List<object>();
foreach (var scenario in scenarios)
{
    var fixtureRoot = Path.Combine(output, "fixtures", scenario.Name);
    var storage = new LocalProbeStorage(fixtureRoot);
    var catalog = storage.GetLocalPath(@"Torrents\torrent_downloads.json");
    storage.EnsureParentDirectory(catalog);
    if (scenario.Name == "profile" && snapshot is not null)
        await File.WriteAllBytesAsync(catalog, snapshot);
    else
    {
        await using var file = File.Create(catalog);
        await JsonSerializer.SerializeAsync(file, scenario.Document,
            JsonTorrentRepositoryDocumentContext.Default.TorrentRepositoryDocument);
    }

    var rows = new List<Measurement>();
    for (var iteration = -warmups; iteration < samples; iteration++)
    {
        var allocationStart = GC.GetTotalAllocatedBytes(precise: true);
        var gc0 = GC.CollectionCount(0);
        var gc1 = GC.CollectionCount(1);
        var gc2 = GC.CollectionCount(2);
        var clock = Stopwatch.StartNew();
        var repository = new JsonTorrentRepository(storage);
        var constructorMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        await repository.LoadingAsync(CancellationToken.None);
        var loadMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        var list = await repository.GetAllAsync(CancellationToken.None);
        var listMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        var restore = await repository.GetAllForRestoreAsync(CancellationToken.None);
        var prepareMs = clock.Elapsed.TotalMilliseconds;
        if (list.Count != scenario.Document.Items.Count || restore.Count != list.Count)
            throw new InvalidDataException("Probe result count does not match the generated fixture.");
        var readAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocationStart;
        var readGc0 = GC.CollectionCount(0) - gc0;
        var readGc1 = GC.CollectionCount(1) - gc1;
        var readGc2 = GC.CollectionCount(2) - gc2;

        // This probe isolates the workflow and its explicit delays. It neither
        // parses torrent bytes in MonoTorrent nor accesses downloaded files.
        var gateway = new NoOpRuntimeGateway();
        var workflow = new RestoreSessionUseCase(repository, gateway);
        clock.Restart();
        await workflow.ExecuteAsync(CancellationToken.None);
        var restoreNoEngineMs = clock.Elapsed.TotalMilliseconds;
        if (gateway.Added != list.Count)
            throw new InvalidDataException("Restore did not visit all fixture entries.");
        rows.Add(new Measurement(iteration, iteration < 0, constructorMs, loadMs,
            listMs, prepareMs, restoreNoEngineMs, readAllocatedBytes,
            readGc0, readGc1, readGc2, gateway.Added, gateway.Started));
        GC.KeepAlive(list);
        GC.KeepAlive(restore);
    }

    var measured = rows.Where(row => !row.Warmup).ToArray();
    var summary = new
    {
        scenario.Name,
        scenario.Origin,
        CatalogBytes = new FileInfo(catalog).Length,
        TorrentCount = scenario.Document.Items.Count,
        FileCount = scenario.Document.Items.Sum(item => (long)item.FileEntries.Count),
        SourceCharacters = scenario.Document.Items.Sum(item => (long)(item.Source?.Value.Length ?? 0)),
        DeclaredFileBytes = scenario.Document.Items.Sum(item => item.FileEntries.Sum(file => file.SizeByte)),
        Median = new
        {
            ConstructorMs = Median(measured.Select(row => row.ConstructorMs)),
            LoadMs = Median(measured.Select(row => row.LoadMs)),
            ListMs = Median(measured.Select(row => row.ListMs)),
            PrepareRestoreMs = Median(measured.Select(row => row.PrepareRestoreMs)),
            RestoreWithoutEngineMs = Median(measured.Select(row => row.RestoreWithoutEngineMs)),
            ReadAllocatedBytes = Median(measured.Select(row => (double)row.ReadAllocatedBytes))
        },
        MinLoadMs = measured.Min(row => row.LoadMs),
        MaxLoadMs = measured.Max(row => row.LoadMs),
        Measurements = rows
    };
    results.Add(summary);
    Console.WriteLine($"{scenario.Name}: count={summary.TorrentCount}, files={summary.FileCount}, " +
        $"JSON={summary.CatalogBytes} B, load={summary.Median.LoadMs:F3} ms, " +
        $"prepare={summary.Median.PrepareRestoreMs:F3} ms, " +
        $"restore WITHOUT engine={summary.Median.RestoreWithoutEngineMs:F3} ms");
}

var report = new
{
    SchemaVersion = 1,
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Framework = RuntimeInformation.FrameworkDescription,
    RuntimeVersion = Environment.Version.ToString(),
    OperatingSystem = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    ProcessorCount = Environment.ProcessorCount,
    ServerGc = System.Runtime.GCSettings.IsServerGC,
    InputSha256 = originalHash,
    Samples = samples,
    Warmups = warmups,
    Limitations = new[]
    {
        "Linked production source; isolated storage/restore probe, not full application startup.",
        "Fixtures are written immediately before reading: OS cache is warm/uncontrolled, not cold disk.",
        "Synthetic source bytes are opaque data to the repository and may not be valid torrent metadata.",
        "RestoreWithoutEngine includes preparation and real Task.Delay(5), but all gateway methods are no-ops.",
        "No WinUI, DI container, real engine, sockets, downloaded file I/O, hashing, or first frame is measured.",
        "Allocation deltas include all managed threads in this probe process; no forced GC between samples.",
        "Results from different target runtimes are not results of migrating the whole application."
    },
    Scenarios = results
};
await File.WriteAllTextAsync(Path.Combine(output, "results.json"),
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Results: {Path.Combine(output, "results.json")}");

static double Median(IEnumerable<double> values)
{
    var sorted = values.Order().ToArray();
    return sorted.Length % 2 == 0
        ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2
        : sorted[sorted.Length / 2];
}

static TorrentDownloadRecord CreateTemplate(int files, int sourceBytes) => new()
{
    Id = Guid.NewGuid(),
    Name = "Synthetic torrent",
    InfoHash = new string('a', 40),
    SavePath = "unused-probe-path",
    Status = TorrentDownloadStatus.Paused,
    FileEntries = Enumerable.Range(0, files).Select(index => new TorrentFileEntryRecord
    {
        FullPath = $"folder/file-{index:D5}.bin", SizeByte = 1024 * 1024, IsSelected = true
    }).ToList(),
    Source = new TorrentSourceRecord
    {
        Kind = TorrentSourceKind.TorrentBytes,
        Value = Convert.ToBase64String(new byte[sourceBytes])
    }
};

static TorrentRepositoryDocument Repeat(TorrentDownloadRecord template, int count) => new()
{
    Items = Enumerable.Range(0, count).Select(index => new TorrentDownloadRecord
    {
        Id = new Guid(index + 1, 0, 0, new byte[8]),
        Name = $"Probe entry {index:D5}",
        InfoHash = index.ToString("x40"),
        SavePath = template.SavePath,
        Status = template.Status,
        FileEntries = template.FileEntries,
        Source = template.Source
    }).ToList()
};

sealed record Scenario(string Name, TorrentRepositoryDocument Document, string Origin);
sealed record Measurement(int Iteration, bool Warmup, double ConstructorMs, double LoadMs,
    double ListMs, double PrepareRestoreMs, double RestoreWithoutEngineMs,
    long ReadAllocatedBytes, int ReadGen0, int ReadGen1, int ReadGen2, int Added, int Started);

sealed class LocalProbeStorage(string root) : IAppStorageService
{
    public string GetLocalPath(string relative) => Path.Combine(root, relative);
    public string GetCachePath(string relative) => Path.Combine(root, "Cache", relative);
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);
    public void EnsureParentDirectory(string filePath) => Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
}

sealed class NoOpRuntimeGateway : ITorrentRuntimeGateway
{
    public int Added { get; private set; }
    public int Started { get; private set; }
    public Task AddAsync(Guid id, TorrentSource source, string savePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Added++;
        return Task.CompletedTask;
    }
    public Task<bool> ExistsByIdAsync(Guid id) => Task.FromResult(false);
    public Task StartAsync(Guid id, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Started++; return Task.CompletedTask; }
    public Task UpdateFileSelectionAsync(Guid id, List<TorrentFileEntry> files, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public Task PauseAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task RemoveAsync(Guid id, bool deleteFiles, CancellationToken ct) => throw new NotSupportedException();
}
