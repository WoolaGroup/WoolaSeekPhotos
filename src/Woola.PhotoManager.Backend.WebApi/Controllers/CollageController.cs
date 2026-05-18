using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CollageController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public CollageController(WoolaDbContext db) => _db = db;

    [HttpGet("album/{albumId}")]
    public async Task<ActionResult> GenerateAlbumCollage(int albumId)
    {
        var photos = await _db.AlbumPhotos
            .Where(ap => ap.AlbumId == albumId)
            .Include(ap => ap.Photo)
            .OrderBy(ap => ap.SortOrder)
            .Take(4)
            .Select(ap => ap.Photo!.Path)
            .ToListAsync();

        if (photos.Count == 0)
            return NotFound("No photos in album");

        try
        {
            const int cellSize = 300;
            var gridSize = photos.Count <= 2 ? 2 : 2;
            var collageWidth = gridSize * cellSize;
            var collageHeight = gridSize * cellSize;

            using var collage = new Image<Rgba32>(collageWidth, collageHeight);

            for (int i = 0; i < Math.Min(photos.Count, 4); i++)
            {
                if (!System.IO.File.Exists(photos[i])) continue;

                using var img = await Image.LoadAsync(photos[i]);
                img.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(cellSize, cellSize),
                    Mode = ResizeMode.Crop
                }));

                var x = (i % gridSize) * cellSize;
                var y = (i / gridSize) * cellSize;
                collage.Mutate(ctx => ctx.DrawImage(img, new Point(x, y), 1f));
            }

            using var ms = new MemoryStream();
            await collage.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 80 });
            ms.Position = 0;

            return File(ms.ToArray(), "image/jpeg");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
