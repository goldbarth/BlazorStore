using System.Collections.Immutable;

namespace ArcFlow.Features.YouTubePlayer.State;

public record YouTubePlayerState(
    PlaylistsState Playlists,
    QueueState Queue,
    PlayerState Player,
    ImmutableList<Notification> Notifications,
    OperationError? LastError = null
)
{
    public YouTubePlayerState() : this(
        Playlists: new PlaylistsState.Loading(),
        Queue: new QueueState(),
        Player: new PlayerState.Empty(),
        Notifications: ImmutableList<Notification>.Empty
        )
    {
    }
}