using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Channels;
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

    public YouTubePlayerStore(IPlaylistService playlistService, IJSRuntime jsRuntime)
    {
        _playlistService = playlistService;
        _jsRuntime = jsRuntime;
        
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

    private YouTubePlayerState Reduce(YouTubePlayerState state, YtAction action)
    {
        var newState = action switch
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
            // Actions that are ONLY processed in Effects (no state update)
            YtAction.CreatePlaylist => state,
            YtAction.AddVideo => state,
            // Error Handling
            YtAction.OperationFailed failed => HandleOperationFailed(state, failed),
            // Notifications
            YtAction.ShowNotification notification => HandleShowNotification(state, notification),
            YtAction.DismissNotification dismiss => HandleDismissNotification(state, dismiss),
            // Compiler forces you to handle all actions
            _ => throw new UnreachableException($"Unhandled action: {action.GetType().Name}")
        };
        
        return newState with { Queue = newState.Queue.Validate() };
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
                CurrentIndex = null
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

        return state with
        {
            Queue = state.Queue with { CurrentIndex = action.Index },
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
                CurrentIndex = null
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

        var id = playerStateChanged.VideoId ?? expected;

        if (state.Player is not PlayerState.Loading)
        {
            if (id is null || expected is null || id != expected)
                return state;
        }

        if (state.Player is PlayerState.Loading && id is null)
            return state;

        var newPlayer = playerStateChanged.YtState switch
        {
            3 => new PlayerState.Buffering(id!),// BUFFERING
            1 => new PlayerState.Playing(id!),  // PLAYING
            2 => new PlayerState.Paused(id!),   // PAUSED
            5 => new PlayerState.Paused(id!),   // CUED
            0 => new PlayerState.Paused(id!),   // ENDED
            -1 => state.Player,                     // UNSTARTED - ignore
            _ => state.Player
        };

        return state with { Player = newPlayer };
    }
    
    private YouTubePlayerState HandleVideoEnded(YouTubePlayerState state)
    {
        return state;
    }
    
    #endregion

    #region Effects
    
    // Effects (interop + service)
    private async Task RunEffects(YtAction action)
    {
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
                await SelectNextVideoWithAutoplay();
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
        }
    }
    
    private async Task LoadAndSelectInitialPlaylist()
    {
        var context = OperationContext.Create("LoadAndSelectInitialPlaylist");
        
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
            await _jsRuntime.InvokeVoidAsync(
                "YouTubePlayerInterop.loadVideo", 
                video.YouTubeId, 
                selectVideo.Autoplay
                );
        }
        catch (JSException ex)
        {
            await Dispatch(new YtAction.OperationFailed(
                new OperationError(
                    ErrorCategory.External,
                    $"Failed to load video '{video.Title}' in YouTube player",
                    context,
                    ex
                )
            ));
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
                    $"Playlist '{playlist.Name}' successfully created.",
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
    
    private async Task SelectNextVideoWithAutoplay()
    {
        if (State.Queue.CurrentIndex is { } i && i < State.Queue.Videos.Count - 1)
            await Dispatch(new YtAction.SelectVideo(i + 1, Autoplay: true));
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
        // goldbarth: Easy extraction - can be expanded
        try
        {
            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["v"] ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    #endregion
    
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
    
    private YouTubePlayerState HandleShowNotification(YouTubePlayerState state, YtAction.ShowNotification action)
    {
        return state with 
        { 
            Notifications = state.Notifications.Add(action.Notification)
        };
    }
    
    private YouTubePlayerState HandleDismissNotification(YouTubePlayerState state, YtAction.DismissNotification action)
    {
        return state with 
        { 
            Notifications = state.Notifications.RemoveAll(n => n.CorrelationId == action.CorrelationId)
        };
    }
}