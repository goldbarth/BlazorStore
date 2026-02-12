namespace ArcFlow.Features.YouTubePlayer.State;

public record PersistenceState(
    bool IsDirty = false,
    DateTime? LastPersistAttemptUtc = null,
    string? LastPersistError = null
);
