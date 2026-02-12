using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace ArcFlow.Tests;

public class PersistEffectTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();

    private static Playlist MakePlaylist(int videoCount = 2) => new()
    {
        Id = PlaylistId,
        Name = "Test Playlist",
        Description = "desc",
        VideoItems = Enumerable.Range(0, videoCount).Select(i => new VideoItem
        {
            Id = Guid.NewGuid(),
            YouTubeId = $"yt_{i}",
            Title = $"Video {i}",
            Position = i,
            PlaylistId = PlaylistId
        }).ToList()
    };

    private static ImmutableList<Playlist> MakeImportedPlaylists(int count = 2, int videosEach = 3)
    {
        return Enumerable.Range(0, count).Select(p =>
        {
            var plId = Guid.NewGuid();
            return new Playlist
            {
                Id = plId,
                Name = $"Imported {p}",
                VideoItems = Enumerable.Range(0, videosEach).Select(v => new VideoItem
                {
                    Id = Guid.NewGuid(),
                    YouTubeId = $"imp_yt_{p}_{v}",
                    Title = $"Imp Video {p}-{v}",
                    Position = v,
                    PlaylistId = plId
                }).ToList()
            };
        }).ToImmutableList();
    }

    #region (a) ImportApplied triggers persist

    [Fact]
    public async Task ImportApplied_TriggersPersistEffect()
    {
        var service = new TrackingPlaylistService();
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);
        var imported = MakeImportedPlaylists();

        await store.Dispatch(new YtAction.ImportApplied(imported, imported[0].Id));
        await Task.Delay(200);

        Assert.Contains("ReplaceAllPlaylists", service.Calls);
    }

    [Fact]
    public async Task ImportApplied_PersistSucceeded_ClearsDirty()
    {
        var service = new TrackingPlaylistService();
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);
        var imported = MakeImportedPlaylists();

        await store.Dispatch(new YtAction.ImportApplied(imported, imported[0].Id));
        await Task.Delay(200);

        Assert.False(store.State.Persistence.IsDirty);
        Assert.Null(store.State.Persistence.LastPersistError);
        Assert.NotNull(store.State.Persistence.LastPersistAttemptUtc);
    }

    #endregion

    #region (b) PersistRequested explicit retry

    [Fact]
    public async Task PersistRequested_WhenDirty_Persists()
    {
        var service = new TrackingPlaylistService();
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);
        var imported = MakeImportedPlaylists();

        // Set up loaded + dirty state via ImportApplied
        await store.Dispatch(new YtAction.ImportApplied(imported, imported[0].Id));
        await Task.Delay(200);

        // First persist should have succeeded, clear the calls
        service.Calls.Clear();

        // Make dirty again by re-importing
        await store.Dispatch(new YtAction.ImportApplied(imported, imported[0].Id));
        await Task.Delay(200);

        Assert.Contains("ReplaceAllPlaylists", service.Calls);
    }

    [Fact]
    public async Task PersistRequested_WhenNotDirty_Skips()
    {
        var service = new TrackingPlaylistService();
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);

        // State is not dirty by default
        await store.Dispatch(new YtAction.PersistRequested());
        await Task.Delay(200);

        Assert.DoesNotContain("ReplaceAllPlaylists", service.Calls);
    }

    #endregion

    #region (c) Persist failure

    [Fact]
    public async Task PersistFailed_KeepsDirty_SetsError()
    {
        var service = new FailingPlaylistService("disk full");
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);
        var imported = MakeImportedPlaylists();

        await store.Dispatch(new YtAction.ImportApplied(imported, imported[0].Id));
        await Task.Delay(200);

        Assert.True(store.State.Persistence.IsDirty);
        Assert.Equal("disk full", store.State.Persistence.LastPersistError);
        Assert.NotNull(store.State.Persistence.LastPersistAttemptUtc);
    }

    [Fact]
    public async Task PersistFailed_ThenRetry_Succeeds()
    {
        var service = new ConfigurablePlaylistService();
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);
        var imported = MakeImportedPlaylists();

        // First attempt fails
        service.ShouldFail = true;
        service.FailMessage = "transient error";
        await store.Dispatch(new YtAction.ImportApplied(imported, imported[0].Id));
        await Task.Delay(200);

        Assert.True(store.State.Persistence.IsDirty);
        Assert.Equal("transient error", store.State.Persistence.LastPersistError);

        // Retry succeeds
        service.ShouldFail = false;
        await store.Dispatch(new YtAction.PersistRequested());
        await Task.Delay(200);

        Assert.False(store.State.Persistence.IsDirty);
        Assert.Null(store.State.Persistence.LastPersistError);
    }

    #endregion

    #region (d) Reducer: PersistRequested is effect-only (no state change)

    [Fact]
    public void PersistRequested_DoesNotChangePersistenceState()
    {
        var service = new TrackingPlaylistService();
        var store = new YouTubePlayerStore(service, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);
        var state = new YouTubePlayerState { Persistence = new PersistenceState(IsDirty: true) };

        var result = store.Reduce(state, new YtAction.PersistRequested());

        // Reducer returns state as-is — the effect handles the actual work
        Assert.True(result.Persistence.IsDirty);
        Assert.Null(result.Persistence.LastPersistAttemptUtc);
    }

    #endregion

    #region Fakes

    private class TrackingPlaylistService : Features.YouTubePlayer.Service.IPlaylistService
    {
        public List<string> Calls { get; } = [];

        public Task<List<Playlist>> GetAllPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Playlist?> GetPlaylistByIdAsync(Guid id) => Task.FromResult<Playlist?>(null);
        public Task CreatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task UpdatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
        public Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video) => Task.CompletedTask;
        public Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId) => Task.CompletedTask;
        public Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos) => Task.CompletedTask;

        public Task ReplaceAllPlaylistsAsync(List<Playlist> playlists)
        {
            Calls.Add("ReplaceAllPlaylists");
            return Task.CompletedTask;
        }
    }

    private class FailingPlaylistService : Features.YouTubePlayer.Service.IPlaylistService
    {
        private readonly string _message;
        public FailingPlaylistService(string message) => _message = message;

        public Task<List<Playlist>> GetAllPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Playlist?> GetPlaylistByIdAsync(Guid id) => Task.FromResult<Playlist?>(null);
        public Task CreatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task UpdatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
        public Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video) => Task.CompletedTask;
        public Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId) => Task.CompletedTask;
        public Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos) => Task.CompletedTask;

        public Task ReplaceAllPlaylistsAsync(List<Playlist> playlists)
            => throw new InvalidOperationException(_message);
    }

    private class ConfigurablePlaylistService : Features.YouTubePlayer.Service.IPlaylistService
    {
        public bool ShouldFail { get; set; }
        public string FailMessage { get; set; } = "error";

        public Task<List<Playlist>> GetAllPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Playlist?> GetPlaylistByIdAsync(Guid id) => Task.FromResult<Playlist?>(null);
        public Task CreatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task UpdatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
        public Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video) => Task.CompletedTask;
        public Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId) => Task.CompletedTask;
        public Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos) => Task.CompletedTask;

        public Task ReplaceAllPlaylistsAsync(List<Playlist> playlists)
            => ShouldFail
                ? throw new InvalidOperationException(FailMessage)
                : Task.CompletedTask;
    }

    private class FakeJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => default;
    }

    #endregion
}
