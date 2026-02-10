namespace ArcFlow.Features.YouTubePlayer.State;

public record YouTubePlayerState(
    PlaylistsState Playlists,
    QueueState Queue,
    PlayerState Player
    );