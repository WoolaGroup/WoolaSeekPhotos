using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ShareController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public ShareController(WoolaDbContext db) => _db = db;

    [HttpGet("album/{albumId}")]
    public async Task<ActionResult> ShareAlbum(int albumId)
    {
        var album = await _db.Albums
            .Include(a => a.AlbumPhotos)
            .ThenInclude(ap => ap.Photo)
            .FirstOrDefaultAsync(a => a.Id == albumId);

        if (album == null) return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var shareUrl = $"{baseUrl}/api/v1/share/album/{albumId}/view";

        return Ok(new
        {
            album = album.Name,
            photoCount = album.AlbumPhotos?.Count ?? 0,
            shareUrl,
            embedHtml = $"<iframe src=\"{shareUrl}\" width=\"800\" height=\"600\" frameborder=\"0\"></iframe>"
        });
    }

    [HttpGet("album/{albumId}/view")]
    [ResponseCache(Duration = 60)]
    public async Task<ActionResult> ViewAlbum(int albumId)
    {
        var album = await _db.Albums
            .Include(a => a.AlbumPhotos.OrderBy(ap => ap.SortOrder))
            .ThenInclude(ap => ap.Photo)
            .FirstOrDefaultAsync(a => a.Id == albumId);

        if (album == null) return NotFound("Album not found");

        var photos = album.AlbumPhotos?
            .Select(ap => ap.Photo)
            .Where(p => p != null && p.ThumbnailPath != null)
            .ToList() ?? new();

        var html = BuildGalleryHtml(album.Name, photos!);
        return Content(html, "text/html");
    }

    private static string BuildGalleryHtml(string albumName, List<Domain.Entities.Photo> photos)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine("<meta charset=\"utf-8\"/>");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/>");
        sb.Append("<title>").Append(System.Net.WebUtility.HtmlEncode(albumName)).AppendLine(" — Woola Photos</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("* { box-sizing:border-box; margin:0; padding:0; }");
        sb.AppendLine("body { font-family:system-ui,sans-serif; background:#1a1a2e; color:#e0e0e0; padding:20px; }");
        sb.AppendLine("h1 { margin-bottom:16px; font-size:24px; }");
        sb.AppendLine(".gallery { display:grid; grid-template-columns:repeat(auto-fill,minmax(200px,1fr)); gap:12px; }");
        sb.AppendLine(".photo { background:#16213e; border-radius:8px; overflow:hidden; }");
        sb.AppendLine(".photo img { width:100%; height:180px; object-fit:cover; display:block; }");
        sb.AppendLine(".photo .info { padding:8px; font-size:12px; color:#94a3b8; }");
        sb.AppendLine("</style></head><body>");
        sb.Append("<h1>").Append("📷 ").Append(System.Net.WebUtility.HtmlEncode(albumName)).AppendLine("</h1>");
        sb.AppendLine("<div class=\"gallery\">");

        foreach (var p in photos)
        {
            var thumbPath = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : "";
            var name = System.Net.WebUtility.HtmlEncode(p.FileName);
            sb.AppendLine("<div class=\"photo\">");
            sb.AppendLine($"<img src=\"{thumbPath}\" alt=\"{name}\" loading=\"lazy\" />");
            sb.AppendLine($"<div class=\"info\">{name}</div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }
}
