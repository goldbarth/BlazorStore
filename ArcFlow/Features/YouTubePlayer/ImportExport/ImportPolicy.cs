namespace ArcFlow.Features.YouTubePlayer.ImportExport;

/// <summary>
/// Determines how imported data relates to existing data.
/// <list type="bullet">
///   <item><see cref="ReplaceAll"/> — Deletes all existing playlists and replaces them with the imported data.</item>
/// </list>
/// </summary>
public enum ImportMode
{
    ReplaceAll
}

/// <summary>
/// Determines how IDs in the import file are handled.
/// <list type="bullet">
///   <item><see cref="TrustIncoming"/> — Uses the IDs from the import file as-is.
///   A collision check is performed before persisting; if any imported ID already
///   exists in the database (after the mode-specific cleanup), the import fails
///   with <see cref="ImportError.IdCollision"/>.</item>
/// </list>
/// </summary>
public enum IdStrategy
{
    TrustIncoming
}

/// <summary>
/// Bundles the policy knobs that control import behaviour.
/// </summary>
public sealed record ImportOptions(
    ImportMode Mode = ImportMode.ReplaceAll,
    IdStrategy IdStrategy = IdStrategy.TrustIncoming
)
{
    public static ImportOptions Default => new();
}
