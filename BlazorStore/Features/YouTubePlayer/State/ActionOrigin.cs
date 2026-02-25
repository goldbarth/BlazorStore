namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Identifies who or what triggered an action, used to distinguish user interactions
/// from system-generated events and undo/redo operations.
/// </summary>
public enum ActionOrigin { User, System, TimeTravel }
