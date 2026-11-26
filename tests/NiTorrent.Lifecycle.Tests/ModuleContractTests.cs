using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Settings.Enums;
using NiTorrent.Domain.Torrents;
using NiTorrent.Torrents.Contracts;
using Xunit;

namespace NiTorrent.Lifecycle.Tests;

public sealed class ModuleContractTests
{
    [Fact]
    public async Task PreviewAndAddKeepSelectionDetachedAndDetectDuplicatesBeforeEngineStartup()
    {
        await using var f = new SessionFixture();
        var module = f.Host.Services.GetRequiredService<ITorrentsModule>();
        var info = new MonoTorrent.BEncoding.BEncodedDictionary
        {
            ["name"] = new MonoTorrent.BEncoding.BEncodedString("test.bin"),
            ["length"] = new MonoTorrent.BEncoding.BEncodedNumber(1),
            ["piece length"] = new MonoTorrent.BEncoding.BEncodedNumber(16384),
            ["pieces"] = new MonoTorrent.BEncoding.BEncodedString(System.Security.Cryptography.SHA1.HashData([42]))
        };
        var bytes = new MonoTorrent.BEncoding.BEncodedDictionary { ["info"] = info }.Encode();
        var source = new NiTorrent.Application.Torrents.TorrentSource.TorrentBytes(bytes);
        var preview = await module.PreviewAsync(source);
        Assert.False(preview.AlreadyExists);
        Assert.Equal("test.bin", preview.Name);
        Assert.Equal(1, preview.TotalSize);
        var file = Assert.Single(preview.Files);
        Assert.True(file.IsSelected);
        var added = await module.AddAsync(source, f.Storage.Root, preview.Files);
        file.IsSelected = false;
        Assert.True(Assert.Single((await f.Repository.GetByIdAsync(added.Id, default))!.FileEntries).IsSelected);
        Assert.True((await module.PreviewAsync(source)).AlreadyExists);
        Assert.Equal(added.Id, Assert.Single(await module.GetAllAsync()).Id);
        Assert.Equal(TorrentDownloadStatus.Running, added.Status);
        Assert.Equal(0, f.Engine.Attempts);
        Assert.Empty(f.Runtime.Calls);
    }

    [Fact]
    public async Task ModuleCommandsPreserveIntentAndReturnedCollectionIsDetached()
    {
        await using var f = new SessionFixture();
        var item = await f.AddAsync(TorrentDownloadStatus.Paused);
        var module = f.Host.Services.GetRequiredService<ITorrentsModule>();
        var before = Assert.Single(await module.GetAllAsync());
        await module.StartAsync(item.Id);
        Assert.Equal(TorrentDownloadStatus.Running, Assert.Single(await module.GetAllAsync()).Status);
        Assert.Equal(TorrentDownloadStatus.Paused, before.Status);
        await module.PauseAsync(item.Id);
        Assert.Equal(TorrentDownloadStatus.Paused, Assert.Single(await module.GetAllAsync()).Status);
        Assert.Equal(0, f.Engine.Attempts);
        await module.DeleteAsync(item.Id, false);
        Assert.Empty(await module.GetAllAsync());
        Assert.Equal(item.Id, before.Id);
        Assert.Empty(f.Runtime.Calls);
    }

    [Fact]
    public async Task SettingsQueriesAndSavedInputCannotMutateModuleStateByAliasing()
    {
        await using var f = new SessionFixture();
        await f.Host.StartAsync();
        var module = f.Host.Services.GetRequiredService<ITorrentSettings>();
        var draft = await module.GetAsync();
        var originalConnections = draft.EngineSettings.MaximumConnections;
        draft.EngineSettings.MaximumConnections = 17;
        Assert.Equal(originalConnections, (await module.GetAsync()).EngineSettings.MaximumConnections);
        draft.CloseBehavior = AppCloseBehavior.MinimizeToTray;
        await module.SaveAsync(draft);
        draft.EngineSettings.MaximumConnections = 999;
        var current = module.Current;
        Assert.Equal(17, current.EngineSettings.MaximumConnections);
        Assert.Equal(AppCloseBehavior.MinimizeToTray, current.CloseBehavior);
        current.EngineSettings.AllowDht = false;
        Assert.True(module.Current.EngineSettings.AllowDht);
    }
}
