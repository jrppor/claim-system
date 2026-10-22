using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;
using SkiaSharp;

namespace Claim.Api.Tests;

/// <summary>The same host with the real image library instead of the fake, fed small real images.</summary>
public sealed class RealImagingApiFactory : ApiFactory
{
    protected override bool FakeThumbnails => false;
}

public class RealThumbnailTests : IClassFixture<RealImagingApiFactory>
{
    private readonly RealImagingApiFactory _factory;

    public RealThumbnailTests(RealImagingApiFactory factory)
    {
        _factory = factory;
        if (_factory.Policies.SearchAsync("AB-1001", default).Result.Count == 0)
            _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
    }

    private async Task<HttpClient> Staff()
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = "staff@claim.test", password = ApiFactory.Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateVersionedClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.OrangeRed);
        using var data = SKImage.FromBitmap(bitmap).Encode(format, 90);
        return data.ToArray();
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<(int Width, int Height)> UploadAndMeasureThumbnail(byte[] image, string fileName, string contentType)
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var staff = await Staff();
        var created = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(image, fileName, contentType));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var photoId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var thumbnail = await staff.GetAsync($"/claims/{claim.Id}/photos/{photoId}/thumbnail");
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal(contentType, thumbnail.Content.Headers.ContentType!.MediaType);
        using var decoded = SKBitmap.Decode(await thumbnail.Content.ReadAsByteArrayAsync());
        return (decoded.Width, decoded.Height);
    }

    [Fact]
    public async Task A_large_landscape_png_gets_a_thumbnail_whose_longest_side_is_320_keeping_the_proportions()
    {
        var size = await UploadAndMeasureThumbnail(Image(1000, 500, SKEncodedImageFormat.Png), "wide.png", "image/png");

        Assert.Equal((320, 160), size);
    }

    [Fact]
    public async Task An_image_smaller_than_320_px_is_not_enlarged()
    {
        var size = await UploadAndMeasureThumbnail(Image(100, 50, SKEncodedImageFormat.Jpeg), "small.jpg", "image/jpeg");

        Assert.Equal((100, 50), size);
    }

    [Fact]
    public async Task A_tall_webp_is_shrunk_by_its_height_and_stays_webp()
    {
        var size = await UploadAndMeasureThumbnail(Image(600, 1280, SKEncodedImageFormat.Webp), "tall.webp", "image/webp");

        Assert.Equal((150, 320), size);
    }

    /// <summary>Adds an EXIF block whose only entry is Orientation (6 = the phone was held upright, the pixels are stored sideways).</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, byte orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0, 0, 0, 8,
            0x00, 0x01, 0x01, 0x12, 0x00, 0x03, 0, 0, 0, 1, 0x00, orientation, 0, 0,
            0, 0, 0, 0,
        ];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    [Fact]
    public async Task A_photo_stored_sideways_with_an_exif_orientation_gets_an_upright_thumbnail()
    {
        var sideways = WithExifOrientation(Image(200, 100, SKEncodedImageFormat.Jpeg), 6);

        var size = await UploadAndMeasureThumbnail(sideways, "phone.jpg", "image/jpeg");

        Assert.Equal((100, 200), size);
    }

    [Fact]
    public async Task A_file_with_image_magic_bytes_but_a_broken_body_is_rejected_with_400_and_stores_nothing()
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var staff = await Staff();
        var filesBefore = _factory.Files.Names.ToHashSet();
        byte[] broken = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(broken, "broken.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("broken.png", problem.GetProperty("errors").GetProperty("file")[0].GetString());
        Assert.Equal(filesBefore, _factory.Files.Names.ToHashSet());
        Assert.DoesNotContain(_factory.Photos.All, p => p.ClaimId == claim.Id);
    }

    /// <summary>A small, real PNG whose header (IHDR) claims another size: a decompression bomb costs a few hundred bytes to send.</summary>
    private static byte[] PngDeclaring(int width, int height)
    {
        var png = Image(1, 1, SKEncodedImageFormat.Png);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);
        // The chunk's CRC covers its type and data (bytes 12..28); decoders refuse the header when it does not match.
        var crc = 0xFFFFFFFFu;
        foreach (var b in png.AsSpan(12, 17))
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29, 4), ~crc);
        return png;
    }

    [Theory]
    [InlineData(10_000, 10_000)] // 100 megapixels: over the pixel cap though each side is fine
    [InlineData(50_000, 10)] // few pixels but a side over the cap
    public async Task An_image_whose_header_declares_huge_dimensions_is_rejected_with_400_naming_the_file_before_it_is_decoded(int width, int height)
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var staff = await Staff();
        var filesBefore = _factory.Files.Names.ToHashSet();
        var bomb = PngDeclaring(width, height);
        Assert.True(bomb.Length < 1000);

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(bomb, "bomb.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reason = problem.GetProperty("errors").GetProperty("file")[0].GetString();
        Assert.Contains("bomb.png", reason);
        Assert.Contains("too large", reason);
        Assert.Equal("bomb.png", problem.GetProperty("fileName").GetString());
        Assert.Equal(filesBefore, _factory.Files.Names.ToHashSet());
        Assert.DoesNotContain(_factory.Photos.All, p => p.ClaimId == claim.Id);
    }

    [Fact]
    public async Task An_image_just_inside_the_pixel_cap_is_still_accepted()
    {
        // 7000 x 7000 = 49 megapixels declared (the body holds a single pixel; the decoder fills in the rest).
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", Upload(PngDeclaring(7000, 7000), "big.png", "image/png"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
