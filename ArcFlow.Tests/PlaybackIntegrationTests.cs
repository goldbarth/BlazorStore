using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace ArcFlow.Tests;

public class PlaybackIntegrationTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();

    private static YouTubePlayerStore CreateStore()
    {
        return new YouTubePlayerStore(
            new FakePlaylistService(),
            new FakeJSRuntime(),
            NullLogger<YouTubePlayerStore>.Instance);
    }

    private static VideoItem MakeVideo(int position) => new()
    {
        Id = Guid.NewGuid(),
        YouTubeId = $"yt_{position}",
        Title = $"Video {position}",
        Position = position,
        PlaylistId = PlaylistId
    };

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

    #region Sequential + RepeatOff → Stop at end

    [Fact]
    public void Sequential_RepeatOff_FullNextSequence_StopsAtEnd()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 0);

        // Next → v1
        var s1 = store.Reduce(state, new YtAction.NextRequested());
        Assert.Equal(v1.Id, s1.Queue.CurrentItemId);

        // Next → v2
        var s2 = store.Reduce(s1, new YtAction.NextRequested());
        Assert.Equal(v2.Id, s2.Queue.CurrentItemId);

        // Next → Stop (end of queue)
        var s3 = store.Reduce(s2, new YtAction.NextRequested());
        Assert.IsType<PlayerState.Paused>(s3.Player);
    }

    #endregion

    #region Sequential + RepeatAll → Wraps

    [Fact]
    public void Sequential_RepeatAll_FullNextSequence_Wraps()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 0) with
        {
            Queue = (StateWithCurrentVideo([v0, v1, v2], 0)).Queue with
            {
                RepeatMode = RepeatMode.All
            }
        };

        // Advance to end
        var s1 = store.Reduce(state, new YtAction.NextRequested());
        var s2 = store.Reduce(s1, new YtAction.NextRequested());
        Assert.Equal(v2.Id, s2.Queue.CurrentItemId);

        // Next wraps to first
        var s3 = store.Reduce(s2, new YtAction.NextRequested());
        Assert.Equal(v0.Id, s3.Queue.CurrentItemId);
        Assert.Equal(0, s3.Queue.CurrentIndex);
        Assert.IsType<PlayerState.Loading>(s3.Player);
    }

    #endregion

    #region Shuffle + Next + Prev round-trip

    [Fact]
    public void Shuffle_NextPrev_RoundTrip_RetracesExactPath()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var v3 = MakeVideo(3);
        var videos = new[] { v0, v1, v2, v3 };
        var state = StateWithCurrentVideo(videos, 0);

        // Enable shuffle
        state = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));
        Assert.True(state.Queue.ShuffleEnabled);
        Assert.Equal(v0.Id, state.Queue.ShuffleOrder[0]); // Current at front

        // Collect the shuffle order for verification
        var shuffleOrder = state.Queue.ShuffleOrder;

        // Navigate forward: v0 → shuffle[1] → shuffle[2]
        var s1 = store.Reduce(state, new YtAction.NextRequested());
        Assert.Equal(shuffleOrder[1], s1.Queue.CurrentItemId);

        var s2 = store.Reduce(s1, new YtAction.NextRequested());
        Assert.Equal(shuffleOrder[2], s2.Queue.CurrentItemId);

        // Navigate backward: should retrace exact path
        var s3 = store.Reduce(s2, new YtAction.PrevRequested());
        Assert.Equal(shuffleOrder[1], s3.Queue.CurrentItemId);

        var s4 = store.Reduce(s3, new YtAction.PrevRequested());
        Assert.Equal(shuffleOrder[0], s4.Queue.CurrentItemId);
    }

    #endregion

    #region Shuffle toggle stability

    [Fact]
    public void ShuffleToggle_CurrentItemIdUnchanged()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 1);

        // Enable shuffle — CurrentItemId should remain v1
        var s1 = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));
        Assert.Equal(v1.Id, s1.Queue.CurrentItemId);
        Assert.Equal(1, s1.Queue.CurrentIndex);

        // Disable shuffle — CurrentItemId should remain v1
        var s2 = store.Reduce(s1, new YtAction.ShuffleSet(false));
        Assert.Equal(v1.Id, s2.Queue.CurrentItemId);
        Assert.Equal(1, s2.Queue.CurrentIndex);
    }

    #endregion

    #region Queue mutation during shuffle

    [Fact]
    public void QueueMutation_DuringShuffle_NewVideoInShuffleOrder()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        // Enable shuffle
        state = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));
        var originalOrder = state.Queue.ShuffleOrder;
        Assert.Equal(2, originalOrder.Count);

        // Simulate adding a video via PlaylistLoaded (which reloads the whole playlist)
        // First: add a new video to the videos list directly via SortChanged-like mutation
        var v2 = MakeVideo(2);
        var newVideos = state.Queue.Videos.Add(v2);
        var stateWithNewVideo = state with
        {
            Queue = state.Queue with { Videos = newVideos }
        };

        // RepairPlaybackStructures should detect the new video
        var repaired = PlaybackNavigation.RepairPlaybackStructures(stateWithNewVideo.Queue);
        Assert.Equal(3, repaired.ShuffleOrder.Count);
        Assert.Contains(v2.Id, repaired.ShuffleOrder);
    }

    [Fact]
    public void QueueMutation_DuringShuffle_HistoryIntact()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 0);

        // Enable shuffle and navigate
        state = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));
        state = store.Reduce(state, new YtAction.NextRequested());
        Assert.Single(state.Queue.PlaybackHistory);

        // Add a video (simulated queue mutation)
        var v3 = MakeVideo(3);
        var newVideos = state.Queue.Videos.Add(v3);
        var mutated = state with { Queue = state.Queue with { Videos = newVideos } };
        var repaired = PlaybackNavigation.RepairPlaybackStructures(mutated.Queue);

        // History should be intact
        Assert.Single(repaired.PlaybackHistory);
    }

    #endregion

    #region Remove current video during shuffle

    [Fact]
    public void RemoveCurrentVideo_DuringShuffle_ClearsCurrentItemId()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 1);

        state = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));

        // Remove v1 (current)
        var newVideos = ImmutableList.Create(v0, v2);
        var mutated = state with { Queue = state.Queue with { Videos = newVideos } };
        var repaired = PlaybackNavigation.RepairPlaybackStructures(mutated.Queue);

        Assert.Null(repaired.CurrentItemId);
        Assert.DoesNotContain(v1.Id, repaired.ShuffleOrder);
    }

    #endregion

    #region Deterministic permutation properties

    [Fact]
    public void Shuffle_DeterministicSameSeed_SameOrder()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var v3 = MakeVideo(3);
        var v4 = MakeVideo(4);
        var videos = ImmutableList.Create(v0, v1, v2, v3, v4);

        var order1 = PlaybackNavigation.GenerateShuffleOrder(videos, v0.Id, seed: 12345);
        var order2 = PlaybackNavigation.GenerateShuffleOrder(videos, v0.Id, seed: 12345);

        Assert.Equal(order1, order2);
    }

    [Fact]
    public void Shuffle_OutputIsPermutation()
    {
        var videos = Enumerable.Range(0, 20).Select(MakeVideo).ToImmutableList();
        var order = PlaybackNavigation.GenerateShuffleOrder(videos, videos[5].Id, seed: 999);

        // Same count
        Assert.Equal(videos.Count, order.Count);
        // Same elements
        Assert.Equal(
            videos.Select(v => v.Id).OrderBy(x => x).ToList(),
            order.OrderBy(x => x).ToList());
        // Current at front
        Assert.Equal(videos[5].Id, order[0]);
    }

    [Fact]
    public void Shuffle_DifferentSeeds_DifferentOrders()
    {
        var videos = Enumerable.Range(0, 10).Select(MakeVideo).ToImmutableList();

        var order1 = PlaybackNavigation.GenerateShuffleOrder(videos, null, seed: 1);
        var order2 = PlaybackNavigation.GenerateShuffleOrder(videos, null, seed: 2);

        // With 10 items, the probability of same order with different seeds is negligible
        Assert.NotEqual(order1, order2);
    }

    #endregion

    #region RepeatOne integration

    [Fact]
    public void RepeatOne_NextRepeatsCurrentVideo_Indefinitely()
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

        // Multiple Next presses all repeat current
        for (int i = 0; i < 5; i++)
        {
            state = store.Reduce(state, new YtAction.NextRequested());
            Assert.Equal(v0.Id, state.Queue.CurrentItemId);
            Assert.Equal(0, state.Queue.CurrentIndex);
            Assert.IsType<PlayerState.Loading>(state.Player);
        }
    }

    #endregion

    #region Undo/Redo still works for SelectVideo with shuffle active

    [Fact]
    public void UndoRedo_WithShuffleActive_PreservesState()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var state = StateWithCurrentVideo([v0, v1, v2], 0);

        // Enable shuffle
        state = store.Reduce(state, new YtAction.ShuffleSet(true, Seed: 42));

        // SelectVideo creates undo history
        var s1 = store.Reduce(state, new YtAction.SelectVideo(2, Autoplay: false));
        Assert.Equal(2, s1.Queue.CurrentIndex);
        Assert.Single(s1.Queue.Past);

        // Undo restores previous state
        var s2 = store.Reduce(s1, new YtAction.UndoRequested());
        Assert.Equal(0, s2.Queue.CurrentIndex);
        Assert.Equal(v0.Id, s2.Queue.CurrentItemId);
        Assert.True(s2.Queue.ShuffleEnabled);

        // Redo goes back
        var s3 = store.Reduce(s2, new YtAction.RedoRequested());
        Assert.Equal(2, s3.Queue.CurrentIndex);
        Assert.True(s3.Queue.ShuffleEnabled);
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
