using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace ArcFlow.Tests;

public class UndoRedoReducerTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();

    private static YouTubePlayerStore CreateStore()
    {
        var playlistService = new FakePlaylistService();
        var jsRuntime = new FakeJSRuntime();
        var logger = NullLogger<YouTubePlayerStore>.Instance;
        return new YouTubePlayerStore(playlistService, jsRuntime, logger);
    }

    private static VideoItem MakeVideo(int position) => new()
    {
        Id = Guid.NewGuid(),
        YouTubeId = $"yt_{position}",
        Title = $"Video {position}",
        Position = position,
        PlaylistId = PlaylistId
    };

    private static YouTubePlayerState StateWithVideos(params VideoItem[] videos)
    {
        return new YouTubePlayerState() with
        {
            Queue = new QueueState
            {
                SelectedPlaylistId = PlaylistId,
                Videos = videos.ToImmutableList(),
                CurrentIndex = null
            }
        };
    }

    [Fact]
    public void Undo_WithEmptyPast_IsNoOp()
    {
        var store = CreateStore();
        var state = StateWithVideos(MakeVideo(0));

        var result = store.Reduce(state, new YtAction.UndoRequested());

        Assert.Same(state, result);
    }

    [Fact]
    public void Redo_WithEmptyFuture_IsNoOp()
    {
        var store = CreateStore();
        var state = StateWithVideos(MakeVideo(0));

        var result = store.Reduce(state, new YtAction.RedoRequested());

        Assert.Same(state, result);
    }

    [Fact]
    public void SelectVideo_PushesSnapshotToPast()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithVideos(v0, v1);

        var result = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));

        Assert.Single(result.Queue.Past);
        Assert.Equal(1, result.Queue.CurrentIndex);
    }

    [Fact]
    public void Undo_RestoresPreviousState()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithVideos(v0, v1);

        // Select video 1 → creates history entry
        var afterSelect = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Equal(1, afterSelect.Queue.CurrentIndex);

        // Undo → should restore null CurrentIndex
        var afterUndo = store.Reduce(afterSelect, new YtAction.UndoRequested());
        Assert.Null(afterUndo.Queue.CurrentIndex);
        Assert.Empty(afterUndo.Queue.Past);
        Assert.Single(afterUndo.Queue.Future);
    }

    [Fact]
    public void Redo_RestoresUndoneState()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithVideos(v0, v1);

        var afterSelect = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        var afterUndo = store.Reduce(afterSelect, new YtAction.UndoRequested());
        var afterRedo = store.Reduce(afterUndo, new YtAction.RedoRequested());

        Assert.Equal(1, afterRedo.Queue.CurrentIndex);
        Assert.Single(afterRedo.Queue.Past);
        Assert.Empty(afterRedo.Queue.Future);
    }

    [Fact]
    public void NewMutation_AfterUndo_ClearsFuture()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithVideos(v0, v1, v2);

        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        var s2 = store.Reduce(s1, new YtAction.UndoRequested());
        Assert.Single(s2.Queue.Future);

        // New mutation should clear Future
        var s3 = store.Reduce(s2, new YtAction.SelectVideo(2, Autoplay: false));
        Assert.Empty(s3.Queue.Future);
        Assert.Single(s3.Queue.Past);
    }

    [Fact]
    public void SortChanged_Undo_RestoresVideoPositions()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithVideos(v0, v1, v2);

        // Sort: move index 0 → index 2
        var afterSort = store.Reduce(state, new YtAction.SortChanged(0, 2));

        // After sort, positions should have been mutated by HandleSortChanged
        // The order in the list should be: v1, v2, v0
        Assert.Same(v1, afterSort.Queue.Videos[0]);
        Assert.Same(v2, afterSort.Queue.Videos[1]);
        Assert.Same(v0, afterSort.Queue.Videos[2]);

        // Undo → should restore original order and positions
        var afterUndo = store.Reduce(afterSort, new YtAction.UndoRequested());

        Assert.Equal(3, afterUndo.Queue.Videos.Count);
        Assert.Same(v0, afterUndo.Queue.Videos[0]);
        Assert.Same(v1, afterUndo.Queue.Videos[1]);
        Assert.Same(v2, afterUndo.Queue.Videos[2]);

        // Positions should be restored
        Assert.Equal(0, afterUndo.Queue.Videos[0].Position);
        Assert.Equal(1, afterUndo.Queue.Videos[1].Position);
        Assert.Equal(2, afterUndo.Queue.Videos[2].Position);
    }

    [Fact]
    public void HistoryLimit_TrimsOldestEntries()
    {
        var store = CreateStore();
        var videos = Enumerable.Range(0, QueueState.HistoryLimit + 10)
            .Select(MakeVideo)
            .ToArray();
        var state = StateWithVideos(videos);

        var current = state;
        for (int i = 0; i < QueueState.HistoryLimit + 5; i++)
        {
            current = store.Reduce(current, new YtAction.SelectVideo(i % videos.Length, Autoplay: false));
        }

        Assert.Equal(QueueState.HistoryLimit, current.Queue.Past.Count);
    }

    [Fact]
    public void PlaylistLoaded_ClearsPastAndFuture()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithVideos(v0, v1);

        // Build up some history
        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Single(s1.Queue.Past);

        // PlaylistLoaded is a boundary → clears history
        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = "New Playlist",
            VideoItems = [MakeVideo(0)]
        };
        var s2 = store.Reduce(s1, new YtAction.PlaylistLoaded(playlist));

        Assert.Empty(s2.Queue.Past);
        Assert.Empty(s2.Queue.Future);
    }

    [Fact]
    public void SelectPlaylist_ClearsPastAndFuture()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithVideos(v0, v1);

        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Single(s1.Queue.Past);

        var s2 = store.Reduce(s1, new YtAction.SelectPlaylist(Guid.NewGuid()));

        Assert.Empty(s2.Queue.Past);
        Assert.Empty(s2.Queue.Future);
    }

    [Fact]
    public void PlayerStateChanged_DoesNotCreateHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var state = StateWithVideos(v0) with
        {
            Player = new PlayerState.Loading("yt_0", true)
        };

        var result = store.Reduce(state, new YtAction.PlayerStateChanged(1, "yt_0"));

        Assert.Empty(result.Queue.Past);
        Assert.Empty(result.Queue.Future);
    }

    [Fact]
    public void MultiStep_UndoRedo_Sequence()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithVideos(v0, v1, v2);

        // Step 1: select video 0
        var s1 = store.Reduce(state, new YtAction.SelectVideo(0, Autoplay: false));
        Assert.Equal(0, s1.Queue.CurrentIndex);
        Assert.Single(s1.Queue.Past);

        // Step 2: select video 1
        var s2 = store.Reduce(s1, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Equal(1, s2.Queue.CurrentIndex);
        Assert.Equal(2, s2.Queue.Past.Count);

        // Step 3: select video 2
        var s3 = store.Reduce(s2, new YtAction.SelectVideo(2, Autoplay: false));
        Assert.Equal(2, s3.Queue.CurrentIndex);
        Assert.Equal(3, s3.Queue.Past.Count);

        // Undo back to video 1
        var u1 = store.Reduce(s3, new YtAction.UndoRequested());
        Assert.Equal(1, u1.Queue.CurrentIndex);
        Assert.Equal(2, u1.Queue.Past.Count);
        Assert.Single(u1.Queue.Future);

        // Undo back to video 0
        var u2 = store.Reduce(u1, new YtAction.UndoRequested());
        Assert.Equal(0, u2.Queue.CurrentIndex);
        Assert.Single(u2.Queue.Past);
        Assert.Equal(2, u2.Queue.Future.Count);

        // Redo to video 1
        var r1 = store.Reduce(u2, new YtAction.RedoRequested());
        Assert.Equal(1, r1.Queue.CurrentIndex);
        Assert.Equal(2, r1.Queue.Past.Count);
        Assert.Single(r1.Queue.Future);

        // Redo to video 2
        var r2 = store.Reduce(r1, new YtAction.RedoRequested());
        Assert.Equal(2, r2.Queue.CurrentIndex);
        Assert.Equal(3, r2.Queue.Past.Count);
        Assert.Empty(r2.Queue.Future);
    }

    [Fact]
    public void NextRequested_DoesNotCreateUndoHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithVideos(v0, v1) with
        {
            Queue = (StateWithVideos(v0, v1)).Queue with
            {
                CurrentIndex = 0,
                CurrentItemId = v0.Id
            },
            Player = new PlayerState.Playing(v0.YouTubeId)
        };

        var result = store.Reduce(state, new YtAction.NextRequested());

        Assert.Empty(result.Queue.Past);
        Assert.Empty(result.Queue.Future);
    }

    // Minimal fakes to satisfy constructor dependencies
    private class FakePlaylistService : ArcFlow.Features.YouTubePlayer.Service.IPlaylistService
    {
        public Task<List<Playlist>> GetAllPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Playlist?> GetPlaylistByIdAsync(Guid id) => Task.FromResult<Playlist?>(null);
        public Task CreatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task UpdatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
        public Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video) => Task.CompletedTask;
        public Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId) => Task.CompletedTask;
        public Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos) => Task.CompletedTask;
        public Task ReplaceAllPlaylistsAsync(List<Playlist> playlists) => Task.CompletedTask;
    }

    private class FakeJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => default;
    }
}