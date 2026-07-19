using OrganizaClub.Application.Common;
using OrganizaClub.Application.Images;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Plans;

public sealed class ManagedImageQuotaService(
    IOrganizaClubDbContext db,
    IObjectStorage objectStorage,
    IImageUploadProcessor imageUploadProcessor,
    CommercialPlanService commercialPlanService,
    TimeProvider timeProvider)
{
    private static readonly ImageUploadValidationMessages DegradedImageMessages = new(
        "Envie uma imagem com conteúdo.",
        "A imagem excede o tamanho máximo permitido.",
        "A imagem deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido.",
        "Imagens animadas não são aceitas.");

    public async Task RegisterManagedImageAsync(
        Guid userId,
        PlanImageAssetModule module,
        Guid entityId,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken)
    {
        var uploadedAt = timeProvider.GetUtcNow();
        var asset = await db.UserPlanImageAssets
            .FirstOrDefaultAsync(item => item.Module == module && item.EntityId == entityId, cancellationToken);

        if (asset is null)
        {
            asset = new UserPlanImageAsset
            {
                UserId = userId,
                Module = module,
                EntityId = entityId,
                ObjectKey = objectKey,
                ContentType = contentType,
                UploadedAt = uploadedAt
            };
            db.UserPlanImageAssets.Add(asset);
        }
        else
        {
            asset.UserId = userId;
            asset.ObjectKey = objectKey;
            asset.ContentType = contentType;
            asset.UploadedAt = uploadedAt;
            asset.IsDegraded = false;
            asset.DegradedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        await EnforceQuotaAsync(userId, cancellationToken);
    }

    public async Task DeleteManagedImageAsync(
        PlanImageAssetModule module,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var asset = await db.UserPlanImageAssets
            .FirstOrDefaultAsync(item => item.Module == module && item.EntityId == entityId, cancellationToken);

        if (asset is null)
        {
            return;
        }

        db.UserPlanImageAssets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteManagedImagesAsync(
        PlanImageAssetModule module,
        IReadOnlyCollection<Guid> entityIds,
        CancellationToken cancellationToken)
    {
        var normalizedIds = entityIds
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedIds.Length == 0)
        {
            return;
        }

        var assets = await db.UserPlanImageAssets
            .Where(item => item.Module == module && normalizedIds.Contains(item.EntityId))
            .ToArrayAsync(cancellationToken);

        if (assets.Length == 0)
        {
            return;
        }

        db.UserPlanImageAssets.RemoveRange(assets);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnforceQuotaAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plan = await commercialPlanService.ResolveEffectivePlanDefinitionAsync(userId, cancellationToken);
        var assets = await db.UserPlanImageAssets
            .Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);

        var assetsToDegrade = assets
            .OrderByDescending(item => item.UploadedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Skip(plan.MaxOriginalImages)
            .Where(item => !item.IsDegraded)
            .ToArray();

        if (assetsToDegrade.Length == 0)
        {
            return;
        }

        foreach (var asset in assetsToDegrade)
        {
            StoredObject storedObject;

            try
            {
                storedObject = await objectStorage.GetAsync(asset.ObjectKey, cancellationToken);
            }
            catch (NotFoundException)
            {
                db.UserPlanImageAssets.Remove(asset);
                continue;
            }

            await using var input = new MemoryStream(storedObject.Content, writable: false);
            var degraded = await imageUploadProcessor.PrepareAsync(
                input,
                storedObject.Content.LongLength,
                storedObject.ContentType,
                ImageUploadPolicies.Degraded,
                DegradedImageMessages,
                cancellationToken);

            await using var output = new MemoryStream(degraded.Content, writable: false);
            await objectStorage.PutAsync(
                new ObjectStoragePutRequest(asset.ObjectKey, output, degraded.ContentLength, degraded.ContentType),
                cancellationToken);

            asset.ContentType = degraded.ContentType;
            asset.IsDegraded = true;
            asset.DegradedAt = timeProvider.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
