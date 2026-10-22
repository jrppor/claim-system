using Claim.Application.Claims;

namespace Claim.Infrastructure.Claims;

public sealed class FileStorageOptions
{
    public const string Section = "FileStorage";

    /// <summary>Folder that holds the files. Must be outside wwwroot; relative paths are resolved against the app's content root.</summary>
    public string RootPath { get; set; } = "storage";
}

/// <summary>
/// Claim Photo bytes as plain files in one folder (ADR 0006). Callers pass GUID names; a name containing anything but a plain
/// file name is refused so a bug elsewhere can never read or write outside the folder.
/// </summary>
public sealed class DiskFileStorage(string rootPath) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(rootPath);

    public async Task SaveAsync(string name, Stream content, CancellationToken ct)
    {
        var path = PathOf(name);
        Directory.CreateDirectory(_root);
        // CreateNew throws if the name is taken, before anything is written, so the cleanup below can only remove our own file.
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            await content.CopyToAsync(file, ct);
            await file.DisposeAsync();
        }
        catch
        {
            // A write that dies part-way (source stream, full disk, cancel) must not leave a truncated file under a real name.
            await file.DisposeAsync();
            File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string name, CancellationToken ct)
    {
        var path = PathOf(name);
        return Task.FromResult<Stream?>(File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
    }

    public Task DeleteAsync(string name, CancellationToken ct)
    {
        File.Delete(PathOf(name));
        return Task.CompletedTask;
    }

    private string PathOf(string name)
    {
        if (name.Length == 0 || name != Path.GetFileName(name) || name is "." or "..")
            throw new ArgumentException("A stored file name must be a plain file name.", nameof(name));
        return Path.Combine(_root, name);
    }
}
