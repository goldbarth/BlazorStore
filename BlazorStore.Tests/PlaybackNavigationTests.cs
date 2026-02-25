using System.Collections.Immutable;
using BlazorStore.Features.YouTubePlayer.Models;
using BlazorStore.Features.YouTubePlayer.State;

namespace BlazorStore.Tests;

public class PlaybackNavigationTests
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

    private static QueueState MakeQueue(params VideoItem[] videos)
    {
        var list = videos.ToImmutableList();
        return new QueueState
        {
            SelectedPlaylistId = PlaylistId,
            Videos = list,
            CurrentIndex = videos.Length > 0 ? 0 : null,
            CurrentItemId = videos.Length > 0 ? videos[0].Id : null
        };
    }

    #region GenerateShuffleOrder

    [Fact]
    public void GenerateShuffleOrder_SameSeed_SameResult()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var videos = ImmutableList.Create(v0, v1, v2);

        var order1 = PlaybackNavigation.GenerateShuffleOrder(videos, null, seed: 42);
        var order2 = PlaybackNavigation.GenerateShuffleOrder(videos, null, seed: 42);

        Assert.Equal(order1, order2);
    }

    [Fact]
    public void GenerateShuffleOrder_IsPermutation()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var v3 = MakeVideo(3);
        var videos = ImmutableList.Create(v0, v1, v2, v3);

        var order = PlaybackNavigation.GenerateShuffleOrder(videos, null, seed: 123);

        Assert.Equal(videos.Count, order.Count);
        Assert.Equal(
            videos.Select(v => v.Id).OrderBy(id => id).ToList(),
            order.OrderBy(id => id).ToList());
    }

    [Fact]
    public void GenerateShuffleOrder_CurrentItemAtFront()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var videos = ImmutableList.Create(v0, v1, v2);

        var order = PlaybackNavigation.GenerateShuffleOrder(videos, v2.Id, seed: 42);

        Assert.Equal(v2.Id, order[0]);
        Assert.Equal(3, order.Count);
    }

    [Fact]
    public void GenerateShuffleOrder_EmptyVideos_ReturnsEmpty()
    {
        var order = PlaybackNavigation.GenerateShuffleOrder(
            ImmutableList<VideoItem>.Empty, null, seed: 0);

        Assert.Empty(order);
    }

    [Fact]
    public void GenerateShuffleOrder_SingleVideo()
    {
        var v0 = MakeVideo(0);
        var videos = ImmutableList.Create(v0);

        var order = PlaybackNavigation.GenerateShuffleOrder(videos, v0.Id, seed: 1);

        Assert.Single(order);
        Assert.Equal(v0.Id, order[0]);
    }

    #endregion

    #region ComputeNext — Sequential

    [Fact]
    public void ComputeNext_Sequential_RepeatOff_AdvancesToNext()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1);

        var (decision, newQueue) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v1.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
    }

    [Fact]
    public void ComputeNext_Sequential_RepeatOff_AtEnd_Stops()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1) with
        {
            CurrentIndex = 1,
            CurrentItemId = v1.Id
        };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.Stop>(decision);
    }

    [Fact]
    public void ComputeNext_Sequential_RepeatAll_AtEnd_Wraps()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1) with
        {
            CurrentIndex = 1,
            CurrentItemId = v1.Id,
            RepeatMode = RepeatMode.All
        };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v0.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
    }

    [Fact]
    public void ComputeNext_RepeatOne_ReplaysCurrentVideo()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1) with { RepeatMode = RepeatMode.One };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v0.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
    }

    [Fact]
    public void ComputeNext_EmptyQueue_Stops()
    {
        var queue = new QueueState();

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.Stop>(decision);
    }

    [Fact]
    public void ComputeNext_NoCurrentItem_Stops()
    {
        var v0 = MakeVideo(0);
        var queue = MakeQueue(v0) with { CurrentItemId = null };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.Stop>(decision);
    }

    [Fact]
    public void ComputeNext_PushesCurrentToPlaybackHistory()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1);

        var (_, newQueue) = PlaybackNavigation.ComputeNext(queue);

        Assert.Single(newQueue.PlaybackHistory);
        Assert.Equal(v0.Id, newQueue.PlaybackHistory[0]);
    }

    [Fact]
    public void ComputeNext_RepeatOne_DoesNotPushHistory()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1) with { RepeatMode = RepeatMode.One };

        var (_, newQueue) = PlaybackNavigation.ComputeNext(queue);

        Assert.Empty(newQueue.PlaybackHistory);
    }

    #endregion

    #region ComputeNext — Shuffle

    [Fact]
    public void ComputeNext_Shuffle_AdvancesToNextInShuffleOrder()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);
        var shuffleOrder = ImmutableList.Create(v1.Id, v2.Id, v0.Id);

        var queue = MakeQueue(v0, v1, v2) with
        {
            CurrentItemId = v1.Id,
            ShuffleEnabled = true,
            ShuffleOrder = shuffleOrder
        };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v2.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
    }

    [Fact]
    public void ComputeNext_Shuffle_RepeatAll_AtEnd_Wraps()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var shuffleOrder = ImmutableList.Create(v0.Id, v1.Id);

        var queue = MakeQueue(v0, v1) with
        {
            CurrentItemId = v1.Id,
            ShuffleEnabled = true,
            ShuffleOrder = shuffleOrder,
            RepeatMode = RepeatMode.All
        };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v0.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
    }

    [Fact]
    public void ComputeNext_Shuffle_RepeatOff_AtEnd_Stops()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var shuffleOrder = ImmutableList.Create(v0.Id, v1.Id);

        var queue = MakeQueue(v0, v1) with
        {
            CurrentItemId = v1.Id,
            ShuffleEnabled = true,
            ShuffleOrder = shuffleOrder
        };

        var (decision, _) = PlaybackNavigation.ComputeNext(queue);

        Assert.IsType<PlaybackDecision.Stop>(decision);
    }

    #endregion

    #region ComputePrev — Sequential

    [Fact]
    public void ComputePrev_Sequential_AtStart_IsNoOp()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1);

        var (decision, _) = PlaybackNavigation.ComputePrev(queue);

        Assert.IsType<PlaybackDecision.NoOp>(decision);
    }

    [Fact]
    public void ComputePrev_Sequential_GoesToPreviousIndex()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1) with
        {
            CurrentIndex = 1,
            CurrentItemId = v1.Id
        };

        var (decision, _) = PlaybackNavigation.ComputePrev(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v0.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
    }

    [Fact]
    public void ComputePrev_EmptyQueue_IsNoOp()
    {
        var queue = new QueueState();

        var (decision, _) = PlaybackNavigation.ComputePrev(queue);

        Assert.IsType<PlaybackDecision.NoOp>(decision);
    }

    #endregion

    #region ComputePrev — Shuffle

    [Fact]
    public void ComputePrev_Shuffle_PopsFromHistory()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var queue = MakeQueue(v0, v1) with
        {
            CurrentItemId = v1.Id,
            ShuffleEnabled = true,
            PlaybackHistory = ImmutableList.Create(v0.Id)
        };

        var (decision, newQueue) = PlaybackNavigation.ComputePrev(queue);

        Assert.IsType<PlaybackDecision.AdvanceTo>(decision);
        Assert.Equal(v0.Id, ((PlaybackDecision.AdvanceTo)decision).ItemId);
        Assert.Empty(newQueue.PlaybackHistory);
    }

    [Fact]
    public void ComputePrev_Shuffle_EmptyHistory_IsNoOp()
    {
        var v0 = MakeVideo(0);
        var queue = MakeQueue(v0) with { ShuffleEnabled = true };

        var (decision, _) = PlaybackNavigation.ComputePrev(queue);

        Assert.IsType<PlaybackDecision.NoOp>(decision);
    }

    #endregion

    #region RepairPlaybackStructures

    [Fact]
    public void Repair_FiltersRemovedVideosFromShuffleOrder()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var removedId = Guid.NewGuid();

        var queue = new QueueState
        {
            Videos = ImmutableList.Create(v0, v1),
            ShuffleEnabled = true,
            ShuffleOrder = ImmutableList.Create(removedId, v0.Id, v1.Id)
        };

        var repaired = PlaybackNavigation.RepairPlaybackStructures(queue);

        Assert.Equal(2, repaired.ShuffleOrder.Count);
        Assert.DoesNotContain(removedId, repaired.ShuffleOrder);
    }

    [Fact]
    public void Repair_AppendsNewVideosToShuffleOrder()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);

        var queue = new QueueState
        {
            Videos = ImmutableList.Create(v0, v1, v2),
            ShuffleEnabled = true,
            ShuffleOrder = ImmutableList.Create(v0.Id, v1.Id)
        };

        var repaired = PlaybackNavigation.RepairPlaybackStructures(queue);

        Assert.Equal(3, repaired.ShuffleOrder.Count);
        Assert.Contains(v2.Id, repaired.ShuffleOrder);
        // Original order preserved for existing items
        Assert.Equal(v0.Id, repaired.ShuffleOrder[0]);
        Assert.Equal(v1.Id, repaired.ShuffleOrder[1]);
    }

    [Fact]
    public void Repair_FiltersRemovedFromPlaybackHistory()
    {
        var v0 = MakeVideo(0);
        var removedId = Guid.NewGuid();

        var queue = new QueueState
        {
            Videos = ImmutableList.Create(v0),
            PlaybackHistory = ImmutableList.Create(removedId, v0.Id)
        };

        var repaired = PlaybackNavigation.RepairPlaybackStructures(queue);

        Assert.Single(repaired.PlaybackHistory);
        Assert.Equal(v0.Id, repaired.PlaybackHistory[0]);
    }

    [Fact]
    public void Repair_ClearsCurrentItemId_IfRemoved()
    {
        var v0 = MakeVideo(0);
        var removedId = Guid.NewGuid();

        var queue = new QueueState
        {
            Videos = ImmutableList.Create(v0),
            CurrentItemId = removedId
        };

        var repaired = PlaybackNavigation.RepairPlaybackStructures(queue);

        Assert.Null(repaired.CurrentItemId);
    }

    [Fact]
    public void Repair_SyncsCurrentIndex()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);

        var queue = new QueueState
        {
            Videos = ImmutableList.Create(v0, v1),
            CurrentItemId = v1.Id,
            CurrentIndex = 0 // Wrong index
        };

        var repaired = PlaybackNavigation.RepairPlaybackStructures(queue);

        Assert.Equal(1, repaired.CurrentIndex);
    }

    [Fact]
    public void Repair_IsIdempotent()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);

        var queue = new QueueState
        {
            Videos = ImmutableList.Create(v0, v1),
            ShuffleEnabled = true,
            ShuffleOrder = ImmutableList.Create(v1.Id, v0.Id),
            PlaybackHistory = ImmutableList.Create(v0.Id),
            CurrentItemId = v1.Id,
            CurrentIndex = 1
        };

        var first = PlaybackNavigation.RepairPlaybackStructures(queue);
        var second = PlaybackNavigation.RepairPlaybackStructures(first);

        Assert.Equal(first.ShuffleOrder, second.ShuffleOrder);
        Assert.Equal(first.PlaybackHistory, second.PlaybackHistory);
        Assert.Equal(first.CurrentItemId, second.CurrentItemId);
        Assert.Equal(first.CurrentIndex, second.CurrentIndex);
    }

    [Fact]
    public void Repair_TrimsPlaybackHistoryToLimit()
    {
        var videos = Enumerable.Range(0, 5).Select(MakeVideo).ToArray();
        var history = Enumerable.Range(0, QueueState.PlaybackHistoryLimit + 10)
            .Select(_ => videos[0].Id)
            .ToImmutableList();

        var queue = new QueueState
        {
            Videos = videos.ToImmutableList(),
            PlaybackHistory = history
        };

        var repaired = PlaybackNavigation.RepairPlaybackStructures(queue);

        Assert.Equal(QueueState.PlaybackHistoryLimit, repaired.PlaybackHistory.Count);
    }

    #endregion

    #region PlaybackHistory limit in ComputeNext

    [Fact]
    public void ComputeNext_TrimsPlaybackHistoryAtLimit()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var history = Enumerable.Range(0, QueueState.PlaybackHistoryLimit)
            .Select(_ => Guid.NewGuid())
            .ToImmutableList();

        var queue = MakeQueue(v0, v1) with { PlaybackHistory = history };

        var (_, newQueue) = PlaybackNavigation.ComputeNext(queue);

        Assert.Equal(QueueState.PlaybackHistoryLimit, newQueue.PlaybackHistory.Count);
        // Oldest entry removed, new entry added
        Assert.Equal(v0.Id, newQueue.PlaybackHistory[^1]);
    }

    #endregion
}
