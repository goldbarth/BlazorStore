using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace ArcFlow.Tests;

public class EffectGatingTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();

    private static VideoItem MakeVideo(int position) => new()
    {
        Id = Guid.NewGuid(),
        YouTubeId = $"yt_{position}",
        Title = $"Video {position}",
        Position = position,
        PlaylistId = PlaylistId
    };

    [Fact]
    public async Task UndoRequested_TriggersNoEffects()
    {
        var tracker = new EffectTrackingPlaylistService();
        var store = new YouTubePlayerStore(
            tracker, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);

        // Build state with a video and history
        await store.Dispatch(new YtAction.UndoRequested());

        // Give time for processing
        await Task.Delay(100);

        Assert.Empty(tracker.Calls);
    }

    [Fact]
    public async Task RedoRequested_TriggersNoEffects()
    {
        var tracker = new EffectTrackingPlaylistService();
        var store = new YouTubePlayerStore(
            tracker, new FakeJSRuntime(), NullLogger<YouTubePlayerStore>.Instance);

        await store.Dispatch(new YtAction.RedoRequested());

        await Task.Delay(100);

        Assert.Empty(tracker.Calls);
    }

    private class EffectTrackingPlaylistService : ArcFlow.Features.YouTubePlayer.Service.IPlaylistService
    {
        public List<string> Calls { get; } = [];

        public Task<List<Playlist>> GetAllPlaylistsAsync()
        {
            Calls.Add("GetAllPlaylists");
            return Task.FromResult(new List<Playlist>());
        }

        public Task<Playlist?> GetPlaylistByIdAsync(Guid id)
        {
            Calls.Add("GetPlaylistById");
            return Task.FromResult<Playlist?>(null);
        }

        public Task CreatePlaylistAsync(Playlist playlist)
        {
            Calls.Add("CreatePlaylist");
            return Task.CompletedTask;
        }

        public Task UpdatePlaylistAsync(Playlist playlist)
        {
            Calls.Add("UpdatePlaylist");
            return Task.CompletedTask;
        }

        public Task DeletePlaylistAsync(Guid playlistId)
        {
            Calls.Add("DeletePlaylist");
            return Task.CompletedTask;
        }

        public Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video)
        {
            Calls.Add("AddVideoToPlaylist");
            return Task.CompletedTask;
        }

        public Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId)
        {
            Calls.Add("RemoveVideoFromPlaylist");
            return Task.CompletedTask;
        }

        public Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos)
        {
            Calls.Add("UpdateVideoPositions");
            return Task.CompletedTask;
        }

        public Task ReplaceAllPlaylistsAsync(List<Playlist> playlists)
        {
            Calls.Add("ReplaceAllPlaylists");
            return Task.CompletedTask;
        }
    }

    private class FakeJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => default;
    }
}