using System.Collections.Immutable;

namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Root state record for the YouTube Player feature. Composes all sub-states
/// (playlists, queue, player, notifications) into a single immutable tree
/// that the store exposes to the UI.
/// </summary>
public record YouTubePlayerState(
    PlaylistsState Playlists,
    QueueState Queue,
    PlayerState Player,
    ImmutableList<Notification> Notifications,
    ImportExportState ImportExport,
    PersistenceState Persistence,
    OperationError? LastError = null
)
{
    public YouTubePlayerState() : this(
        Playlists: new PlaylistsState.Loading(),
        Queue: new QueueState(),
        Player: new PlayerState.Empty(),
        Notifications: ImmutableList<Notification>.Empty,
        ImportExport: new ImportExportState.Idle(),
        Persistence: new PersistenceState()
        )
    {
    }
}