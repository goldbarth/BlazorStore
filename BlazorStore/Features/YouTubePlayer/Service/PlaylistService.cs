using BlazorStore.Data;
using BlazorStore.Features.YouTubePlayer.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorStore.Features.YouTubePlayer.Service;

public class PlaylistService(ApplicationDbContext context) : IPlaylistService
{
    // SQL: SELECT * FROM Categories
    public async Task<List<Playlist>> GetAllPlaylistsAsync()
    {
        return await context.Playlists
            .Include(p => p.VideoItems)
            .ToListAsync();
    }

    // SQL: SELECT * FROM Playlist WHERE Id = @id
    // FindAsync uses the primary key and caches internally
    public async Task<Playlist?> GetPlaylistByIdAsync(Guid playlistId)
    {
        return await context.Playlists
            .Include(p => p.VideoItems)
            .FirstOrDefaultAsync(p => p.Id == playlistId);
    }

    // SQL: INSERT INTO Playlist
    public async Task CreatePlaylistAsync(Playlist playlist)
    {
        await context.Playlists.AddAsync(playlist);
        await context.SaveChangesAsync();
    }

    // SQL: UPDATE Playlist SET Name = @name, Description = @description WHERE Id = @id
    public async Task UpdatePlaylistAsync(Playlist playlist)
    {
        context.Playlists.Update(playlist);
        await context.SaveChangesAsync();
    }

    // SQL: DELETE FROM Playlist WHERE Id = @id
    public async Task DeletePlaylistAsync(Guid playlistId)
    {
        var playlist = await context.Playlists.FindAsync(playlistId);
        if (playlist == null)
            return;
        
        context.Playlists.Remove(playlist);
        await context.SaveChangesAsync();
    }

    // SQL: INSERT INTO VideoItem
    public async Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video)
    {
        bool playlistExists = await context.Playlists.AnyAsync(p => p.Id == playlistId);
        if (!playlistExists)
            return;
        
        video.PlaylistId = playlistId;
        
        await context.VideoItems.AddAsync(video);
        await context.SaveChangesAsync();
    }

    // SQL: DELETE FROM Playlist WHERE PlaylistId = @playlistId AND VideoId = @videoId
    public async Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId)
    {
        var playlist = await context.Playlists
            .Include(p => p.VideoItems)
            .FirstOrDefaultAsync(p => p.Id == playlistId);

        var video = playlist?.VideoItems.FirstOrDefault(v => v.Id == videoId);
        if (video == null)
            return;

        playlist?.VideoItems.Remove(video);
        await context.SaveChangesAsync();
    }

    // SQL: UPDATE VideoItem SET Position = @position WHERE Id = @id
    public async Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos)
    {
        var playlist = await context.Playlists
            .Include(p => p.VideoItems)
            .FirstOrDefaultAsync(p => p.Id == playlistId);

        if (playlist == null)
            return;

        foreach (var video in videos)
        {
            var existingVideo = playlist.VideoItems.FirstOrDefault(v => v.Id == video.Id);
            existingVideo?.Position = video.Position;
        }

        await context.SaveChangesAsync();
    }

    // SQL: DELETE all, then INSERT all (full snapshot replace)
    public async Task ReplaceAllPlaylistsAsync(List<Playlist> playlists)
    {
        context.VideoItems.RemoveRange(context.VideoItems);
        context.Playlists.RemoveRange(context.Playlists);
        await context.SaveChangesAsync();

        await context.Playlists.AddRangeAsync(playlists);
        await context.SaveChangesAsync();
    }
}