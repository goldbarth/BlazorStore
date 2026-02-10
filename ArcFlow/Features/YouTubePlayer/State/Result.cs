namespace ArcFlow.Features.YouTubePlayer.State;

// <summary>
/// Result pattern for store operations - avoids exceptions for expected failure cases
/// </summary>
public abstract record Result
{
    public record Success : Result;
    public record Failure(OperationError Error) : Result;
    
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;
}

public abstract record Result<T>
{
    public record Success(T Value) : Result<T>;
    public record Failure(OperationError Error) : Result<T>;
    
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;
}