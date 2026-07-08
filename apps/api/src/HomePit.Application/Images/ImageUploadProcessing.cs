namespace HomePit.Application.Images;

public sealed record ImageUploadPolicy(
    IReadOnlySet<string> AllowedContentTypes,
    long MaxBytes,
    bool ConvertToWebp,
    int? MaxWidth,
    int? MaxHeight,
    int? RequiredWidth,
    int? RequiredHeight,
    bool RejectAnimated);

public sealed record ImageUploadValidationMessages(
    string EmptyContent,
    string TooLarge,
    string InvalidContentType,
    string InvalidContent,
    string AnimatedNotAllowed,
    string? InvalidDimensions = null);

public sealed record PreparedImageUpload(
    byte[] Content,
    string ContentType,
    int Width,
    int Height)
{
    public long ContentLength => Content.LongLength;
}

public interface IImageUploadProcessor
{
    Task<PreparedImageUpload> PrepareAsync(
        Stream content,
        long contentLength,
        string? contentType,
        ImageUploadPolicy policy,
        ImageUploadValidationMessages messages,
        CancellationToken cancellationToken);
}

public static class ImageUploadPolicies
{
    public const long CommonMaxBytes = 5 * 1024 * 1024;
    public const int CommonMaxDimension = 2000;
    public const long SeoMaxBytes = 600 * 1024;
    public const int SeoWidth = 1200;
    public const int SeoHeight = 630;

    public static readonly IReadOnlySet<string> CommonAllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/bmp"
    };

    public static readonly IReadOnlySet<string> SeoAllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/webp"
    };

    public static readonly ImageUploadPolicy Common = new(
        CommonAllowedContentTypes,
        CommonMaxBytes,
        ConvertToWebp: true,
        MaxWidth: CommonMaxDimension,
        MaxHeight: CommonMaxDimension,
        RequiredWidth: null,
        RequiredHeight: null,
        RejectAnimated: true);

    public static readonly ImageUploadPolicy Seo = new(
        SeoAllowedContentTypes,
        SeoMaxBytes,
        ConvertToWebp: false,
        MaxWidth: null,
        MaxHeight: null,
        RequiredWidth: SeoWidth,
        RequiredHeight: SeoHeight,
        RejectAnimated: false);
}
