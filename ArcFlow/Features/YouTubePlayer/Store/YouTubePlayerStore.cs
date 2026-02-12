using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;
using ArcFlow.Features.YouTubePlayer.ImportExport;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.Service;
using ArcFlow.Features.YouTubePlayer.State;
using Microsoft.JSInterop;

namespace ArcFlow.Features.YouTubePlayer.Store;

public sealed class YouTubePlayerStore
{
    private readonly IPlaylistService _playlistService;
    private readonly IJSRuntime _jsRuntime;
    
    private readonly ILogger<YouTubePlayerStore> _logger;
    
    private readonly Channel<YtAction> _actionQueue;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public YouTubePlayerState State { get; private set; }
    
    public event Action<YouTubePlayerState>? StateChanged;
    
#if DEBUG
    private readonly List<StateTransition> _history = [];
    private const int MaxHistorySize = 50;
    
    public record StateTransition(
        DateTime Timestamp,
        YtAction Action,
        YouTubePlayerState StateBefore,
        YouTubePlayerState StateAfter
    );
#endif

    public YouTubePlayerStore(IPlaylistService playlistService, IJSRuntime jsRuntime, ILogger<YouTubePlayerStore> logger)
    {
        _playlistService = playlistService;
        _jsRuntime = jsRuntime;
        _logger = logger;

        State = new YouTubePlayerState();
        
        _actionQueue = Channel.CreateUnbounded<YtAction>();
        _ = ProcessActionsAsync(_cts.Token);
    }

    public async Task Dispatch(YtAction action)
    {
        if (_disposed) return;
        await _actionQueue.Writer.WriteAsync(action, _cts.Token);
    }
    
    private async Task ProcessActionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var action in _actionQueue.Reader.ReadAllAsync(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) 
                    break;
                    
                await ProcessActionAsync(action);
            }
        }
        catch (OperationCanceledException)
        {
            // goldbarth: Expected during disposal
        }
    }
    
    private async Task ProcessActionAsync(YtAction action)
    {
        var oldState = State;
        State = Reduce(State, action);
        
#if DEBUG
        _history.Add(new StateTransition(DateTime.UtcNow, action, oldState, State));
        if (_history.Count > MaxHistorySize)
            _history.RemoveAt(0);
#endif
        
        NotifyStateChanged();
        await RunEffects(action);
    }
    
    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(State);
    }
    
#if DEBUG
    public IReadOnlyList<StateTransition> GetHistory() => _history.AsReadOnly();
