using OrganizaClub.Application.Common;
using OrganizaClub.Application.Images;
using OrganizaClub.Infrastructure.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class ImageSharpImageUploadProcessorTests
{
    private static readonly ImageUploadValidationMessages CommonMessages = new(
        "empty",
        "too-large",
        "invalid-type",
        "invalid-content",
        "animated");

    private static readonly ImageUploadValidationMessages SeoMessages = new(
        "empty",
        "too-large",
        "invalid-type",
        "invalid-content",
        "animated",
        "invalid-dimensions");

    [Fact]
    public async Task Common_policy_auto_orients_resizes_and_converts_to_webp()
    {
        var processor = new ImageSharpImageUploadProcessor();
        var jpeg = TestImageFactory.CreateJpeg(3000, 1000, orientation: 6);

        var prepared = await processor.PrepareAsync(
            new MemoryStream(jpeg),
            jpeg.Length,
            "image/jpeg",
            ImageUploadPolicies.Common,
            CommonMessages,
            CancellationToken.None);

        using var output = Image.Load<Rgba32>(prepared.Content);

        Assert.Equal("image/webp", prepared.ContentType);
        Assert.Equal(667, prepared.Width);
        Assert.Equal(2000, prepared.Height);
        Assert.Equal(667, output.Width);
        Assert.Equal(2000, output.Height);
    }

    [Fact]
    public async Task Common_policy_preserves_transparency_when_converting_png()
    {
        var processor = new ImageSharpImageUploadProcessor();
        var png = TestImageFactory.CreatePng(320, 180, transparent: true);

        var prepared = await processor.PrepareAsync(
            new MemoryStream(png),
            png.Length,
            "image/png",
            ImageUploadPolicies.Common,
            CommonMessages,
            CancellationToken.None);

        using var output = Image.Load<Rgba32>(prepared.Content);
        var pixel = output[0, 0];

        Assert.Equal("image/webp", prepared.ContentType);
        Assert.True(pixel.A < 255);
    }

    [Fact]
    public async Task Common_policy_does_not_upscale_small_images()
    {
        var processor = new ImageSharpImageUploadProcessor();
        var png = TestImageFactory.CreatePng(800, 600);

        var prepared = await processor.PrepareAsync(
            new MemoryStream(png),
            png.Length,
            "image/png",
            ImageUploadPolicies.Common,
            CommonMessages,
            CancellationToken.None);

        Assert.Equal(800, prepared.Width);
        Assert.Equal(600, prepared.Height);
    }

    [Fact]
    public async Task Common_policy_rejects_animated_gif()
    {
        var processor = new ImageSharpImageUploadProcessor();
        var gif = TestImageFactory.CreateAnimatedGif();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            processor.PrepareAsync(
                new MemoryStream(gif),
                gif.Length,
                "image/gif",
                ImageUploadPolicies.Common,
                CommonMessages,
                CancellationToken.None));

        Assert.Equal("animated", exception.Message);
    }

    [Fact]
    public async Task Common_policy_rejects_unsupported_content_type()
    {
        var processor = new ImageSharpImageUploadProcessor();
        var png = TestImageFactory.CreatePng();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            processor.PrepareAsync(
                new MemoryStream(png),
                png.Length,
                "image/tiff",
                ImageUploadPolicies.Common,
                CommonMessages,
                CancellationToken.None));

        Assert.Equal("invalid-type", exception.Message);
    }

    [Fact]
    public async Task Seo_policy_requires_exact_webp_dimensions_and_preserves_bytes()
    {
        var processor = new ImageSharpImageUploadProcessor();
        var webp = TestImageFactory.CreateWebp(1200, 630);

        var prepared = await processor.PrepareAsync(
            new MemoryStream(webp),
            webp.Length,
            "image/webp",
            ImageUploadPolicies.Seo,
            SeoMessages,
            CancellationToken.None);

        Assert.Equal("image/webp", prepared.ContentType);
        Assert.Equal(1200, prepared.Width);
        Assert.Equal(630, prepared.Height);
        Assert.Equal(webp, prepared.Content);
    }
}
