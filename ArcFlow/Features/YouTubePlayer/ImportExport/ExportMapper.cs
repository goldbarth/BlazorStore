using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.ImportExport;

public static class ExportMapper
{
    public static ExportEnvelopeV1 ToEnvelope(ImmutableList<Playlist> playlists, Guid? selectedPlaylistId)
    {
        var dtos = playlists.Select(MapPlaylist).ToList();
        return ExportEnvelopeV1.Create(dtos, selectedPlaylistId);
    }

    private static ExportPlaylistDto MapPlaylist(Playlist playlist)
    {
        var videos = playlist.VideoItems
            .OrderBy(v => v.Position)
            .Select(MapVideo)
            .ToList();

        return new ExportPlaylistDto(
            Id: playlist.Id,
            Name: playlist.Name,
            Description: playlist.Description,
            CreatedAtUtc: playlist.CreatedAt,
            UpdatedAtUtc: playlist.UpdatedAt,
            Videos: videos
        );
    }

    private static ExportVideoDto MapVideo(VideoItem video)
    {
        return new ExportVideoDto(
            Id: video.Id,
            YouTubeId: video.YouTubeId,
            Title: video.Title,
            ThumbnailUrl: video.ThumbnailUrl,
            Duration: video.Duration,
            Position: video.Position,
            AddedAtUtc: video.AddedAt
        );
    }
}
