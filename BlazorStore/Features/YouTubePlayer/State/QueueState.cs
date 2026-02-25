using System.Collections.Immutable;
using BlazorStore.Features.YouTubePlayer.Models;

namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Central state for the video queue: selected playlist, video list, current playback position,
/// shuffle/repeat settings, and undo/redo history stacks.
/// </summary>
public record QueueState
{
    public const int HistoryLimit = 30;
    public const int PlaybackHistoryLimit = 100;

    public Guid? SelectedPlaylistId { get; init; }
    public ImmutableList<VideoItem> Videos { get; init; } = ImmutableList<VideoItem>.Empty;
    public int? CurrentIndex { get; init; }

    public ImmutableList<QueueSnapshot> Past { get; init; } = ImmutableList<QueueSnapshot>.Empty;
    public ImmutableList<QueueSnapshot> Future { get; init; } = ImmutableList<QueueSnapshot>.Empty;

    // Playback navigation
    public RepeatMode RepeatMode { get; init; } = RepeatMode.Off;
    public bool ShuffleEnabled { get; init; } = false;
    public Guid? CurrentItemId { get; init; } = null;
    public ImmutableList<Guid> ShuffleOrder { get; init; } = ImmutableList<Guid>.Empty;
    public ImmutableList<Guid> PlaybackHistory { get; init; } = ImmutableList<Guid>.Empty;
    public int ShuffleSeed { get; init; } = 0;

    public bool CanUndo => !Past.IsEmpty;
    public bool CanRedo => !Future.IsEmpty;

    public bool HasSelection => SelectedPlaylistId is not null;
    public bool HasVideo  => CurrentIndex is not null;

    /// <summary>
    /// Validates the current state of the queue by ensuring the current index is within the bounds
    /// of the video list and is only set when there are videos available.
    /// Also clears CurrentItemId if it references a removed video.
    /// </summary>
    public QueueState Validate()
    {
        var result = this;

        if (CurrentIndex.HasValue && CurrentIndex.Value >= Videos.Count
            || Videos.Count == 0 && CurrentIndex.HasValue)
        {
            result = result with { CurrentIndex = null };
        }

        if (CurrentItemId.HasValue && !Videos.Any(v => v.Id == CurrentItemId.Value))
        {
            result = result with { CurrentItemId = null };
        }

        return result;
    }
}
