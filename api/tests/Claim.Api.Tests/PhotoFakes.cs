using Claim.Application.Claims;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

/// <summary>In-memory file storage. Tests look at <see cref="Names"/> to see what is (still) stored.</summary>
public sealed class FakeFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly object _lock = new();

    public IReadOnlyCollection<string> Names { get { lock (_lock) return _files.Keys.ToList(); } }

    public byte[]? Bytes(string name)
    {
        lock (_lock) return _files.TryGetValue(name, out var b) ? b : null;
    }

    public async Task SaveAsync(string name, Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        lock (_lock) _files[name] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string name, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult<Stream?>(_files.TryGetValue(name, out var b) ? new MemoryStream(b) : null);
    }

    public Task DeleteAsync(string name, CancellationToken ct)
    {
        lock (_lock) _files.Remove(name);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory Claim Photos. Also what the fake Unit of Work appends to.</summary>
public sealed class FakeClaimPhotoRepository : IClaimPhotoRepository
{
    private readonly List<ClaimPhoto> _photos = [];
    private readonly object _lock = new();

    /// <summary>Makes the next <see cref="Append"/> throw, as if the database failed after the file was already stored.</summary>
    public bool FailNextAppend { get; set; }

    public IReadOnlyList<ClaimPhoto> All { get { lock (_lock) return _photos.ToList(); } }

    public ClaimPhoto Append(ClaimPhoto photo)
    {
        lock (_lock)
        {
            if (FailNextAppend)
            {
                FailNextAppend = false;
                throw new InvalidOperationException("database failed");
            }
            var stored = photo.Stored(_photos.Count + 1);
            _photos.Add(stored);
            return stored;
        }
    }

    public Task<IReadOnlyList<ClaimPhoto>> ListAsync(long claimId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<ClaimPhoto>>(_photos.Where(p => p.ClaimId == claimId).ToList());
    }
}
