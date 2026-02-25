using System.Collections.Immutable;
using BlazorStore.Features.YouTubePlayer.Models;

namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Discriminated union representing the loading lifecycle of the playlist collection.
/// Transitions: Loading -> Loaded | Empty | Error.
/// </summary>
public abstract record PlaylistsState
{
    private PlaylistsState() { }

    /// <summary>Playlists are being fetched from the database.</summary>
    public sealed record Loading : PlaylistsState;

    /// <summary>Playlists were loaded successfully.</summary>
    public sealed record Loaded(ImmutableList<Playlist> Items) : PlaylistsState;

    /// <summary>No playlists exist yet.</summary>
    public sealed record Empty : PlaylistsState;

    /// <summary>Loading playlists failed.</summary>
    public sealed record Error(string Message) : PlaylistsState;
}