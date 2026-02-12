using ArcFlow.Features.YouTubePlayer.Components;
using ArcFlow.Features.YouTubePlayer.Models;
using ArcFlow.Features.YouTubePlayer.State;
using ArcFlow.Features.YouTubePlayer.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace ArcFlow.Features.YouTubePlayer;

public partial class YouTubePlayer : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private YouTubePlayerStore Store { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    
    private DotNetObjectReference<YouTubePlayer>? _dotNetRef;
    
    private Guid? _lastPlaylistId;
    private int _lastVideoCount;
    
    private bool _createPlaylistDrawerOpen;
    private bool _addVideoDrawerOpen;
    private bool _importDrawerOpen;
    private bool _sortableInitialized;
    
    private readonly HashSet<Guid> _shownNotifications = [];

    private YouTubePlayerState State => Store.State;
    
    private IReadOnlyList<Playlist> Playlists =>
        State.Playlists is PlaylistsState.Loaded l ? l.Items : [];

    private Guid? SelectedPlaylistId => State.Queue.SelectedPlaylistId;
    private IReadOnlyList<VideoItem> Videos => State.Queue.Videos;
    private int? CurrentIndex => State.Queue.CurrentIndex;

    private VideoItem? CurrentVideo =>
        CurrentIndex is { } i and >= 0 && i < Videos.Count
            ? Videos[i]
            : null;
    
    private string SelectedPlaylistName =>
        Playlists.FirstOrDefault(p => p.Id == SelectedPlaylistId)?.Name
        ?? "No playlist selected";

    private bool IsPlaying => State.Player is PlayerState.Playing;
    private bool IsShuffleEnabled => State.Queue.ShuffleEnabled;
    private RepeatMode CurrentRepeatMode => State.Queue.RepeatMode;

    private bool CanPlayNext =>
        CurrentIndex is not null && Videos.Count > 0 &&
        (CurrentRepeatMode != RepeatMode.Off || IsShuffleEnabled || 
         CurrentIndex is { } i && i < Videos.Count - 1);

    private bool CanPlayPrevious =>
        CurrentIndex is not null && Videos.Count > 0 &&
        (IsShuffleEnabled
            ? !State.Queue.PlaybackHistory.IsEmpty
            : CurrentIndex is > 0);

    private string RepeatIcon => CurrentRepeatMode switch
    {
        RepeatMode.One => Icons.Material.Filled.RepeatOne,
        _ => Icons.Material.Filled.Repeat
    };
    
    private bool CanUndo => State.Queue.CanUndo;
    private bool CanRedo => State.Queue.CanRedo;
    
    private Task Undo() => Store.Dispatch(new YtAction.UndoRequested());
    private Task Redo() => Store.Dispatch(new YtAction.RedoRequested());

    protected override Task OnInitializedAsync()
    {
        Store.StateChanged += OnStoreStateChanged;
        return Task.CompletedTask;
    }
    
    private void OnStoreStateChanged(YouTubePlayerState newState)
    {
        foreach (var notification in newState.Notifications)
        {
            if (!_shownNotifications.Add(notification.CorrelationId)) continue;

            var severity = notification.Severity switch
            {
                NotificationSeverity.Success => Severity.Success,
                NotificationSeverity.Warning => Severity.Warning,
                NotificationSeverity.Error => Severity.Error,
                _ => Severity.Info
            };

            Snackbar.Add(notification.Message, severity);
            _ = Store.Dispatch(new YtAction.DismissNotification(notification.CorrelationId));
        }

        _ = InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JsRuntime.InvokeVoidAsync("YouTubePlayerInterop.init", _dotNetRef);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize YouTube Player: {ex.Message}");
            }

