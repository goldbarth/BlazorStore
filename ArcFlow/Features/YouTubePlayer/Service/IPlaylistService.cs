using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.Service;

public interface IPlaylistService
{
    Task<List<Playlist>> GetAllPlaylistsAsync();
    Task<Playlist?> GetPlaylistByIdAsync(Guid playlistId);
    
    Task CreatePlaylistAsync(Playlist playlist);
    Task UpdatePlaylistAsync(Playlist playlist);
    Task DeletePlaylistAsync(Guid playlistId);
    
    Task AddVideoToPlaylistAsync(Guid playlistId, VideoItem video);
    Task RemoveVideoFromPlaylistAsync(Guid playlistId, Guid videoId);
    Task UpdateVideoPositionsAsync(Guid playlistId, List<VideoItem> videos);

    Task ReplaceAllPlaylistsAsync(List<Playlist> playlists);
}