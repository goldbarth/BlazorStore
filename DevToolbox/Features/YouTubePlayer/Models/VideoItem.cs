namespace DevToolbox.Features.YouTubePlayer.Models;

public class VideoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string YouTubeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int Position { get; set; }
    
    // Foreign Key
    public Guid PlaylistId { get; set; }
    public Playlist? Playlist { get; set; }

    public string Url => $"https://www.youtube.com/watch?v={YouTubeId}";
    public string EmbedUrl => $"https://www.youtube.com/embed/{YouTubeId}?enablejsapi=1&wmode=transparent";
}