            await Store.Dispatch(new YtAction.Initialize());
        }
        
        var pid = SelectedPlaylistId;
        var count = Videos.Count;

        if (pid != _lastPlaylistId || count != _lastVideoCount)
        {
            try { await JsRuntime.InvokeVoidAsync("SortableInterop.destroy", "video-sortable-list"); }
            catch { /* ignored */ }


            _sortableInitialized = false;
            _lastPlaylistId = pid;
            _lastVideoCount = count;
        }

        if (Videos.Any() && !_sortableInitialized)
            await InitializeSortable();
    }

    private async Task InitializeSortable()
    {
        if (_dotNetRef == null) return;
        await JsRuntime.InvokeVoidAsync("SortableInterop.init", "video-sortable-list", _dotNetRef);
        _sortableInitialized = true;
    }

    private async Task TogglePlayPause()
    {
        switch (State.Player)
        {
            case PlayerState.Playing:
                await JsRuntime.InvokeVoidAsync("YouTubePlayerInterop.pause");
                break;
            case PlayerState.Paused:
                await JsRuntime.InvokeVoidAsync("YouTubePlayerInterop.play");
                break;
        }
    }

    private Task PlayNext() 
        => !CanPlayNext ? Task.CompletedTask : Store.Dispatch(new YtAction.NextRequested());

    private Task PlayPrevious() 
        => !CanPlayPrevious ? Task.CompletedTask : Store.Dispatch(new YtAction.PrevRequested());

    private Task ToggleShuffle() 
        => Store.Dispatch(new YtAction.ShuffleSet(!IsShuffleEnabled));

    private Task CycleRepeat()
    {
        var next = CurrentRepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.Off
        };
        return Store.Dispatch(new YtAction.RepeatSet(next));
    }
    
    [JSInvokable]
    public Task OnVideoClicked(string videoId)
    {
        var index = Videos.ToList().FindIndex(v => v.Id.ToString() == videoId);
        return index < 0
            ? Task.CompletedTask
            : Store.Dispatch(new YtAction.SelectVideo(index, Autoplay: true));
    }

    [JSInvokable]
    public Task OnSortChanged(int oldIndex, int newIndex) 
        => oldIndex == newIndex ? Task.CompletedTask : Store.Dispatch(new YtAction.SortChanged(oldIndex, newIndex));

    [JSInvokable]
    public async Task OnVideoEnded() 
        => await Store.Dispatch(new YtAction.VideoEnded());

    [JSInvokable("OnPlayerStateChanged")]
    public async Task OnPlayerStateChanged(int ytState, string? videoId) 
        => await Store.Dispatch(new YtAction.PlayerStateChanged(ytState, videoId!));

    private Task OnCreatePlaylistSubmit(CreatePlaylistDrawer.CreatePlaylistRequest r) 
        => Store.Dispatch(new YtAction.CreatePlaylist(r.Name, r.Description));

    private Task OnAddVideoSubmit(AddVideoDrawer.AddVideoRequest request) 
        => Store.Dispatch(new YtAction.AddVideo(request.PlaylistId, request.Url, request.Title));

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("SortableInterop.destroy", "video-sortable-list");
            await JsRuntime.InvokeVoidAsync("YouTubePlayerInterop.destroy");
        }
        catch { /* ignore */ }

        _dotNetRef?.Dispose();

        Store.StateChanged -= OnStoreStateChanged;
    }

    private string GetPlaylistItemStyle(bool isSelected)
    {
        const string baseStyle = "border-radius: 5px; margin-bottom: 8px; cursor: pointer;";

        return isSelected
            ? $"{baseStyle} background-color: #007bff; color: white;"
            : baseStyle;
    }

    private void OpenDrawer() => _createPlaylistDrawerOpen = true;

    private void OpenAddVideoDrawer() => _addVideoDrawerOpen = true;

    private Task OnExportClick() => Store.Dispatch(new YtAction.ExportRequested());
    private void OpenImportDrawer() => _importDrawerOpen = true;
    private Task OnImportSubmit(string json) => Store.Dispatch(new YtAction.ImportRequested(json));
    private Task OnPersistRetry() => Store.Dispatch(new YtAction.PersistRequested());
}