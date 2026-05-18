namespace Woola.PhotoManager.Tests.Domain;

public class PhotoTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var photo = Backend.Domain.Entities.Photo.Create(
            "C:\\fotos\\vacaciones.jpg", "abc123", 1024);

        Assert.Equal("C:\\fotos\\vacaciones.jpg", photo.Path);
        Assert.Equal("abc123", photo.Hash);
        Assert.Equal(1024, photo.FileSize);
        Assert.Equal("Discovered", photo.Status);
        Assert.Equal("vacaciones.jpg", photo.FileName);
    }

    [Fact]
    public void SoftDelete_ShouldMarkDeleted()
    {
        var photo = Backend.Domain.Entities.Photo.Create("test.jpg", "hash", 100);
        photo.SoftDelete();

        Assert.True(photo.IsDeleted);
        Assert.NotNull(photo.UpdatedAt);
    }

    [Fact]
    public void SetMetadata_ShouldUpdateFields()
    {
        var photo = Backend.Domain.Entities.Photo.Create("test.jpg", "hash", 100);
        var taken = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        photo.SetMetadata(taken, 1920, 1080, 41.3879, 2.16992,
            "Canon EOS R5", "RF 24-70", 2.8, 0.008, 400, 50, 1);

        Assert.Equal(taken, photo.DateTaken);
        Assert.Equal(1920, photo.Width);
        Assert.Equal(1080, photo.Height);
        Assert.Equal(41.3879, photo.Latitude);
        Assert.Equal(2.16992, photo.Longitude);
        Assert.Equal("Canon EOS R5", photo.CameraModel);
    }

    [Fact]
    public void MarkIndexed_ShouldUpdateStatus()
    {
        var photo = Backend.Domain.Entities.Photo.Create("test.jpg", "hash", 100);
        photo.MarkIndexed();

        Assert.Equal("Indexed", photo.Status);
        Assert.NotNull(photo.LastIndexedAt);
    }
}
