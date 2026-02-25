namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Why playback advanced to a different video — distinguishes user-initiated skips from auto-advance after a video ends.
/// </summary>
public enum AdvanceReason { Next, Previous, AutoAdvance }

/// <summary>
/// Why playback was stopped — either the queue ran out of tracks or was empty to begin with.
/// </summary>
public enum StopReason { EndOfQueue, EmptyQueue }
