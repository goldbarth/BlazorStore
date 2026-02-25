namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Defines which actions create undo snapshots, which reset the undo stack (boundary actions),
/// and which are transient playback events that should never be undone.
/// </summary>
public static class UndoPolicy
{
    public static bool IsUndoable(YtAction action) => action is
        YtAction.SelectVideo or YtAction.SortChanged;

    public static bool IsBoundary(YtAction action) => action is
        YtAction.PlaylistLoaded or YtAction.SelectPlaylist or YtAction.ImportApplied;

    public static bool IsPlaybackTransient(YtAction action) => action is
        YtAction.NextRequested or YtAction.PrevRequested
        or YtAction.PlaybackAdvanced or YtAction.PlaybackStopped
        or YtAction.ShuffleSet or YtAction.RepeatSet;
}
