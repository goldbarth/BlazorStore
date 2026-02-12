using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.ImportExport;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.State;

/// <summary>
/// Discriminated union of all actions that can be dispatched to the YouTube Player store.
/// Grouped by concern: lifecycle, user commands, interop events, error handling,
/// notifications, playback navigation, and undo/redo.
/// </summary>
public record YtAction
{
    public ActionOrigin Origin { get; init; } = ActionOrigin.User;

    // ── Lifecycle ────────────────────────────────────────────────

    /// <summary>Bootstraps the feature: loads playlists from the database.</summary>
    public sealed record Initialize : YtAction;

    // ── Data loaded (results from side effects) ─────────────────

    /// <summary>All playlists were fetched from the database.</summary>
    public sealed record PlaylistsLoaded(ImmutableList<Playlist> Playlists) : YtAction;

    /// <summary>A single playlist (with its videos) was loaded.</summary>
    public sealed record PlaylistLoaded(Playlist Playlist) : YtAction;

    // ── User commands ───────────────────────────────────────────

    /// <summary>User wants to create a new playlist.</summary>
    public sealed record CreatePlaylist(string Name, string? Description) : YtAction;

    /// <summary>User wants to add a video to a playlist by URL.</summary>
    public sealed record AddVideo(Guid PlaylistId, string Url, string Title) : YtAction;

    /// <summary>User selected a playlist to view/play.</summary>
    public sealed record SelectPlaylist(Guid PlaylistId) : YtAction;

    /// <summary>User clicked a specific video in the queue.</summary>
    public sealed record SelectVideo(int Index, bool Autoplay) : YtAction;

    /// <summary>User reordered a video via drag-and-drop.</summary>
    public sealed record SortChanged(int OldIndex, int NewIndex) : YtAction;

    // ── JS interop events ───────────────────────────────────────

    /// <summary>YouTube IFrame API reported a player state change (playing, paused, buffering, etc.).</summary>
    public sealed record PlayerStateChanged(int YtState, string VideoId) : YtAction;

    /// <summary>The current video finished playing.</summary>
    public sealed record VideoEnded : YtAction;

    // ── Playback navigation (user intent) ───────────────────────

    /// <summary>Toggle shuffle mode on/off, optionally with a specific seed for deterministic ordering.</summary>
    public sealed record ShuffleSet(bool Enabled, int? Seed = null) : YtAction;

    /// <summary>Set the repeat mode (Off, All, One).</summary>
    public sealed record RepeatSet(RepeatMode Mode) : YtAction;

    /// <summary>User pressed "next track".</summary>
    public sealed record NextRequested : YtAction;

    /// <summary>User pressed "previous track".</summary>
    public sealed record PrevRequested : YtAction;

    // ── Playback navigation (computed results) ──────────────────

    /// <summary>Playback was advanced to a new video (result of Next/Prev/AutoAdvance).</summary>
    public sealed record PlaybackAdvanced(Guid ToItemId, AdvanceReason Reason) : YtAction;

    /// <summary>Playback was stopped because the queue ended or was empty.</summary>
    public sealed record PlaybackStopped(StopReason Reason) : YtAction;

    // ── Undo / Redo ─────────────────────────────────────────────

    /// <summary>User triggered undo — restores the previous queue snapshot.</summary>
    public sealed record UndoRequested : YtAction;

    /// <summary>User triggered redo — re-applies a previously undone queue snapshot.</summary>
    public sealed record RedoRequested : YtAction;
    
    // ── Error handling ──────────────────────────────────────────

    /// <summary>A store operation failed — triggers notification and stores the error.</summary>
    public sealed record OperationFailed(OperationError Error) : YtAction;

    // ── Notifications ───────────────────────────────────────────

    /// <summary>Display a notification to the user via snackbar.</summary>
    public sealed record ShowNotification(Notification Notification) : YtAction;

    /// <summary>Remove a notification after the user dismissed it.</summary>
    public sealed record DismissNotification(Guid CorrelationId) : YtAction;

    // ── Import / Export ──────────────────────────────────────────

    /// <summary>User clicked "Export".</summary>
    public sealed record ExportRequested : YtAction;

    /// <summary>DTO serialised, ready for download.</summary>
    public sealed record ExportPrepared(ExportEnvelopeV1 Envelope) : YtAction;

    /// <summary>Browser download triggered successfully.</summary>
    public sealed record ExportSucceeded : YtAction;

    /// <summary>Export failed at some stage.</summary>
    public sealed record ExportFailed(ExportError Error) : YtAction;

    /// <summary>User selected a file for import.</summary>
    public sealed record ImportRequested(string JsonContent) : YtAction;

    /// <summary>JSON parsed into the DTO.</summary>
    public sealed record ImportParsed(ExportEnvelopeV1 Envelope) : YtAction;

    /// <summary>Validation passed.</summary>
    public sealed record ImportValidated(ExportEnvelopeV1 Envelope) : YtAction;

    /// <summary>Playlists converted and ready to replace state.</summary>
    public sealed record ImportApplied(ImmutableList<Playlist> Playlists, Guid? SelectedPlaylistId) : YtAction;

    /// <summary>Import completed — summary counts.</summary>
    public sealed record ImportSucceeded(int PlaylistCount, int VideoCount) : YtAction;

    /// <summary>Import failed at some stage.</summary>
    public sealed record ImportFailed(ImportError Error) : YtAction;

    // ── Persistence ──────────────────────────────────────────────

    /// <summary>Request a DB write.</summary>
    public sealed record PersistRequested : YtAction;

    /// <summary>DB write succeeded.</summary>
    public sealed record PersistSucceeded : YtAction;

    /// <summary>DB write failed.</summary>
    public sealed record PersistFailed(string Message, Exception? Inner = null) : YtAction;
}