using System.Collections.Immutable;
using System.Text.Json;
using ArcFlow.Features.YouTubePlayer.ImportExport;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Tests;

public class ExportPipelineTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();
    private static readonly DateTime FixedCreatedAt = new(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedUpdatedAt = new(2024, 6, 20, 14, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedAddedAt = new(2024, 3, 10, 8, 0, 0, DateTimeKind.Utc);

    private static Playlist MakePlaylist(int videoCount = 3)
    {
        return new Playlist
        {
            Id = PlaylistId,
            Name = "Test Playlist",
            Description = "A test playlist",
            CreatedAt = FixedCreatedAt,
            UpdatedAt = FixedUpdatedAt,
            VideoItems = Enumerable.Range(0, videoCount).Select(i => new VideoItem
            {
                Id = Guid.NewGuid(),
                YouTubeId = $"yt_{i}",
                Title = $"Video {i}",
                ThumbnailUrl = $"https://img.youtube.com/vi/yt_{i}/mqdefault.jpg",
                Duration = TimeSpan.FromMinutes(3 + i),
                Position = i,
                AddedAt = FixedAddedAt.AddDays(i),
                PlaylistId = PlaylistId
            }).ToList()
        };
    }

    #region (a) ExportMapper tests

    [Fact]
    public void ToEnvelope_MapsPlaylistFields()
    {
        var playlist = MakePlaylist(1);
        var playlists = ImmutableList.Create(playlist);

        var envelope = ExportMapper.ToEnvelope(playlists, null);

        var dto = Assert.Single(envelope.Playlists);
        Assert.Equal(playlist.Id, dto.Id);
        Assert.Equal(playlist.Name, dto.Name);
        Assert.Equal(playlist.Description, dto.Description);
        Assert.Equal(playlist.CreatedAt, dto.CreatedAtUtc);
        Assert.Equal(playlist.UpdatedAt, dto.UpdatedAtUtc);
    }

    [Fact]
    public void ToEnvelope_MapsVideoFields()
    {
        var playlist = MakePlaylist(1);
        var video = playlist.VideoItems[0];
        var playlists = ImmutableList.Create(playlist);

        var envelope = ExportMapper.ToEnvelope(playlists, null);

        var videoDto = Assert.Single(envelope.Playlists[0].Videos);
        Assert.Equal(video.Id, videoDto.Id);
        Assert.Equal(video.YouTubeId, videoDto.YouTubeId);
        Assert.Equal(video.Title, videoDto.Title);
        Assert.Equal(video.ThumbnailUrl, videoDto.ThumbnailUrl);
        Assert.Equal(video.Duration, videoDto.Duration);
        Assert.Equal(video.Position, videoDto.Position);
        Assert.Equal(video.AddedAt, videoDto.AddedAtUtc);
    }

    [Fact]
    public void ToEnvelope_SetsSchemaVersionAndTimestamp()
    {
        var playlist = MakePlaylist(0);
        var playlists = ImmutableList.Create(playlist);
        var before = DateTime.UtcNow;

        var envelope = ExportMapper.ToEnvelope(playlists, null);

        Assert.Equal(ExportEnvelopeV1.CurrentSchemaVersion, envelope.SchemaVersion);
        Assert.InRange(envelope.ExportedAtUtc, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void ToEnvelope_SetsSelectedPlaylistId()
    {
        var playlist = MakePlaylist(0);
        var playlists = ImmutableList.Create(playlist);
        var selectedId = playlist.Id;

        var envelope = ExportMapper.ToEnvelope(playlists, selectedId);

        Assert.Equal(selectedId, envelope.SelectedPlaylistId);
    }

    [Fact]
    public void ToEnvelope_OrdersVideosByPosition()
    {
        var playlist = MakePlaylist(3);
        // Reverse the list so positions are out of order in the source
        playlist.VideoItems.Reverse();
        var playlists = ImmutableList.Create(playlist);

        var envelope = ExportMapper.ToEnvelope(playlists, null);

        var positions = envelope.Playlists[0].Videos.Select(v => v.Position).ToList();
        Assert.Equal([0, 1, 2], positions);
    }

    #endregion

    #region (b) Serialization tests

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var playlist = MakePlaylist(2);
        var envelope = ExportMapper.ToEnvelope(ImmutableList.Create(playlist), null);

        var json = ExportSerializer.Serialize(envelope);

        // Should parse without throwing
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Serialize_UsesCamelCase()
    {
        var playlist = MakePlaylist(1);
        var envelope = ExportMapper.ToEnvelope(ImmutableList.Create(playlist), playlist.Id);

        var json = ExportSerializer.Serialize(envelope);

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"exportedAtUtc\"", json);
        Assert.Contains("\"selectedPlaylistId\"", json);
        Assert.Contains("\"createdAtUtc\"", json);
        Assert.Contains("\"youTubeId\"", json);
        Assert.DoesNotContain("\"SchemaVersion\"", json);
        Assert.DoesNotContain("\"ExportedAtUtc\"", json);
    }

    [Fact]
    public void Serialize_RoundTrip()
    {
        var playlist = MakePlaylist(2);
        var original = ExportMapper.ToEnvelope(ImmutableList.Create(playlist), playlist.Id);

        var json = ExportSerializer.Serialize(original);
        var deserialized = ExportSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(original.SelectedPlaylistId, deserialized.SelectedPlaylistId);
        Assert.Equal(original.Playlists.Count, deserialized.Playlists.Count);

        var origPlaylist = original.Playlists[0];
        var deserPlaylist = deserialized.Playlists[0];
        Assert.Equal(origPlaylist.Id, deserPlaylist.Id);
        Assert.Equal(origPlaylist.Name, deserPlaylist.Name);
        Assert.Equal(origPlaylist.Videos.Count, deserPlaylist.Videos.Count);

        for (int i = 0; i < origPlaylist.Videos.Count; i++)
        {
            Assert.Equal(origPlaylist.Videos[i].Id, deserPlaylist.Videos[i].Id);
            Assert.Equal(origPlaylist.Videos[i].YouTubeId, deserPlaylist.Videos[i].YouTubeId);
            Assert.Equal(origPlaylist.Videos[i].Title, deserPlaylist.Videos[i].Title);
            Assert.Equal(origPlaylist.Videos[i].Position, deserPlaylist.Videos[i].Position);
        }
    }

    #endregion
}
