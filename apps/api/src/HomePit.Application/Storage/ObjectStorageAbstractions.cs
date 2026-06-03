namespace HomePit.Application.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "homepit-assets";
    public bool UseSsl { get; set; }
    public bool CreateBucketOnStartup { get; set; } = true;
}

public sealed record ObjectStoragePutRequest(
    string ObjectKey,
    Stream Content,
    long ContentLength,
    string ContentType);

public sealed record StoredObject(
    string ObjectKey,
    byte[] Content,
    string ContentType);

public interface IObjectStorage
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken);
    Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken);
    Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public static class ObjectStorageKeys
{
    public static string UserProfilePhoto(Guid userId) => $"users/{userId:D}/profile-photo";
    public static string PromptImage(Guid promptId) => $"prompts/{promptId:D}/image";
}
