using HomePit.Application.Common;
using HomePit.Application.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace HomePit.Infrastructure.Images;

public sealed class ImageSharpImageUploadProcessor : IImageUploadProcessor
{
    private static readonly WebpEncoder WebpEncoder = new()
    {
        Quality = 75
    };

    public async Task<PreparedImageUpload> PrepareAsync(
        Stream content,
        long contentLength,
        string? contentType,
        ImageUploadPolicy policy,
        ImageUploadValidationMessages messages,
        CancellationToken cancellationToken)
    {
        if (contentLength <= 0)
        {
            throw new ValidationException(messages.EmptyContent);
        }

        if (contentLength > policy.MaxBytes)
        {
            throw new ValidationException(messages.TooLarge);
        }

        var normalizedContentType = NormalizeContentType(contentType, policy, messages);
        var buffer = await ReadBufferAsync(content, cancellationToken);

        try
        {
            if (!policy.ConvertToWebp)
            {
                return await ValidatePassthroughAsync(buffer, normalizedContentType, policy, messages, cancellationToken);
            }

            return await ConvertToWebpAsync(buffer, policy, messages, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw new ValidationException(messages.InvalidContentType);
        }
        catch (InvalidImageContentException)
        {
            throw new ValidationException(messages.InvalidContent);
        }
        catch (ImageFormatException)
        {
            throw new ValidationException(messages.InvalidContent);
        }
        catch (NotSupportedException)
        {
            throw new ValidationException(messages.InvalidContent);
        }
    }

    private static string NormalizeContentType(string? contentType, ImageUploadPolicy policy, ImageUploadValidationMessages messages)
    {
        var normalized = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim().ToLowerInvariant();
        if (normalized is null || !policy.AllowedContentTypes.Contains(normalized))
        {
            throw new ValidationException(messages.InvalidContentType);
        }

        return normalized;
    }

    private static async Task<PreparedImageUpload> ValidatePassthroughAsync(
        byte[] buffer,
        string contentType,
        ImageUploadPolicy policy,
        ImageUploadValidationMessages messages,
        CancellationToken cancellationToken)
    {
        await using var input = new MemoryStream(buffer, writable: false);
        var info = await Image.IdentifyAsync(input, cancellationToken);
        if (info is null)
        {
            throw new ValidationException(messages.InvalidContent);
        }

        if (policy.RejectAnimated && info.FrameMetadataCollection.Count > 1)
        {
            throw new ValidationException(messages.AnimatedNotAllowed);
        }

        EnsureRequiredDimensions(info.Width, info.Height, policy, messages);

        return new PreparedImageUpload(buffer, contentType, info.Width, info.Height);
    }

    private static async Task<PreparedImageUpload> ConvertToWebpAsync(
        byte[] buffer,
        ImageUploadPolicy policy,
        ImageUploadValidationMessages messages,
        CancellationToken cancellationToken)
    {
        await using var input = new MemoryStream(buffer, writable: false);
        using var image = await Image.LoadAsync(input, cancellationToken);

        if (policy.RejectAnimated && image.Frames.Count > 1)
        {
            throw new ValidationException(messages.AnimatedNotAllowed);
        }

        image.Mutate(context =>
        {
            context.AutoOrient();

            if (policy.MaxWidth.HasValue && policy.MaxHeight.HasValue)
            {
                var resizeSize = CalculateResize(image.Width, image.Height, policy.MaxWidth.Value, policy.MaxHeight.Value);
                if (resizeSize is not null)
                {
                    context.Resize(resizeSize.Value.Width, resizeSize.Value.Height);
                }
            }
        });

        StripMetadata(image);
        EnsureRequiredDimensions(image.Width, image.Height, policy, messages);

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, WebpEncoder, cancellationToken);
        return new PreparedImageUpload(output.ToArray(), "image/webp", image.Width, image.Height);
    }

    private static (int Width, int Height)? CalculateResize(int width, int height, int maxWidth, int maxHeight)
    {
        if (width <= maxWidth && height <= maxHeight)
        {
            return null;
        }

        var ratio = Math.Min((double)maxWidth / width, (double)maxHeight / height);
        return (
            Width: Math.Max(1, (int)Math.Round(width * ratio, MidpointRounding.AwayFromZero)),
            Height: Math.Max(1, (int)Math.Round(height * ratio, MidpointRounding.AwayFromZero)));
    }

    private static void EnsureRequiredDimensions(int width, int height, ImageUploadPolicy policy, ImageUploadValidationMessages messages)
    {
        if (policy.RequiredWidth.HasValue &&
            policy.RequiredHeight.HasValue &&
            (width != policy.RequiredWidth.Value || height != policy.RequiredHeight.Value))
        {
            throw new ValidationException(messages.InvalidDimensions ?? messages.InvalidContent);
        }
    }

    private static void StripMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.CicpProfile = null;
    }

    private static async Task<byte[]> ReadBufferAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
