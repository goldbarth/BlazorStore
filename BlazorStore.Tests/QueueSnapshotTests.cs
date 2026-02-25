using System.Collections.Immutable;
using BlazorStore.Features.YouTubePlayer.Models;
using BlazorStore.Features.YouTubePlayer.State;

namespace BlazorStore.Tests;

public class QueueSnapshotTests
{
    private static VideoItem MakeVideo(int position, string title = "Video") => new()
    {
        Id = Guid.NewGuid(),
        YouTubeId = $"yt_{position}",
        Title = $"{title} {position}",
        Position = position,
        PlaylistId = Guid.NewGuid()
    };

    [Fact]
    public void FromQueueState_CapturesPositions()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);

        var queue = new QueueState
        {
            SelectedPlaylistId = Guid.NewGuid(),
            Videos = ImmutableList.Create(v0, v1, v2),
            CurrentIndex = 1
        };

        var snapshot = QueueSnapshot.FromQueueState(queue);

        Assert.Equal(3, snapshot.VideoPositions.Count);
        Assert.Equal(0, snapshot.VideoPositions[0]);
        Assert.Equal(1, snapshot.VideoPositions[1]);
        Assert.Equal(2, snapshot.VideoPositions[2]);
        Assert.Equal(queue.SelectedPlaylistId, snapshot.SelectedPlaylistId);
        Assert.Equal(1, snapshot.CurrentIndex);
    }

    [Fact]
    public void RoundTrip_RestoresState()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var playlistId = Guid.NewGuid();

        var queue = new QueueState
        {
            SelectedPlaylistId = playlistId,
            Videos = ImmutableList.Create(v0, v1),
            CurrentIndex = 0
        };

        var snapshot = QueueSnapshot.FromQueueState(queue);
        var restored = snapshot.ToQueueState();

        Assert.Equal(playlistId, restored.SelectedPlaylistId);
        Assert.Equal(0, restored.CurrentIndex);
        Assert.Equal(2, restored.Videos.Count);
        Assert.Same(v0, restored.Videos[0]);
        Assert.Same(v1, restored.Videos[1]);
    }

    [Fact]
    public void ToQueueState_RestoresPositions_AfterMutation()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var v2 = MakeVideo(2);

        var queue = new QueueState
        {
            SelectedPlaylistId = Guid.NewGuid(),
            Videos = ImmutableList.Create(v0, v1, v2),
            CurrentIndex = null
        };

        // Snapshot captures positions [0, 1, 2]
        var snapshot = QueueSnapshot.FromQueueState(queue);

        // Simulate HandleSortChanged mutating positions in-place
        v0.Position = 2;
        v1.Position = 0;
        v2.Position = 1;

        // Restore from snapshot — should fix positions back
        var restored = snapshot.ToQueueState();

        Assert.Equal(0, restored.Videos[0].Position);
        Assert.Equal(1, restored.Videos[1].Position);
        Assert.Equal(2, restored.Videos[2].Position);
    }

    [Fact]
    public void FromQueueState_EmptyVideos_ProducesEmptyPositions()
    {
        var queue = new QueueState();
        var snapshot = QueueSnapshot.FromQueueState(queue);

        Assert.Empty(snapshot.Videos);
        Assert.Empty(snapshot.VideoPositions);
        Assert.Null(snapshot.CurrentIndex);
    }

    [Fact]
    public void RoundTrip_PreservesPlaybackFields()
    {
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var playlistId = Guid.NewGuid();
        var currentItemId = v1.Id;
        var shuffleOrder = ImmutableList.Create(v1.Id, v0.Id);
        var playbackHistory = ImmutableList.Create(v0.Id);

        var queue = new QueueState
        {
            SelectedPlaylistId = playlistId,
            Videos = ImmutableList.Create(v0, v1),
            CurrentIndex = 1,
            RepeatMode = RepeatMode.All,
            ShuffleEnabled = true,
            CurrentItemId = currentItemId,
            ShuffleOrder = shuffleOrder,
            PlaybackHistory = playbackHistory,
            ShuffleSeed = 42
        };

        var snapshot = QueueSnapshot.FromQueueState(queue);
        var restored = snapshot.ToQueueState();

        Assert.Equal(RepeatMode.All, restored.RepeatMode);
        Assert.True(restored.ShuffleEnabled);
        Assert.Equal(currentItemId, restored.CurrentItemId);
        Assert.Equal(shuffleOrder, restored.ShuffleOrder);
        Assert.Equal(playbackHistory, restored.PlaybackHistory);
        Assert.Equal(42, restored.ShuffleSeed);
    }
}