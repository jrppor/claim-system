using Claim.Infrastructure.Claims;

namespace Claim.Api.Tests;

/// <summary>The disk adapter on its own (the API tests use the in-memory fake): a write that fails must not leave a file behind.</summary>
public sealed class DiskFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claim-storage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>Yields some bytes, then fails, like a client or disk giving up half-way through a file.</summary>
    private sealed class BreakingStream : Stream
    {
        private bool _gaveBytes;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_gaveBytes) throw new IOException("connection lost");
            _gaveBytes = true;
            buffer.AsSpan(offset, Math.Min(count, 100)).Fill(7);
            return Math.Min(count, 100);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_write_that_throws_part_way_leaves_no_partial_file()
    {
        var storage = new DiskFileStorage(_root);

        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync("abc.jpg", new BreakingStream(), default));

        Assert.Empty(Directory.GetFiles(_root));
        Assert.Null(await storage.OpenReadAsync("abc.jpg", default));
    }

    [Fact]
    public async Task A_completed_write_can_be_read_back_and_a_name_that_already_exists_is_refused_without_touching_the_old_file()
    {
        var storage = new DiskFileStorage(_root);
        await storage.SaveAsync("abc.jpg", new MemoryStream([1, 2, 3]), default);

        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync("abc.jpg", new BreakingStream(), default));

        await using var stored = (await storage.OpenReadAsync("abc.jpg", default))!;
        using var copy = new MemoryStream();
        await stored.CopyToAsync(copy);
        Assert.Equal<byte>([1, 2, 3], copy.ToArray());
    }
}
