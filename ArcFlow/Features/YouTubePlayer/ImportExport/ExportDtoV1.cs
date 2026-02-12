namespace ArcFlow.Features.YouTubePlayer.ImportExport;

/// <summary>
/// Schema version 1 envelope for playlist import/export.
/// All fields except <see cref="SelectedPlaylistId"/> are required.
/// </summary>
public sealed record ExportEnvelopeV1(
    int SchemaVersion,
    DateTime ExportedAtUtc,
    IReadOnlyList<ExportPlaylistDto> Playlists,
    Guid? SelectedPlaylistId
)
{
    public const int CurrentSchemaVersion = 1;

    public static ExportEnvelopeV1 Create(
        IReadOnlyList<ExportPlaylistDto> playlists,
        Guid? selectedPlaylistId = null)
        => new(
            SchemaVersion: CurrentSchemaVersion,
            ExportedAtUtc: DateTime.UtcNow,
            Playlists: playlists,
            SelectedPlaylistId: selectedPlaylistId
        );
}

/// <summary>
/// Exported representation of a playlist.
/// <see cref="Id"/>, <see cref="Name"/> and <see cref="Videos"/> are required.
/// </summary>
public sealed record ExportPlaylistDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ExportVideoDto> Videos
);

/// <summary>
/// Exported representation of a video item.
/// <see cref="Id"/>, <see cref="YouTubeId"/> and <see cref="Title"/> are required.
/// <see cref="ThumbnailUrl"/> and <see cref="Duration"/> are optional.
/// </summary>
public sealed record ExportVideoDto(
    Guid Id,
    string YouTubeId,
    string Title,
    string? ThumbnailUrl,
    TimeSpan? Duration,
    int Position,
    DateTime AddedAtUtc
);
