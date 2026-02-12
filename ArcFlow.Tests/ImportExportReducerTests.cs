using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.ImportExport;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace ArcFlow.Tests;

public class ImportExportReducerTests
{
    private static readonly Guid PlaylistId = Guid.NewGuid();

    private static YouTubePlayerStore CreateStore()
    {
        var playlistService = new FakePlaylistService();
        var jsRuntime = new FakeJSRuntime();
        var logger = NullLogger<YouTubePlayerStore>.Instance;
        return new YouTubePlayerStore(playlistService, jsRuntime, logger);
    }

    private static VideoItem MakeVideo(int position) => new()
    {
        Id = Guid.NewGuid(),
        YouTubeId = $"yt_{position}",
        Title = $"Video {position}",
        Position = position,
        PlaylistId = PlaylistId
    };

    private static YouTubePlayerState StateWithVideos(params VideoItem[] videos)
    {
        return new YouTubePlayerState
        {
            Queue = new QueueState
            {
                SelectedPlaylistId = PlaylistId,
                Videos = videos.ToImmutableList(),
                CurrentIndex = null
            }
        };
    }

    private static YouTubePlayerState StateWithCurrentVideo(VideoItem[] videos, int currentIdx)
    {
        return new YouTubePlayerState
        {
            Queue = new QueueState
            {
                SelectedPlaylistId = PlaylistId,
                Videos = videos.ToImmutableList(),
                CurrentIndex = currentIdx,
                CurrentItemId = videos[currentIdx].Id
            },
            Player = new PlayerState.Playing(videos[currentIdx].YouTubeId)
        };
    }

    private static ExportEnvelopeV1 MakeEnvelope(int playlistCount = 1, int videosPerPlaylist = 2)
    {
        var playlists = Enumerable.Range(0, playlistCount).Select(p =>
            new ExportPlaylistDto(
                Guid.NewGuid(),
                $"Playlist {p}",
                "",
                DateTime.UtcNow,
                DateTime.UtcNow,
                Enumerable.Range(0, videosPerPlaylist).Select(v =>
                    new ExportVideoDto(Guid.NewGuid(), $"yt_{p}_{v}", $"Video {p}-{v}", null, null, v, DateTime.UtcNow)
                ).ToList()
            )
        ).ToList();

        return ExportEnvelopeV1.Create(playlists);
    }

    private static ImmutableList<Playlist> MakeImportedPlaylists(int count = 2, int videosEach = 3)
    {
        return Enumerable.Range(0, count).Select(p =>
        {
            var pl = new Playlist
            {
                Id = Guid.NewGuid(),
                Name = $"Imported {p}",
                VideoItems = Enumerable.Range(0, videosEach).Select(v => new VideoItem
                {
                    Id = Guid.NewGuid(),
                    YouTubeId = $"imp_yt_{p}_{v}",
                    Title = $"Imp Video {p}-{v}",
                    Position = v
                }).ToList()
            };
            return pl;
        }).ToImmutableList();
    }

    #region (a) ReplaceAll deterministic

    [Fact]
    public void ImportApplied_ReplacesPlaylistsState()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var state = StateWithCurrentVideo([v0], 0);
        var imported = MakeImportedPlaylists();

        var result = store.Reduce(state, new YtAction.ImportApplied(imported, imported[0].Id));

