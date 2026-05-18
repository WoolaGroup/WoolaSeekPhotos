namespace Woola.PhotoManager.Backend.Domain.ValueObjects;

public record PhotoFile(string Path, string Hash, long FileSize)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Extension => System.IO.Path.GetExtension(Path).ToLowerInvariant();
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}
