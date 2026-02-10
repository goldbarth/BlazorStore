namespace ArcFlow.Features.YouTubePlayer.State;

/// <summary>
/// Categorized errors for store operations
/// </summary>
public record OperationError(
    ErrorCategory Category,
    string Message,
    OperationContext Context,
    Exception? InnerException = null
);

/// <summary>
/// Context for every operation - enables debugging and correlation
/// </summary>
public record OperationContext(
    string Operation,
    Guid CorrelationId,
    Guid? PlaylistId = null,
    Guid? VideoId = null,
    int? Index = null
)
{
    public static OperationContext Create(string operation, Guid? playlistId = null, Guid? videoId = null,
        int? index = null)
        => new(operation, Guid.NewGuid(), playlistId, videoId, index);
}

/// <summary>
/// Error categories matching the pattern from your requirements
/// </summary>
public enum ErrorCategory
{
    /// <summary>User input validation failed</summary>
    Validation,

    /// <summary>Resource not found or conflict in domain state</summary>
    NotFound,

    /// <summary>Network/timeout errors - potentially retryable</summary>
    Transient,

    /// <summary>JS-Interop or external API errors</summary>
    External,

    /// <summary>Unexpected bugs/unhandled exceptions</summary>
    Unexpected
}