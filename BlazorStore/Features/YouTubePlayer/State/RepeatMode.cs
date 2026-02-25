namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Controls what happens when the end of the queue is reached:
/// Off = stop, All = restart from beginning, One = loop current video.
/// </summary>
public enum RepeatMode { Off, All, One }
