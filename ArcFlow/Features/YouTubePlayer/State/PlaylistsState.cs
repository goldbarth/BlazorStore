using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.State;

public abstract record PlaylistsState
{
    private PlaylistsState() { } // Prevents external inheritance
    
    public sealed record Loading : PlaylistsState;
    public sealed record Loaded(ImmutableList<Playlist> Items) : PlaylistsState;
    public sealed record Empty : PlaylistsState;
    public sealed record Error(string Message) : PlaylistsState;
}