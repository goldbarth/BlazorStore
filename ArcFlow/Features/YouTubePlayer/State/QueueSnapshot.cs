using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.State;

public sealed record QueueSnapshot(
    Guid? SelectedPlaylistId,
    ImmutableList<VideoItem> Videos,
    ImmutableList<int> VideoPositions,
    int? CurrentIndex,
    RepeatMode RepeatMode,
    bool ShuffleEnabled,
    Guid? CurrentItemId,
    ImmutableList<Guid> ShuffleOrder,
    ImmutableList<Guid> PlaybackHistory,
    int ShuffleSeed
)
{
    public static QueueSnapshot FromQueueState(QueueState queue)
    {
        var positions = queue.Videos.Select(v => v.Position).ToImmutableList();
        return new QueueSnapshot(
            queue.SelectedPlaylistId,
            queue.Videos,
            positions,
            queue.CurrentIndex,
            queue.RepeatMode,
            queue.ShuffleEnabled,
            queue.CurrentItemId,
            queue.ShuffleOrder,
            queue.PlaybackHistory,
            queue.ShuffleSeed
        );
    }

    public QueueState ToQueueState()
    {
        // Restore positions on VideoItem instances whose Position has drifted
        for (int i = 0; i < Videos.Count && i < VideoPositions.Count; i++)
        {
            Videos[i].Position = VideoPositions[i];
        }

        return new QueueState
        {
            SelectedPlaylistId = SelectedPlaylistId,
            Videos = Videos,
            CurrentIndex = CurrentIndex,
            RepeatMode = RepeatMode,
            ShuffleEnabled = ShuffleEnabled,
            CurrentItemId = CurrentItemId,
            ShuffleOrder = ShuffleOrder,
            PlaybackHistory = PlaybackHistory,
            ShuffleSeed = ShuffleSeed
        };
    }
}
