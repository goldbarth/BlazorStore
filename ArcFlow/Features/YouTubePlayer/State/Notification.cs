namespace ArcFlow.Features.YouTubePlayer.State;

/// <summary>
/// UI notification model for user feedback
/// </summary>
public record Notification(
    NotificationSeverity Severity,
    string Message,
    Guid CorrelationId,
    DateTime Timestamp,
    bool IsDismissible = true
)
{
    public static Notification FromError(OperationError error, string userMessage)
        => new(
            Severity: error.Category.ToSeverity(),
            Message: userMessage,
            CorrelationId: error.Context.CorrelationId,
            Timestamp: DateTime.UtcNow
        );
}

/// <summary>
/// Notification severity for UI feedback
/// </summary>
public enum NotificationSeverity
{
    Success,
    Info,
    Warning,
    Error
}

public static class ErrorCategoryExtensions
{
    public static NotificationSeverity ToSeverity(this ErrorCategory category)
        => category switch
        {
            ErrorCategory.Validation => NotificationSeverity.Warning,
            ErrorCategory.NotFound => NotificationSeverity.Warning,
            ErrorCategory.Transient => NotificationSeverity.Warning,
            ErrorCategory.External => NotificationSeverity.Error,
            ErrorCategory.Unexpected => NotificationSeverity.Error,
            _ => NotificationSeverity.Info
        };
}