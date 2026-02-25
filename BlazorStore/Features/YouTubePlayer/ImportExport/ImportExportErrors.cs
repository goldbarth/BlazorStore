namespace BlazorStore.Features.YouTubePlayer.ImportExport;

/// <summary>
/// Discriminated union of all errors that can occur during import.
/// </summary>
public abstract record ImportError
{
    private ImportError() { }

    /// <summary>The file could not be parsed as valid JSON or deserialized into the DTO.</summary>
    public sealed record ParseError(string Message, Exception? Inner = null) : ImportError;

    /// <summary>The <c>schemaVersion</c> in the file is not supported by this version of BlazorStore.</summary>
    public sealed record UnsupportedSchema(int Found, int MaxSupported) : ImportError;

    /// <summary>A required field is missing, empty, or violates a constraint (e.g. empty playlist name).</summary>
    public sealed record ValidationError(string Field, string Message) : ImportError;

    /// <summary>An imported ID already exists in the target database after mode-specific cleanup.</summary>
    public sealed record IdCollision(Guid CollidingId, string EntityType) : ImportError;

    /// <summary>All validation passed but persisting to the database failed.</summary>
    public sealed record PersistenceFailed(string Message, Exception? Inner = null) : ImportError;
}

/// <summary>
/// Discriminated union of all errors that can occur during export.
/// </summary>
public abstract record ExportError
{
    private ExportError() { }

    /// <summary>Serializing the state to JSON failed.</summary>
    public sealed record SerializationFailed(string Message, Exception? Inner = null) : ExportError;

    /// <summary>Triggering the browser download via JS interop failed.</summary>
    public sealed record InteropFailed(string Message, Exception? Inner = null) : ExportError;
}
