using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain.Entities;

namespace Woola.PhotoManager.Backend.Infrastructure.Data;

public class WoolaDbContext : DbContext
{
    public WoolaDbContext(DbContextOptions<WoolaDbContext> options) : base(options) { }

    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Face> Faces => Set<Face>();
    public DbSet<PhotoTag> PhotoTags => Set<PhotoTag>();
    public DbSet<AlbumPhoto> AlbumPhotos => Set<AlbumPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Photo>(entity =>
        {
            entity.ToTable("Photos");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Hash).IsUnique();
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => new { e.DateTaken, e.CreatedAt }).HasDatabaseName("idx_photos_sort");
            entity.HasIndex(e => e.CameraModel);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Latitude, e.Longitude });
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(e => e.Path).IsRequired();
            entity.Property(e => e.Hash).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Albums");
            entity.HasKey(e => e.Id);
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.CoverPhoto)
                  .WithMany()
                  .HasForeignKey(e => e.CoverPhotoId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).HasMaxLength(50);
        });

        modelBuilder.Entity<Face>(entity =>
        {
            entity.ToTable("Faces");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PersonName);
            entity.HasIndex(e => e.PhotoId);
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(e => e.Photo)
                  .WithMany(p => p.Faces)
                  .HasForeignKey(e => e.PhotoId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Encoding).HasColumnType("BLOB");
        });

        modelBuilder.Entity<PhotoTag>(entity =>
        {
            entity.ToTable("PhotoTags");
            entity.HasKey(e => new { e.PhotoId, e.TagId });
            entity.HasOne(e => e.Photo)
                  .WithMany(p => p.PhotoTags)
                  .HasForeignKey(e => e.PhotoId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tag)
                  .WithMany(t => t.PhotoTags)
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlbumPhoto>(entity =>
        {
            entity.ToTable("AlbumPhotos");
            entity.HasKey(e => new { e.AlbumId, e.PhotoId });
            entity.HasOne(e => e.Album)
                  .WithMany(a => a.AlbumPhotos)
                  .HasForeignKey(e => e.AlbumId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Photo)
                  .WithMany()
                  .HasForeignKey(e => e.PhotoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
