using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace HomePit.IntegrationTests;

internal static class TestImageFactory
{
    public static byte[] CreatePng(int width = 64, int height = 64, bool transparent = false)
    {
        using var image = new Image<Rgba32>(width, height, transparent
            ? new Rgba32(255, 0, 0, 120)
            : new Rgba32(255, 0, 0, 255));

        return Save(image, new PngEncoder());
    }

    public static byte[] CreateJpeg(int width = 64, int height = 64)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(255, 140, 0, 255));
        return Save(image, new JpegEncoder());
    }

    public static byte[] CreateWebp(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(0, 140, 255, 255));
        return Save(image, new WebpEncoder());
    }

    private static byte[] Save(Image image, IImageEncoder encoder)
    {
        using var buffer = new MemoryStream();
        image.Save(buffer, encoder);
        return buffer.ToArray();
    }
}