#endif
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _cts.Cancel();
        _actionQueue.Writer.Complete();
        _cts.Dispose();
        _disposed = true;
    }
    
    #region Reducer

    internal YouTubePlayerState Reduce(YouTubePlayerState state, YtAction action)
    {
        if (action is YtAction.UndoRequested)
            return ReduceUndo(state);

        if (action is YtAction.RedoRequested)
            return ReduceRedo(state);

        var preSnapshot = QueueSnapshot.FromQueueState(state.Queue);
        var oldVideosRef = state.Queue.Videos;
        var newState = ReduceStandard(state, action);
        newState = ApplyHistoryPolicy(state.Queue, newState, action, preSnapshot);

        // Repair playback structures if videos changed
        if (!ReferenceEquals(oldVideosRef, newState.Queue.Videos))
            newState = newState with { Queue = PlaybackNavigation.RepairPlaybackStructures(newState.Queue) };

        return newState with { Queue = newState.Queue.Validate() };
    }

    private YouTubePlayerState ReduceStandard(YouTubePlayerState state, YtAction action)
    {
        return action switch
        {
            // Actions with state update
            YtAction.Initialize => HandleInitialize(state),
            YtAction.SelectPlaylist selectPlaylist => HandleSelectPlaylist(state, selectPlaylist),
            YtAction.SelectVideo selectVideo => HandleSelectVideo(state, selectVideo),
            YtAction.SortChanged sortChanged => HandleSortChanged(state, sortChanged),
            YtAction.PlaylistsLoaded playlistsLoaded => HandlePlaylistsLoaded(state, playlistsLoaded),
            YtAction.PlaylistLoaded playlistLoaded => HandlePlaylistLoaded(state, playlistLoaded),
            YtAction.PlayerStateChanged stateChanged => HandlePlayerStateChanged(state, stateChanged),
            YtAction.VideoEnded => HandleVideoEnded(state),
            // Playback navigation
            YtAction.ShuffleSet shuffleSet => HandleShuffleSet(state, shuffleSet),
            YtAction.RepeatSet repeatSet => HandleRepeatSet(state, repeatSet),
            YtAction.NextRequested => HandleNextRequested(state),
            YtAction.PrevRequested => HandlePrevRequested(state),
            YtAction.PlaybackAdvanced => state,
            YtAction.PlaybackStopped => state,
            // Actions that are ONLY processed in Effects (no state update)
            YtAction.CreatePlaylist => state,
            YtAction.AddVideo => state,
            // Error Handling
            YtAction.OperationFailed failed => HandleOperationFailed(state, failed),
            // Notifications
            YtAction.ShowNotification notification => HandleShowNotification(state, notification),
            YtAction.DismissNotification dismiss => HandleDismissNotification(state, dismiss),
            // Import / Export
            YtAction.ExportRequested => state with { ImportExport = new ImportExportState.ExportInProgress() },
            YtAction.ExportPrepared ep => state with { ImportExport = new ImportExportState.ExportSucceeded(ep.Envelope.ExportedAtUtc) },
            YtAction.ExportSucceeded => state with { ImportExport = new ImportExportState.Idle() },
            YtAction.ExportFailed ef => state with { ImportExport = new ImportExportState.ExportFailed(ef.Error) },
            YtAction.ImportRequested => state with { ImportExport = new ImportExportState.ImportParsing() },
            YtAction.ImportParsed ip => state with { ImportExport = new ImportExportState.ImportParsed(ip.Envelope) },
            YtAction.ImportValidated iv => state with { ImportExport = new ImportExportState.ImportValidated(iv.Envelope) },
            YtAction.ImportApplied ia => HandleImportApplied(state, ia),
            YtAction.ImportSucceeded isuc => state with { ImportExport = new ImportExportState.ImportSucceeded(isuc.PlaylistCount, isuc.VideoCount) },
            YtAction.ImportFailed ifail => state with { ImportExport = new ImportExportState.ImportFailed(ifail.Error) },
            // Persistence
            YtAction.PersistRequested => state,
            YtAction.PersistSucceeded => state with { Persistence = state.Persistence with { IsDirty = false, LastPersistAttemptUtc = DateTime.UtcNow, LastPersistError = null } },
            YtAction.PersistFailed pf => state with { Persistence = state.Persistence with { LastPersistAttemptUtc = DateTime.UtcNow, LastPersistError = pf.Message } },
            // Compiler forces you to handle all actions
            _ => throw new UnreachableException($"Unhandled action: {action.GetType().Name}")
        };
    }

    private static YouTubePlayerState ReduceUndo(YouTubePlayerState state)
    {
        if (state.Queue.Past.IsEmpty)
            return state;

        var snapshot = state.Queue.Past[^1];
        var currentSnapshot = QueueSnapshot.FromQueueState(state.Queue);
        var restored = snapshot.ToQueueState();

        return state with
        {
            Queue = restored with
            {
                Past = state.Queue.Past.RemoveAt(state.Queue.Past.Count - 1),
                Future = state.Queue.Future.Add(currentSnapshot)
            }
        };
    }

    private static YouTubePlayerState ReduceRedo(YouTubePlayerState state)
    {
        if (state.Queue.Future.IsEmpty)
            return state;

        var snapshot = state.Queue.Future[^1];
        var currentSnapshot = QueueSnapshot.FromQueueState(state.Queue);
        var restored = snapshot.ToQueueState();

        return state with
        {
            Queue = restored with
            {
                Past = state.Queue.Past.Add(currentSnapshot),
                Future = state.Queue.Future.RemoveAt(state.Queue.Future.Count - 1)
            }
        };
    }

    private static YouTubePlayerState ApplyHistoryPolicy(
        QueueState oldQueue, YouTubePlayerState newState, YtAction action, QueueSnapshot preSnapshot)
    {
        // Playback transient actions preserve undo history without modification
        if (UndoPolicy.IsPlaybackTransient(action))
        {
            return newState with
            {
                Queue = newState.Queue with
                {
                    Past = oldQueue.Past,
                    Future = oldQueue.Future
                }
            };
        }

        if (UndoPolicy.IsBoundary(action))
        {
            return newState with
            {
                Queue = newState.Queue with
                {
                    Past = ImmutableList<QueueSnapshot>.Empty,
                    Future = ImmutableList<QueueSnapshot>.Empty
                }
            };
        }

        if (UndoPolicy.IsUndoable(action) && QueueDataChanged(oldQueue, newState.Queue))
        {
            var past = newState.Queue.Past.Add(preSnapshot);
            if (past.Count > QueueState.HistoryLimit)
                past = past.RemoveAt(0);

            return newState with
            {
                Queue = newState.Queue with
                {
                    Past = past,
                    Future = ImmutableList<QueueSnapshot>.Empty
                }
            };
        }

        // Non-undoable actions: preserve history as-is
        return newState with
        {
            Queue = newState.Queue with
            {
                Past = oldQueue.Past,
                Future = oldQueue.Future
            }
        };
    }

    private static bool QueueDataChanged(QueueState oldQueue, QueueState newQueue)
    {
        return oldQueue.SelectedPlaylistId != newQueue.SelectedPlaylistId
               || oldQueue.CurrentIndex != newQueue.CurrentIndex
               || !ReferenceEquals(oldQueue.Videos, newQueue.Videos);
    }

    private YouTubePlayerState HandleInitialize(YouTubePlayerState state)
    {
        return state with { Playlists = new PlaylistsState.Loading() };
    }
    
    private YouTubePlayerState HandleSelectPlaylist(YouTubePlayerState state, YtAction.SelectPlaylist selectPlaylist)
    {
        if (state.Queue.SelectedPlaylistId == selectPlaylist.PlaylistId)
            return state;

        return state with
        {
            Queue = state.Queue with
            {
                SelectedPlaylistId = selectPlaylist.PlaylistId,
                Videos = [],
                CurrentIndex = null,
                CurrentItemId = null,
                ShuffleOrder = ImmutableList<Guid>.Empty,
                PlaybackHistory = ImmutableList<Guid>.Empty
            },
            Player = new PlayerState.Empty()
        };
    }
    
    private static YouTubePlayerState HandleSelectVideo(YouTubePlayerState state, YtAction.SelectVideo action)
    {
        if (state.Queue.CurrentIndex == action.Index &&
            state.Player is not PlayerState.Empty) return state;

        if (state.Queue.Videos.Count == 0 || action.Index < 0
            || action.Index >= state.Queue.Videos.Count) return state;

        var video = state.Queue.Videos[action.Index];

        // Push old CurrentItemId to PlaybackHistory if shuffle is enabled
        var history = state.Queue.PlaybackHistory;
        if (state.Queue is { ShuffleEnabled: true, CurrentItemId: not null })
        {
            history = history.Add(state.Queue.CurrentItemId.Value);
            if (history.Count > QueueState.PlaybackHistoryLimit)
                history = history.RemoveAt(0);
        }

        return state with
        {
            Queue = state.Queue with
            {
                CurrentIndex = action.Index,
                CurrentItemId = video.Id,
                PlaybackHistory = history
            },
            Player = new PlayerState.Loading(video.YouTubeId, action.Autoplay)
        };
    }
    
    private YouTubePlayerState HandleSortChanged(YouTubePlayerState state, YtAction.SortChanged sortChanged)
    {
        var videos = state.Queue.Videos;
        if (videos.Count == 0 
            || sortChanged.OldIndex == sortChanged.NewIndex 
            || sortChanged.OldIndex < 0 || sortChanged.OldIndex >= videos.Count 
            || sortChanged.NewIndex < 0 || sortChanged.NewIndex >= videos.Count) return state;

        // ImmutableList → mutable List
        var list = videos.ToList();
        var moved = list[sortChanged.OldIndex];
    
        // Mutable Operations
        list.RemoveAt(sortChanged.OldIndex);
        list.Insert(sortChanged.NewIndex, moved);

        // Update CurrentIndex
        int? current = state.Queue.CurrentIndex;
        if (current is { } currentIndex)
        {
            if (currentIndex == sortChanged.OldIndex) 
                current = sortChanged.NewIndex;
            else if (sortChanged.OldIndex < currentIndex && sortChanged.NewIndex >= currentIndex) 
                current = currentIndex - 1;
            else if (sortChanged.OldIndex > currentIndex && sortChanged.NewIndex <= currentIndex) 
                current = currentIndex + 1;
        }

        // Reset positions
        for (int i = 0; i < list.Count; i++)
            list[i].Position = i;

        // mutable List → ImmutableList
        return state with 
        { 
            Queue = state.Queue with 
            { 
                Videos = list.ToImmutableList(), 
                CurrentIndex = current 
            } 
        };
    }
    
    private YouTubePlayerState HandlePlaylistsLoaded(YouTubePlayerState state, YtAction.PlaylistsLoaded playlistsLoaded)
    {
        return state with
        {
            Playlists = playlistsLoaded.Playlists.Count == 0
                ? new PlaylistsState.Empty()
                : new PlaylistsState.Loaded(playlistsLoaded.Playlists)
        };
    }
    
    private YouTubePlayerState HandlePlaylistLoaded(YouTubePlayerState state, YtAction.PlaylistLoaded playlistLoaded)
    {
        return state with
        {
            Queue = state.Queue with
            {
                SelectedPlaylistId = playlistLoaded.Playlist.Id,
                Videos = playlistLoaded.Playlist.VideoItems
                    .OrderBy(v => v.Position)
                    .ToImmutableList(),
                CurrentIndex = null,
                CurrentItemId = null,
                ShuffleOrder = ImmutableList<Guid>.Empty,
                PlaybackHistory = ImmutableList<Guid>.Empty
            }
        };
    }
    
    private static YouTubePlayerState HandlePlayerStateChanged(YouTubePlayerState state, YtAction.PlayerStateChanged playerStateChanged)
    {
        var expected = state.Player switch
        {
            PlayerState.Loading x => x.VideoId,
            PlayerState.Buffering x => x.VideoId,
            PlayerState.Playing x => x.VideoId,
            PlayerState.Paused x => x.VideoId,
            _ => null
        };

        var id = playerStateChanged.VideoId;

        if (state.Player is not PlayerState.Loading)
        {
            if (expected is null || id != expected)
                return state;
        }

        var newPlayer = playerStateChanged.YtState switch
        {
            3 => new PlayerState.Buffering(id),// BUFFERING
            1 => new PlayerState.Playing(id),  // PLAYING
            2 => new PlayerState.Paused(id),   // PAUSED
            5 => new PlayerState.Paused(id),   // CUED
            0 => new PlayerState.Paused(id),   // ENDED
            -1 => state.Player,                // UNSTARTED - ignore
            _ => state.Player
        };

        return state with { Player = newPlayer };
    }
    
    private YouTubePlayerState HandleVideoEnded(YouTubePlayerState state)
    {
        return state;
    }

    private static YouTubePlayerState HandleShuffleSet(YouTubePlayerState state, YtAction.ShuffleSet action)
    {
        if (action.Enabled)
        {
            var seed = action.Seed ?? Environment.TickCount;
            var shuffleOrder = PlaybackNavigation.GenerateShuffleOrder(
                state.Queue.Videos, state.Queue.CurrentItemId, seed);

            return state with
            {
                Queue = state.Queue with
                {
                    ShuffleEnabled = true,
                    ShuffleSeed = seed,
                    ShuffleOrder = shuffleOrder,
                    PlaybackHistory = ImmutableList<Guid>.Empty
                }
            };
        }
        else
        {
            return state with
            {
                Queue = state.Queue with
                {
                    ShuffleEnabled = false,
                    ShuffleOrder = ImmutableList<Guid>.Empty,
                    PlaybackHistory = ImmutableList<Guid>.Empty
                }
            };
        }
    }

    private static YouTubePlayerState HandleRepeatSet(YouTubePlayerState state, YtAction.RepeatSet action)
    {
        return state with
        {
            Queue = state.Queue with { RepeatMode = action.Mode }
        };
    }

    private static YouTubePlayerState HandleNextRequested(YouTubePlayerState state)
    {
        var (decision, newQueue) = PlaybackNavigation.ComputeNext(state.Queue);

        return decision switch
        {
            PlaybackDecision.AdvanceTo adv => ApplyAdvanceTo(state, newQueue, adv.ItemId),
            PlaybackDecision.Stop => state with
            {
                Queue = newQueue,
                Player = state.Queue.CurrentItemId.HasValue
                    ? new PlayerState.Paused(
                        state.Queue.Videos.FirstOrDefault(v => v.Id == state.Queue.CurrentItemId.Value)?.YouTubeId ?? "")
                    : new PlayerState.Empty()
            },
            _ => state with { Queue = newQueue }
        };
    }

    private static YouTubePlayerState HandlePrevRequested(YouTubePlayerState state)
    {
        var (decision, newQueue) = PlaybackNavigation.ComputePrev(state.Queue);

        return decision switch
        {
            PlaybackDecision.AdvanceTo adv => ApplyAdvanceTo(state, newQueue, adv.ItemId),
            _ => state with { Queue = newQueue }
        };
    }

    private static YouTubePlayerState ApplyAdvanceTo(YouTubePlayerState state, QueueState queue, Guid itemId)
    {
        var idx = queue.Videos.FindIndex(v => v.Id == itemId);
        if (idx < 0) return state with { Queue = queue };

        var video = queue.Videos[idx];
        return state with
        {
            Queue = queue with
            {
                CurrentIndex = idx,
                CurrentItemId = itemId
            },
            Player = new PlayerState.Loading(video.YouTubeId, true)
        };
    }

    private static YouTubePlayerState HandleImportApplied(YouTubePlayerState state, YtAction.ImportApplied action)
    {
        return state with
        {
            Playlists = new PlaylistsState.Loaded(action.Playlists),
            Queue = new QueueState { SelectedPlaylistId = action.SelectedPlaylistId },
            Player = new PlayerState.Empty(),
            ImportExport = new ImportExportState.ImportApplied(),
            Persistence = state.Persistence with { IsDirty = true }
        };
    }

    #endregion

    #region Effects
    
    // Effects (interop + service)
    private async Task RunEffects(YtAction action)
    {
        if (action is YtAction.UndoRequested or YtAction.RedoRequested)
            return;

        switch (action)
        {
            case YtAction.Initialize:
            {
                await LoadAndSelectInitialPlaylist();
                break;
            }

            case YtAction.SelectPlaylist selectPlaylist:
            {
                await LoadAndDispatchPlaylist(selectPlaylist);
                break;
            }

            case YtAction.SelectVideo selectVideo:
            {
                await LoadSelectedVideo(selectVideo);
                break;
            }

            case YtAction.SortChanged:
            {
                await UpdatePlaylistVideoPositionsAsync();
                break;
            }

            case YtAction.VideoEnded:
            {
                await Dispatch(new YtAction.NextRequested());
                break;
            }

            case YtAction.NextRequested:
            case YtAction.PrevRequested:
            {
                await LoadCurrentVideoFromState();
                break;
            }
            
            case YtAction.AddVideo addVideo:
            {
                await AddVideoToPlaylist(addVideo);
                break;
            }
            
            case YtAction.CreatePlaylist cp:
            {
                await CreateAndSelectPlaylist(cp);
                break;
            }

            case YtAction.ExportRequested:
            {
                await RunExportEffect();
                break;
            }

            case YtAction.ImportApplied:
            case YtAction.PersistRequested:
            {
                await RunPersistEffect();
                break;
            }
        }
    }
    
    private async Task RunExportEffect()
    {
        if (State.Playlists is not PlaylistsState.Loaded loaded)
        {
            await Dispatch(new YtAction.ExportFailed(
                new ExportError.SerializationFailed("No playlists loaded")));
            return;
        }

        ExportEnvelopeV1 envelope;
        string json;
        try
        {
            envelope = ExportMapper.ToEnvelope(loaded.Items, State.Queue.SelectedPlaylistId);
            json = ExportSerializer.Serialize(envelope);
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.ExportFailed(
                new ExportError.SerializationFailed(ex.Message, ex)));
            return;
        }

        await Dispatch(new YtAction.ExportPrepared(envelope));

        var fileName = $"arcflow-export-{DateTime.UtcNow:yyyy-MM-dd}.json";
        try
        {
            await _jsRuntime.InvokeVoidAsync("ExportInterop.downloadFile", fileName, json);
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.ExportFailed(
                new ExportError.InteropFailed(ex.Message, ex)));
            return;
        }

        await Dispatch(new YtAction.ExportSucceeded());
    }

    private async Task RunPersistEffect()
    {
        if (!State.Persistence.IsDirty)
            return;

        if (State.Playlists is not PlaylistsState.Loaded loaded)
        {
            await Dispatch(new YtAction.PersistFailed("No playlists loaded to persist"));
            return;
        }

        try
        {
            var snapshot = loaded.Items.Select(p => new Playlist
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                VideoItems = p.VideoItems.Select(v => new VideoItem
                {
                    Id = v.Id,
                    YouTubeId = v.YouTubeId,
                    Title = v.Title,
                    ThumbnailUrl = v.ThumbnailUrl,
                    Duration = v.Duration,
                    AddedAt = v.AddedAt,
                    Position = v.Position,
                    PlaylistId = p.Id
                }).ToList()
            }).ToList();

            await _playlistService.ReplaceAllPlaylistsAsync(snapshot);
            await Dispatch(new YtAction.PersistSucceeded());

            _logger.LogInformation("Persist succeeded: {PlaylistCount} playlists written", snapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persist failed");
            await Dispatch(new YtAction.PersistFailed(ex.Message, ex));
        }
    }

    private async Task LoadAndSelectInitialPlaylist()
    {
        var context = OperationContext.Create("LoadAndSelectInitialPlaylist");
        
        _logger.LogInformation("Loading initial playlists - CorrelationId: {CorrelationId}", context.CorrelationId);
        
        try
        {
            var playlists = await _playlistService.GetAllPlaylistsAsync();
            await Dispatch(new YtAction.PlaylistsLoaded(playlists.ToImmutableList()));

            if (playlists.Count > 0)
                await Dispatch(new YtAction.SelectPlaylist(playlists[0].Id));
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Unexpected,
                    "Failed to load initial playlists",
                    context,
                    ex
                )
            ));
        }
    }
    
    private async Task LoadAndDispatchPlaylist(YtAction.SelectPlaylist selectPlaylist)
    {
        var context = OperationContext.Create(
            "LoadAndDispatchPlaylist",
            playlistId: selectPlaylist.PlaylistId
        );
        
        try
        {
            var playlist = await _playlistService.GetPlaylistByIdAsync(selectPlaylist.PlaylistId);
            
            if (playlist is null)
            {
                await Dispatch(new YtAction.OperationFailed(
                    new OperationError(
                        ErrorCategory.NotFound,
                        $"Playlist with ID {selectPlaylist.PlaylistId} not found",
                        context
                    )
                ));
                return;
            }
                    
            await Dispatch(new YtAction.PlaylistLoaded(playlist));

            if (playlist.VideoItems.Count != 0)
                await Dispatch(new YtAction.SelectVideo(0, Autoplay: false));
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Unexpected,
                    "Failed to load playlist",
                    context,
                    ex
                )
            ));
        }
    }
    
    private async Task LoadSelectedVideo(YtAction.SelectVideo selectVideo)
    {
        var videos = State.Queue.Videos;
        if (selectVideo.Index < 0 || selectVideo.Index >= videos.Count) return;

        var video = videos[selectVideo.Index];
        var context = OperationContext.Create(
            "LoadSelectedVideo",
            videoId: video.Id,
            index: selectVideo.Index
        );
        
        try
        {
            // Check if player container exists before attempting to load
            var playerExists = await _jsRuntime.InvokeAsync<bool>("eval", 
                "document.getElementById('youtube-player-container') !== null");
            
            if (!playerExists)
            {
                _logger.LogWarning("YouTube player container not found, skipping video load");
                return;
            }
            
            await _jsRuntime.InvokeVoidAsync(
                "YouTubePlayerInterop.loadVideo", 
                video.YouTubeId, 
                selectVideo.Autoplay
            );
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JS error while loading video, player may not be ready yet");
            // Don't dispatch error on initial load - player might not be ready yet
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Unexpected,
                    "Unexpected error while loading video",
                    context,
                    ex
                )
            ));
        }
    }

    private async Task CreateAndSelectPlaylist(YtAction.CreatePlaylist createPlaylist)
    {
        var context = OperationContext.Create("CreateAndSelectPlaylist");
        
        try
        {
            if (string.IsNullOrWhiteSpace(createPlaylist.Name))
            {
                await Dispatch(new YtAction.OperationFailed(
                    new OperationError(
                        ErrorCategory.Validation,
                        "Playlist name cannot be empty.",
                        context
                    )
                ));
                return;
            }
            
            var playlist = new Playlist
            {
                Id = Guid.NewGuid(),
                Name = createPlaylist.Name,
                Description = createPlaylist.Description ?? string.Empty,
                VideoItems = []
            };

            await _playlistService.CreatePlaylistAsync(playlist);
            
            _logger.LogInformation(
                "Playlist created: {PlaylistId} | Name: {Name} | CorrelationId: {CorrelationId}",
                playlist.Id,
                playlist.Name,
                context.CorrelationId
            );

            var playlists = await _playlistService.GetAllPlaylistsAsync();
            await Dispatch(new YtAction.PlaylistsLoaded(playlists.ToImmutableList()));
            await Dispatch(new YtAction.SelectPlaylist(playlist.Id));
            
            // Success notification
            await Dispatch(new YtAction.ShowNotification(
                new Notification(
                    NotificationSeverity.Success,
                    $"Playlist '{playlist.Name}' successfully  created.",
                    context.CorrelationId,
                    DateTime.UtcNow
                )
            ));
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Unexpected,
                    "Failed to create playlist",
                    context,
                    ex
                )
            ));
        }
    }
    
    private async Task UpdatePlaylistVideoPositionsAsync()
    {
        var pid = State.Queue.SelectedPlaylistId;
        if (pid is null) return;
        
        var context = OperationContext.Create(
            "UpdatePlaylistVideoPositions", 
            playlistId: pid
            );
        
        try
        {
            await _playlistService.UpdateVideoPositionsAsync(pid.Value, State.Queue.Videos.ToList());
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Unexpected,
                    "Failed to update video positions",
                    context,
                    ex
                )
            ));
        }
    }
    
    private async Task LoadCurrentVideoFromState()
    {
        if (State.Player is not PlayerState.Loading loading)
            return;

        var videoId = loading.VideoId;
        var autoplay = loading.Autoplay;

        try
        {
            var playerExists = await _jsRuntime.InvokeAsync<bool>("eval",
                "document.getElementById('youtube-player-container') !== null");

            if (!playerExists)
            {
                _logger.LogWarning("YouTube player container not found, skipping video load");
                return;
            }

            await _jsRuntime.InvokeVoidAsync(
                "YouTubePlayerInterop.loadVideo",
                videoId,
                autoplay
            );
        }
        catch (JSException ex)
        {
            _logger.LogWarning(ex, "JS error while loading video, player may not be ready yet");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading video via playback navigation");
        }
    }
    
    private async Task AddVideoToPlaylist(YtAction.AddVideo addVideo)
    {
        var context = OperationContext.Create(
            "AddVideoToPlaylist",
            playlistId: addVideo.PlaylistId
        );
        
        var youtubeId = ExtractYouTubeId(addVideo.Url);
        if (string.IsNullOrWhiteSpace(youtubeId))
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Validation,
                    "Invalid YouTube-URL.",
                    context
                )
            ));
            return;
        }

        try
        {
            var video = new VideoItem
            {
                Id = Guid.NewGuid(),
                YouTubeId = youtubeId,
                Title = addVideo.Title,
                ThumbnailUrl = $"https://img.youtube.com/vi/{youtubeId}/mqdefault.jpg"
            };

            await _playlistService.AddVideoToPlaylistAsync(addVideo.PlaylistId, video);
            
            _logger.LogInformation(
                "Video added: {VideoId} | YouTubeId: {YouTubeId} | PlaylistId: {PlaylistId} | CorrelationId: {CorrelationId}",
                video.Id,
                video.YouTubeId,
                addVideo.PlaylistId,
                context.CorrelationId
            );

            var playlist = await _playlistService.GetPlaylistByIdAsync(addVideo.PlaylistId);
            
            if (playlist is null)
            {
                await Dispatch(new YtAction.OperationFailed(
                    new OperationError(
                        ErrorCategory.NotFound,
                        $"Playlist {addVideo.PlaylistId} not found after adding video",
                        context with { VideoId = video.Id }
                    )
                ));
                return;
            }
            
            await Dispatch(new YtAction.PlaylistLoaded(playlist));
                    
            var idx = playlist.VideoItems
                .OrderBy(v => v.Position)
                .ToList()
                .FindIndex(v => v.Id == video.Id);

            if (idx >= 0)
                await Dispatch(new YtAction.SelectVideo(idx, Autoplay: false));
            
            // Success notification
            await Dispatch(new YtAction.ShowNotification(
                new Notification(
                    NotificationSeverity.Success,
                    $"Video '{video.Title}' added.",
                    context.CorrelationId,
                    DateTime.UtcNow
                )
            ));
        }
        catch (Exception ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.Unexpected,
                    "Failed to add video to playlist",
                    context,
                    ex
                )
            ));
        }
    }

    private string ExtractYouTubeId(string url)
    {
        try
        {
            // Handle different YouTube URL formats
            // https://www.youtube.com/watch?v=VIDEO_ID
            // https://youtu.be/VIDEO_ID
            // https://www.youtube.com/embed/VIDEO_ID
            
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;
            
            var uri = new Uri(url);
            
            // Standard watch URL
            if (uri.Host.Contains("youtube.com") && uri.AbsolutePath == "/watch")
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var videoId = query["v"];
                return IsValidYouTubeId(videoId) ? videoId! : string.Empty;
            }
            
            // Short URL (youtu.be)
            if (uri.Host == "youtu.be")
            {
                var videoId = uri.AbsolutePath.TrimStart('/');
                return IsValidYouTubeId(videoId) ? videoId : string.Empty;
            }
            
            // Embed URL
            if (uri.Host.Contains("youtube.com") && uri.AbsolutePath.StartsWith("/embed/"))
            {
                var videoId = uri.AbsolutePath.Replace("/embed/", "");
                return IsValidYouTubeId(videoId) ? videoId : string.Empty;
            }
            
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private bool IsValidYouTubeId(string? videoId)
    {
        // YouTube IDs are exactly 11 characters, alphanumeric plus - and _
        if (string.IsNullOrWhiteSpace(videoId) || videoId.Length != 11)
            return false;
            
        return videoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
    
    #endregion

    #region Error Handling & Notifications
    
    private YouTubePlayerState HandleOperationFailed(YouTubePlayerState state, YtAction.OperationFailed failed)
    {
        // Log structured error
        LogOperationError(failed.Error);
        
        // Map to user-friendly notification
        var notification = MapErrorToNotification(failed.Error);
        
        return state with 
        { 
            Notifications = state.Notifications.Add(notification)
        };
    }
    
    private void LogOperationError(OperationError error)
    {
        var logLevel = error.Category switch
        {
            ErrorCategory.Validation => LogLevel.Warning,
            ErrorCategory.NotFound => LogLevel.Warning,
            ErrorCategory.Transient => LogLevel.Warning,
            ErrorCategory.External => LogLevel.Error,
            ErrorCategory.Unexpected => LogLevel.Error,
            _ => LogLevel.Information
        };

        _logger.Log(
            logLevel,
            error.InnerException,
            "Operation failed: {Operation} | Category: {Category} | CorrelationId: {CorrelationId} | PlaylistId: {PlaylistId} | VideoId: {VideoId} | Message: {Message}",
            error.Context.Operation,
            error.Category,
            error.Context.CorrelationId,
            error.Context.PlaylistId,
            error.Context.VideoId,
            error.Message
        );
    }
    
    private Notification MapErrorToNotification(OperationError error)
    {
        var userMessage = error.Category switch
        {
            ErrorCategory.Validation => error.Message,
            ErrorCategory.NotFound => "The requested resource was not found.",
            ErrorCategory.Transient => "Network error. Please try again.",
            ErrorCategory.External => "Error loading YouTube player.",
            ErrorCategory.Unexpected => "An unexpected error has occurred.",
            _ => "An error has occurred."
        };

        return Notification.FromError(error, userMessage);
    }
    
    private static YouTubePlayerState HandleShowNotification(YouTubePlayerState state, YtAction.ShowNotification action)
    {
        return state with
        {
            Notifications = state.Notifications.Add(action.Notification)
        };
    }

    private static YouTubePlayerState HandleDismissNotification(YouTubePlayerState state, YtAction.DismissNotification action)
    {
        return state with
        {
            Notifications = state.Notifications.RemoveAll(n => n.CorrelationId == action.CorrelationId)
        };
    }
    
    #endregion
}