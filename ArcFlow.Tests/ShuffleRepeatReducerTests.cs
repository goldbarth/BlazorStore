using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace ArcFlow.Tests;

public class ShuffleRepeatReducerTests
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

    private static YouTubePlayerState StateWithCurrentVideo(VideoItem[] videos, int currentIdx)
    {
        return new YouTubePlayerState() with
        {
            Queue = new QueueState
            {
                SelectedPlaylistId = PlaylistId,
                Videos = videos.ToImmutableList(),
                CurrentIndex = currentIdx,
                CurrentItemId = videos[currentIdx].Id
            },
            Player = new PlayerState.Playing(videos[currentIdx].YouTubeId)
        };
    }

    #region ShuffleSet

    [Fact]
    public void ShuffleSet_Enable_GeneratesShuffleOrder()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 0);

        var result = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));

        Assert.True(result.Queue.ShuffleEnabled);
        Assert.Equal(3, result.Queue.ShuffleOrder.Count);
        Assert.Equal(v0.Id, result.Queue.ShuffleOrder[0]); // Current at front
        Assert.Empty(result.Queue.PlaybackHistory);
    }

    [Fact]
    public void ShuffleSet_Disable_ClearsShuffleState()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0) with
        {
            Queue = (StateWithCurrentVideo([v0, v1], 0)).Queue with
            {
                ShuffleEnabled = true,
                ShuffleOrder = ImmutableList.Create(v0.Id, v1.Id),
                PlaybackHistory = ImmutableList.Create(v1.Id)
            }
        };

        var result = store.Reduce(state, new YtAction.ShuffleSet(false));

        Assert.False(result.Queue.ShuffleEnabled);
        Assert.Empty(result.Queue.ShuffleOrder);
        Assert.Empty(result.Queue.PlaybackHistory);
    }

    [Fact]
    public void ShuffleSet_PreservesCurrentItemId()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 1);

        var result = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));

        Assert.Equal(v1.Id, result.Queue.CurrentItemId);
    }

    #endregion

    #region RepeatSet

    [Fact]
    public void RepeatSet_ChangesMode()
    {
        var store = CreateStore();
        var state = StateWithVideos(MakeVideo(0));

        var result = store.Reduce(state, new YtAction.RepeatSet(RepeatMode.All));
        Assert.Equal(RepeatMode.All, result.Queue.RepeatMode);

        result = store.Reduce(result, new YtAction.RepeatSet(RepeatMode.One));
        Assert.Equal(RepeatMode.One, result.Queue.RepeatMode);

        result = store.Reduce(result, new YtAction.RepeatSet(RepeatMode.Off));
        Assert.Equal(RepeatMode.Off, result.Queue.RepeatMode);
    }

    #endregion

    #region NextRequested

    [Fact]
    public void NextRequested_Sequential_RepeatOff_AdvancesToNext()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        var result = store.Reduce(state, new YtAction.NextRequested());

        Assert.Equal(1, result.Queue.CurrentIndex);
        Assert.Equal(v1.Id, result.Queue.CurrentItemId);
        Assert.IsType<PlayerState.Loading>(result.Player);
    }

    [Fact]
    public void NextRequested_Sequential_RepeatOff_AtEnd_Stops()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 1);

        var result = store.Reduce(state, new YtAction.NextRequested());

        Assert.IsType<PlayerState.Paused>(result.Player);
    }

    [Fact]
    public void NextRequested_Sequential_RepeatAll_Wraps()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 1) with
        {
            Queue = (StateWithCurrentVideo([v0, v1], 1)).Queue with
            {
                RepeatMode = RepeatMode.All
            }
        };

        var result = store.Reduce(state, new YtAction.NextRequested());

        Assert.Equal(0, result.Queue.CurrentIndex);
        Assert.Equal(v0.Id, result.Queue.CurrentItemId);
    }

    [Fact]
    public void NextRequested_RepeatOne_ReplaysCurrentVideo()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0) with
        {
            Queue = (StateWithCurrentVideo([v0, v1], 0)).Queue with
            {
                RepeatMode = RepeatMode.One
            }
        };

        var result = store.Reduce(state, new YtAction.NextRequested());

        Assert.Equal(0, result.Queue.CurrentIndex);
        Assert.Equal(v0.Id, result.Queue.CurrentItemId);
        Assert.IsType<PlayerState.Loading>(result.Player);
    }

    [Fact]
    public void NextRequested_Shuffle_FollowsShuffleOrder()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var shuffleOrder = ImmutableList.Create(v0.Id, v2.Id, v1.Id);

        var state = StateWithCurrentVideo([v0, v1, v2], 0) with
        {
            Queue = (StateWithCurrentVideo([v0, v1, v2], 0)).Queue with
            {
                ShuffleEnabled = true,
                ShuffleOrder = shuffleOrder
            }
        };

        var result = store.Reduce(state, new YtAction.NextRequested());

        Assert.Equal(v2.Id, result.Queue.CurrentItemId);
        Assert.Equal(2, result.Queue.CurrentIndex);
    }

    #endregion

    #region PrevRequested

    [Fact]
    public void PrevRequested_Sequential_GoesToPrevious()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 1);

        var result = store.Reduce(state, new YtAction.PrevRequested());

        Assert.Equal(0, result.Queue.CurrentIndex);
        Assert.Equal(v0.Id, result.Queue.CurrentItemId);
    }

    [Fact]
    public void PrevRequested_Sequential_AtStart_IsNoOp()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        var result = store.Reduce(state, new YtAction.PrevRequested());

        Assert.Equal(0, result.Queue.CurrentIndex);
    }

    [Fact]
    public void PrevRequested_Shuffle_PopsFromHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);

        var state = StateWithCurrentVideo([v0, v1, v2], 2) with
        {
            Queue = (StateWithCurrentVideo([v0, v1, v2], 2)).Queue with
            {
                ShuffleEnabled = true,
                ShuffleOrder = ImmutableList.Create(v0.Id, v2.Id, v1.Id),
                PlaybackHistory = ImmutableList.Create(v0.Id, v1.Id)
            }
        };

        var result = store.Reduce(state, new YtAction.PrevRequested());

        Assert.Equal(v1.Id, result.Queue.CurrentItemId);
        Assert.Single(result.Queue.PlaybackHistory);
        Assert.Equal(v0.Id, result.Queue.PlaybackHistory[0]);
    }

    #endregion

    #region Undo/Redo preservation

    [Fact]
    public void NextRequested_DoesNotCreateUndoHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        // First create some undo history via SelectVideo
        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Single(s1.Queue.Past);

        // NextRequested should preserve undo history
        var s2 = store.Reduce(s1, new YtAction.NextRequested());

        Assert.Single(s2.Queue.Past);
        Assert.Empty(s2.Queue.Future);
    }

    [Fact]
    public void ShuffleSet_DoesNotCreateUndoHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Single(s1.Queue.Past);

        var s2 = store.Reduce(s1, new YtAction.ShuffleSet(true, Seed: 42));
        Assert.Single(s2.Queue.Past);
    }

    [Fact]
    public void RepeatSet_DoesNotCreateUndoHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        var s2 = store.Reduce(s1, new YtAction.RepeatSet(RepeatMode.All));

        Assert.Single(s2.Queue.Past);
    }

    [Fact]
    public void Undo_Redo_PreservesShuffleRepeatState()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        // Enable shuffle
        var s1 = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));
        // Set repeat
        var s2 = store.Reduce(s1, new YtAction.RepeatSet(RepeatMode.All));
        // SelectVideo creates undo entry
        var s3 = store.Reduce(s2, new YtAction.SelectVideo(1, Autoplay: false));

        Assert.True(s3.Queue.ShuffleEnabled);
        Assert.Equal(RepeatMode.All, s3.Queue.RepeatMode);

        // Undo restores the snapshot (which includes shuffle/repeat state)
        var s4 = store.Reduce(s3, new YtAction.UndoRequested());

        // The snapshot was taken AFTER shuffle+repeat were set, so they're preserved
        Assert.True(s4.Queue.ShuffleEnabled);
        Assert.Equal(RepeatMode.All, s4.Queue.RepeatMode);
    }

    #endregion

    #region Queue mutations with shuffle active

    [Fact]
    public void PlaylistLoaded_ClearsShuffleAndHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0) with
        {
            Queue = (StateWithCurrentVideo([v0, v1], 0)).Queue with
            {
                ShuffleEnabled = true,
                ShuffleOrder = ImmutableList.Create(v0.Id, v1.Id),
                PlaybackHistory = ImmutableList.Create(v0.Id)
            }
        };

        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Name = "New",
            VideoItems = [MakeVideo(0), MakeVideo(1)]
        };

        var result = store.Reduce(state, new YtAction.PlaylistLoaded(playlist));

        Assert.Empty(result.Queue.ShuffleOrder);
        Assert.Empty(result.Queue.PlaybackHistory);
        Assert.Null(result.Queue.CurrentItemId);
    }

    [Fact]
    public void SortChanged_PreservesCurrentItemId()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 0);

        // Sort: move index 0 → index 2
        var result = store.Reduce(state, new YtAction.SortChanged(0, 2));

        // CurrentItemId should still point to v0 even though index changed
        Assert.Equal(v0.Id, result.Queue.CurrentItemId);
    }

    #endregion

    // Minimal fakes
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
