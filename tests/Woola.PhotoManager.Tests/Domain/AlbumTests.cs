namespace Woola.PhotoManager.Tests.Domain;

public class AlbumTests
{
    [Fact]
    public void Create_ShouldSetNameAndDescription()
    {
        var album = Backend.Domain.Entities.Album.Create("Vacaciones 2024", "Fotos de la playa");

        Assert.Equal("Vacaciones 2024", album.Name);
        Assert.Equal("Fotos de la playa", album.Description);
        Assert.Null(album.CoverPhotoId);
    }

    [Fact]
    public void Update_ShouldChangeName()
    {
        var album = Backend.Domain.Entities.Album.Create("Old", null);
        album.Update("New Name", "New desc");

        Assert.Equal("New Name", album.Name);
        Assert.Equal("New desc", album.Description);
    }

    [Fact]
    public void SetCover_ShouldAssignPhoto()
    {
        var album = Backend.Domain.Entities.Album.Create("Test", null);
        album.SetCover(42);

        Assert.Equal(42, album.CoverPhotoId);
    }

    [Fact]
    public void SoftDelete_ShouldSetFlag()
    {
        var album = Backend.Domain.Entities.Album.Create("Test", null);
        album.SoftDelete();

        Assert.True(album.IsDeleted);
    }
}
