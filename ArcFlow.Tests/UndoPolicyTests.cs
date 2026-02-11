using ArcFlow.Features.YouTubePlayer.State;

namespace ArcFlow.Tests;

public class UndoPolicyTests
{
    [Fact]
    public void SelectVideo_IsUndoable()
    {
        var action = new YtAction.SelectVideo(0, Autoplay: false);
        Assert.True(UndoPolicy.IsUndoable(action));
    }

    [Fact]
    public void SortChanged_IsUndoable()
    {
        var action = new YtAction.SortChanged(0, 1);
        Assert.True(UndoPolicy.IsUndoable(action));
    }

    [Fact]
    public void PlayerStateChanged_IsNotUndoable()
    {
        var action = new YtAction.PlayerStateChanged(1, "abc");
        Assert.False(UndoPolicy.IsUndoable(action));
    }

    [Fact]
    public void Initialize_IsNotUndoable()
    {
        var action = new YtAction.Initialize();
        Assert.False(UndoPolicy.IsUndoable(action));
    }

    [Fact]
    public void PlaylistLoaded_IsBoundary()
    {
        var action = new YtAction.PlaylistLoaded(new ArcFlow.Features.YouTubePlayer.Models.Playlist
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            VideoItems = []
        });
        Assert.True(UndoPolicy.IsBoundary(action));
    }

    [Fact]
    public void SelectPlaylist_IsBoundary()
    {
        var action = new YtAction.SelectPlaylist(Guid.NewGuid());
        Assert.True(UndoPolicy.IsBoundary(action));
    }

    [Fact]
    public void SelectVideo_IsNotBoundary()
    {
        var action = new YtAction.SelectVideo(0, Autoplay: false);
        Assert.False(UndoPolicy.IsBoundary(action));
    }

    [Fact]
    public void UndoRequested_IsNotUndoable()
    {
        var action = new YtAction.UndoRequested();
        Assert.False(UndoPolicy.IsUndoable(action));
    }

    [Fact]
    public void RedoRequested_IsNotUndoable()
    {
        var action = new YtAction.RedoRequested();
        Assert.False(UndoPolicy.IsUndoable(action));
    }

    [Theory]
    [InlineData(typeof(YtAction.NextRequested))]
    [InlineData(typeof(YtAction.PrevRequested))]
    [InlineData(typeof(YtAction.ShuffleSet))]
    [InlineData(typeof(YtAction.RepeatSet))]
    public void PlaybackTransientActions_AreTransient(Type actionType)
    {
        YtAction action = actionType.Name switch
        {
            nameof(YtAction.NextRequested) => new YtAction.NextRequested(),
            nameof(YtAction.PrevRequested) => new YtAction.PrevRequested(),
            nameof(YtAction.ShuffleSet) => new YtAction.ShuffleSet(true),
            nameof(YtAction.RepeatSet) => new YtAction.RepeatSet(RepeatMode.All),
            _ => throw new InvalidOperationException()
        };
        Assert.True(UndoPolicy.IsPlaybackTransient(action));
    }

    [Fact]
    public void PlaybackAdvanced_IsTransient()
    {
        var action = new YtAction.PlaybackAdvanced(Guid.NewGuid(), AdvanceReason.Next);
        Assert.True(UndoPolicy.IsPlaybackTransient(action));
    }

    [Fact]
    public void PlaybackStopped_IsTransient()
    {
        var action = new YtAction.PlaybackStopped(StopReason.EndOfQueue);
        Assert.True(UndoPolicy.IsPlaybackTransient(action));
    }

    [Fact]
    public void SelectVideo_IsNotPlaybackTransient()
    {
        var action = new YtAction.SelectVideo(0, Autoplay: false);
        Assert.False(UndoPolicy.IsPlaybackTransient(action));
    }
}