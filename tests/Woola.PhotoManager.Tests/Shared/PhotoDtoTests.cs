using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Tests.Shared;

public class PhotoDtoTests
{
    [Fact]
    public void FileSizeDisplay_ShouldFormatBytes()
    {
        var dto = new PhotoDto { FileSize = 500 };
        Assert.EndsWith("B", dto.FileSizeDisplay);
    }

    [Fact]
    public void FileSizeDisplay_ShouldFormatKB()
    {
        var dto = new PhotoDto { FileSize = 2048 };
        Assert.EndsWith("KB", dto.FileSizeDisplay);
    }

    [Fact]
    public void FileSizeDisplay_ShouldFormatMB()
    {
        var dto = new PhotoDto { FileSize = 5 * 1024 * 1024 };
        Assert.EndsWith("MB", dto.FileSizeDisplay);
    }

    [Fact]
    public void Resolution_ShouldFormat()
    {
        var dto = new PhotoDto { Width = 1920, Height = 1080 };
        Assert.Equal("1920x1080", dto.Resolution);
    }
}
