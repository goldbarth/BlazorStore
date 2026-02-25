using BlazorStore.Features.YouTubePlayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorStore.Data.EntityMapping;

public class VideoItemMapping : IEntityTypeConfiguration<VideoItem>
{
    public void Configure(EntityTypeBuilder<VideoItem> builder)
    {
        builder.Property(p => p.YouTubeId)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(v => v.Title)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(v => v.ThumbnailUrl)
            .HasMaxLength(2000)
            .IsRequired();
    }
}