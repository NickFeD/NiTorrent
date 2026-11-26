using Newtonsoft.Json;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Settings.Enums;
using NiTorrent.Infrastructure.Settings;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Presentation.Abstractions;
using NiTorrent.Presentation.Features.Settings;

namespace NiTorrent.Lifecycle.Tests;

public sealed class TraySettingsTests
{
    [Theory]
    [InlineData(AppCloseBehavior.ExitApplication)]
    [InlineData(AppCloseBehavior.MinimizeToTray)]
    [InlineData(AppCloseBehavior.AskUser)]
    public async Task SettingsEditorSavesAllThreeModesWithoutLosingEngineSettings(AppCloseBehavior behavior)
    {
        await using var f = new SessionFixture();
        var repository = f.Host.Services.GetRequiredService<ISettingsRepository>();
        await repository.SaveAsync(new AppSettings
        {
            CloseBehavior = AppCloseBehavior.MinimizeToTray,
            EngineSettings = new() { MaximumConnections = 87, AllowDht = false }
        }, default);
        await f.Host.StartAsync();
        var settings = f.Host.Services.GetRequiredService<AppSettingsService>();
        var editor = new TorrentSettingsViewModel(new TorrentSettingsModule(settings, new GetSettingsQuery(repository),
            new UpdateSettingsUseCase(settings, new EngineSettings(), f.Session)), new Picker());
        await editor.EnsureLoadedAsync();
        Assert.Equal((int)AppCloseBehavior.MinimizeToTray, editor.CloseBehaviorIndex);
        editor.CloseBehaviorIndex = (int)behavior;
        await editor.SaveCommand.ExecuteAsync(null);
        var saved = await repository.GetAsync(default);
        Assert.Equal(behavior, saved.CloseBehavior);
        Assert.Equal(87, saved.EngineSettings.MaximumConnections);
        Assert.False(saved.EngineSettings.AllowDht);
        Assert.False(editor.HasUnsavedChanges);
    }

    private sealed class EngineSettings : IEngineSettingsService
    {
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task ApplySettingsAsync(TorrentEngineSettings settings, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Picker : IPickerHelper
    {
        public Task<string?> PickSingleFilePathAsync(params string[] fileTypes) => throw new NotSupportedException();
        public Task<string?> PickSingleFolderPathAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public void NewAndMissingSettingsDefaultToAsk()
    {
        Assert.Equal(AppCloseBehavior.AskUser, new AppSettings().CloseBehavior);
        Assert.Equal(AppCloseBehavior.AskUser, new AppJsonSettings().CloseBehavior);
        Assert.Equal(AppCloseBehavior.AskUser, JsonConvert.DeserializeObject<AppJsonSettings>("{}")!.CloseBehavior);
    }

    [Theory]
    [InlineData(AppCloseBehavior.ExitApplication)]
    [InlineData(AppCloseBehavior.MinimizeToTray)]
    [InlineData(AppCloseBehavior.AskUser)]
    public void ExplicitLegacyChoiceSurvivesSettingsLoad(AppCloseBehavior behavior)
    {
        var json = JsonConvert.SerializeObject(new AppSettings { CloseBehavior = behavior });
        Assert.Equal(behavior, JsonConvert.DeserializeObject<AppJsonSettings>(json)!.CloseBehavior);
    }
}
