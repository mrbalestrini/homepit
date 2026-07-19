using OrganizaClub.Application.Common;
using OrganizaClub.Application.Storage;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace OrganizaClub.Infrastructure.ObjectStorage;

public sealed class MinioObjectStorage(IMinioClient client, IOptions<ObjectStorageOptions> options) : IObjectStorage
{
    private readonly IMinioClient client = client;
    private readonly ObjectStorageOptions options = options.Value;

    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (!options.CreateBucketOnStartup)
        {
            return;
        }

        var bucketExistsArgs = new BucketExistsArgs()
            .WithBucket(options.BucketName);

        if (await client.BucketExistsAsync(bucketExistsArgs, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var makeBucketArgs = new MakeBucketArgs()
            .WithBucket(options.BucketName);

        await client.MakeBucketAsync(makeBucketArgs, cancellationToken).ConfigureAwait(false);
    }

    public async Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken)
    {
        var args = new PutObjectArgs()
            .WithBucket(options.BucketName)
            .WithObject(request.ObjectKey)
            .WithStreamData(request.Content)
            .WithObjectSize(request.ContentLength)
            .WithContentType(request.ContentType);

        await client.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey);

            var stat = await client.StatObjectAsync(statArgs, cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();

            var getArgs = new GetObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(buffer));

            await client.GetObjectAsync(getArgs, cancellationToken: cancellationToken).ConfigureAwait(false);

            return new StoredObject(
                objectKey,
                buffer.ToArray(),
                string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            throw new NotFoundException("Arquivo não encontrado.");
        }
        catch (BucketNotFoundException)
        {
            throw new NotFoundException("Bucket de arquivos não encontrado.");
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey);

            await client.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectNotFoundException)
        {
            throw new NotFoundException("Arquivo não encontrado.");
        }
        catch (BucketNotFoundException)
        {
            throw new NotFoundException("Bucket de arquivos não encontrado.");
        }
    }
}
