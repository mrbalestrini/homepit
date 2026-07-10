using HomePit.Application.Storage;
using HomePit.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Common;

public sealed class HomePitDataPurgeService(
    IHomePitDbContext db,
    IObjectStorage objectStorage)
{
    public async Task DeleteHouseholdAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var household = await db.Households
            .FirstOrDefaultAsync(item => item.Id == householdId, cancellationToken);

        if (household is null)
        {
            return;
        }

        var objectKeys = (await Task.WhenAll(
                db.Universes
                    .AsNoTracking()
                    .Where(item => item.HouseholdId == householdId && item.ImageObjectKey != null)
                    .Select(item => item.ImageObjectKey!)
                    .ToArrayAsync(cancellationToken),
                db.Activities
                    .AsNoTracking()
                    .Where(item => item.HouseholdId == householdId && item.ImageObjectKey != null)
                    .Select(item => item.ImageObjectKey!)
                    .ToArrayAsync(cancellationToken),
                db.Prompts
                    .AsNoTracking()
                    .Where(item => item.HouseholdId == householdId && item.ImageObjectKey != null)
                    .Select(item => item.ImageObjectKey!)
                    .ToArrayAsync(cancellationToken)))
            .SelectMany(item => item)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var managedActivityIds = await db.Activities
            .AsNoTracking()
            .Where(item => item.HouseholdId == householdId && item.ImageObjectKey != null)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var managedPromptIds = await db.Prompts
            .AsNoTracking()
            .Where(item => item.HouseholdId == householdId && item.ImageObjectKey != null)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        await DeleteObjectKeysAsync(objectKeys, cancellationToken);

        if (managedActivityIds.Length > 0 || managedPromptIds.Length > 0)
        {
            var managedImageAssets = await db.UserPlanImageAssets
                .Where(item =>
                    (item.Module == PlanImageAssetModule.Activity && managedActivityIds.Contains(item.EntityId)) ||
                    (item.Module == PlanImageAssetModule.Prompt && managedPromptIds.Contains(item.EntityId)))
                .ToArrayAsync(cancellationToken);

            if (managedImageAssets.Length > 0)
            {
                db.UserPlanImageAssets.RemoveRange(managedImageAssets);
            }
        }

        await DeleteActivityCommentsAsync(
            db.ActivityComments.Where(comment => comment.HouseholdId == householdId),
            cancellationToken);

        db.Households.Remove(household);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        var memberships = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Select(member => new MembershipSnapshot(member.Id, member.HouseholdId, member.Role, member.IsActive))
            .ToArrayAsync(cancellationToken);

        var membershipIds = memberships
            .Select(member => member.Id)
            .ToArray();

        if (membershipIds.Length > 0)
        {
            await DeleteActivityCommentsAsync(
                db.ActivityComments.Where(comment => membershipIds.Contains(comment.AuthorMemberId)),
                cancellationToken);
        }

        var ownedHouseholdIds = await db.Households
            .AsNoTracking()
            .Where(household => household.CreatedByUserId == userId)
            .Select(household => household.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var householdId in ownedHouseholdIds)
        {
            await DeleteHouseholdAsync(householdId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(user.ProfilePhotoObjectKey))
        {
            await objectStorage.DeleteAsync(user.ProfilePhotoObjectKey, cancellationToken);
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteObjectKeysAsync(IEnumerable<string?> objectKeys, CancellationToken cancellationToken)
    {
        foreach (var objectKey in objectKeys
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal))
        {
            await objectStorage.DeleteAsync(objectKey!, cancellationToken);
        }
    }

    private async Task DeleteActivityCommentsAsync(
        IQueryable<Domain.Projects.ActivityComment> query,
        CancellationToken cancellationToken)
    {
        if (db is DbContext dbContext && !string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            await query.ExecuteDeleteAsync(cancellationToken);
            return;
        }

        var comments = await query.ToArrayAsync(cancellationToken);
        if (comments.Length == 0)
        {
            return;
        }

        db.ActivityComments.RemoveRange(comments);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record MembershipSnapshot(
        Guid Id,
        Guid HouseholdId,
        Domain.Households.HouseholdRole Role,
        bool IsActive);
}
