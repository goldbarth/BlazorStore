namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Railway-oriented result type for store operations — encodes success/failure
/// without throwing exceptions for expected error cases.
/// </summary>
public abstract record Result
{
    public record Success : Result;
    public record Failure(OperationError Error) : Result;

    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;
}

/// <summary>
/// Generic variant of <see cref="Result"/> that carries a typed value on success.
/// </summary>
public abstract record Result<T>
{
    public record Success(T Value) : Result<T>;
    public record Failure(OperationError Error) : Result<T>;

    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;
}