        Assert.IsType<PlaylistsState.Loaded>(result.Playlists);
        var loaded = (PlaylistsState.Loaded)result.Playlists;
        Assert.Equal(imported.Count, loaded.Items.Count);
        Assert.Equal(imported[0].Id, loaded.Items[0].Id);
    }

    [Fact]
    public void ImportApplied_ResetsQueueToEmpty()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 1);
        var imported = MakeImportedPlaylists();

        var result = store.Reduce(state, new YtAction.ImportApplied(imported, null));

        Assert.Empty(result.Queue.Videos);
        Assert.Null(result.Queue.CurrentIndex);
    }

    [Fact]
    public void ImportApplied_ResetsPlayerToEmpty()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var state = StateWithCurrentVideo([v0], 0);
        var imported = MakeImportedPlaylists();

        var result = store.Reduce(state, new YtAction.ImportApplied(imported, null));

        Assert.IsType<PlayerState.Empty>(result.Player);
    }

    [Fact]
    public void ImportApplied_SetsSelectedPlaylistId()
    {
        var store = CreateStore();
        var state = StateWithVideos(MakeVideo(0));
        var imported = MakeImportedPlaylists();
        var targetId = imported[1].Id;

        var result = store.Reduce(state, new YtAction.ImportApplied(imported, targetId));

        Assert.Equal(targetId, result.Queue.SelectedPlaylistId);
    }

    #endregion

    #region (b) Statemachine

    [Fact]
    public void ExportRequested_SetsExportInProgress()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState();

        var result = store.Reduce(state, new YtAction.ExportRequested());

        Assert.IsType<ImportExportState.ExportInProgress>(result.ImportExport);
    }

    [Fact]
    public void ExportPrepared_SetsExportSucceeded()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState { ImportExport = new ImportExportState.ExportInProgress() };
        var envelope = MakeEnvelope();

        var result = store.Reduce(state, new YtAction.ExportPrepared(envelope));

        Assert.IsType<ImportExportState.ExportSucceeded>(result.ImportExport);
    }

    [Fact]
    public void ExportFailed_SetsExportFailed()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState { ImportExport = new ImportExportState.ExportInProgress() };
        var error = new ExportError.SerializationFailed("boom");

        var result = store.Reduce(state, new YtAction.ExportFailed(error));

        var failed = Assert.IsType<ImportExportState.ExportFailed>(result.ImportExport);
        Assert.IsType<ExportError.SerializationFailed>(failed.Error);
    }

    [Fact]
    public void ImportRequested_SetsImportParsing()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState();

        var result = store.Reduce(state, new YtAction.ImportRequested("{}"));

        Assert.IsType<ImportExportState.ImportParsing>(result.ImportExport);
    }

    [Fact]
    public void ImportParsed_SetsImportParsed()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState { ImportExport = new ImportExportState.ImportParsing() };
        var envelope = MakeEnvelope();

        var result = store.Reduce(state, new YtAction.ImportParsed(envelope));

        var parsed = Assert.IsType<ImportExportState.ImportParsed>(result.ImportExport);
        Assert.Same(envelope, parsed.Envelope);
    }

    [Fact]
    public void ImportValidated_SetsImportValidated()
    {
        var store = CreateStore();
        var envelope = MakeEnvelope();
        var state = new YouTubePlayerState { ImportExport = new ImportExportState.ImportParsed(envelope) };

        var result = store.Reduce(state, new YtAction.ImportValidated(envelope));

        Assert.IsType<ImportExportState.ImportValidated>(result.ImportExport);
    }

    [Fact]
    public void ImportSucceeded_SetsImportSucceeded()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState { ImportExport = new ImportExportState.ImportApplied() };

        var result = store.Reduce(state, new YtAction.ImportSucceeded(3, 10));

        var succeeded = Assert.IsType<ImportExportState.ImportSucceeded>(result.ImportExport);
        Assert.Equal(3, succeeded.PlaylistCount);
        Assert.Equal(10, succeeded.VideoCount);
    }

    [Fact]
    public void ImportFailed_SetsImportFailed()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState();
        var error = new ImportError.ParseError("bad json");

        var result = store.Reduce(state, new YtAction.ImportFailed(error));

        var failed = Assert.IsType<ImportExportState.ImportFailed>(result.ImportExport);
        Assert.IsType<ImportError.ParseError>(failed.Error);
    }

    #endregion

    #region (c) Dirty-Flag

    [Fact]
    public void ImportApplied_SetsDirtyTrue()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState();
        Assert.False(state.Persistence.IsDirty);

        var imported = MakeImportedPlaylists();
        var result = store.Reduce(state, new YtAction.ImportApplied(imported, null));

        Assert.True(result.Persistence.IsDirty);
    }

    [Fact]
    public void PersistSucceeded_ClearsDirtyFlag()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState { Persistence = new PersistenceState(IsDirty: true) };

        var result = store.Reduce(state, new YtAction.PersistSucceeded());

        Assert.False(result.Persistence.IsDirty);
        Assert.NotNull(result.Persistence.LastPersistAttemptUtc);
        Assert.Null(result.Persistence.LastPersistError);
    }

    [Fact]
    public void PersistFailed_KeepsDirtyAndSetsError()
    {
        var store = CreateStore();
        var state = new YouTubePlayerState { Persistence = new PersistenceState(IsDirty: true) };

        var result = store.Reduce(state, new YtAction.PersistFailed("disk full"));

        Assert.True(result.Persistence.IsDirty);
        Assert.NotNull(result.Persistence.LastPersistAttemptUtc);
        Assert.Equal("disk full", result.Persistence.LastPersistError);
    }

    #endregion

    #region (d) Undo-Integration

    [Fact]
    public void ImportApplied_ClearsUndoHistory()
    {
        var store = CreateStore();
        var v0 = MakeVideo(0);
        var v1 = MakeVideo(1);
        var state = StateWithCurrentVideo([v0, v1], 0);

        // Build undo history via SelectVideo
        var s1 = store.Reduce(state, new YtAction.SelectVideo(1, Autoplay: false));
        Assert.Single(s1.Queue.Past);

        // ImportApplied should clear undo history
        var imported = MakeImportedPlaylists();
        var result = store.Reduce(s1, new YtAction.ImportApplied(imported, null));

        Assert.Empty(result.Queue.Past);
        Assert.Empty(result.Queue.Future);
    }

    #endregion

    // Minimal fakes (same pattern as ShuffleRepeatReducerTests)
    private class FakePlaylistService : Features.YouTubePlayer.Service.IPlaylistService
    {
        public Task<List<Playlist>> GetAllPlaylistsAsync() => Task.FromResult(new List<Playlist>());
        public Task<Playlist?> GetPlaylistByIdAsync(Guid id) => Task.FromResult<Playlist?>(null);
        public Task CreatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task UpdatePlaylistAsync(Playlist playlist) => Task.CompletedTask;
        public Task DeletePlaylistAsync(Guid playlistId) => Task.CompletedTask;
        public Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video) => Task.CompletedTask;
        public Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId) => Task.CompletedTask;
        public Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos) => Task.CompletedTask;
        public Task ReplaceAllPlaylistsAsync(List<Playlist> playlists) => Task.CompletedTask;
    }

    private class FakeJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => default;
    }
}
