using System.Text;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Notifications;
using OrganizaClub.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Notifications;

public sealed class DailyDigestService(
    IOrganizaClubDbContext db,
    IWhatsAppClient whatsAppClient,
    TimeProvider timeProvider)
{
    public const string RunKind = "daily-project-digest";

    public async Task<int> SendDueDigestsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var preferences = await db.NotificationPreferences
            .AsNoTracking()
            .Where(preference => preference.DailyDigestEnabled && preference.WhatsAppPhoneNumber != null)
            .Select(preference => new DailyDigestPreference(
                preference.SpaceId,
                preference.SpaceMemberId,
                preference.WhatsAppPhoneNumber!,
                preference.DailyDigestTime,
                preference.TimeZoneId))
            .ToArrayAsync(cancellationToken);

        var sent = 0;
        foreach (var preference in preferences)
        {
            var localNow = GetLocalNow(now, preference.TimeZoneId);
            if (localNow.TimeOfDay < preference.DailyDigestTime.ToTimeSpan() ||
                localNow.TimeOfDay >= preference.DailyDigestTime.AddMinutes(10).ToTimeSpan())
            {
                continue;
            }

            var localDate = DateOnly.FromDateTime(localNow.DateTime);
            var alreadySent = await db.NotificationRuns.AnyAsync(run =>
                run.SpaceId == preference.SpaceId &&
                run.SpaceMemberId == preference.SpaceMemberId &&
                run.Kind == RunKind &&
                run.LocalDate == localDate,
                cancellationToken);

            if (alreadySent)
            {
                continue;
            }

            var message = await BuildDigestAsync(preference.SpaceId, preference.SpaceMemberId, localDate, cancellationToken);
            if (message is null)
            {
                continue;
            }

            var result = await whatsAppClient.SendTextAsync(preference.WhatsAppPhoneNumber!, message, cancellationToken);
            db.NotificationRuns.Add(new NotificationRun
            {
                SpaceId = preference.SpaceId,
                SpaceMemberId = preference.SpaceMemberId,
                Kind = RunKind,
                LocalDate = localDate,
                SentAt = now,
                ProviderMessageId = result.ProviderMessageId
            });
            sent++;
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    private async Task<string?> BuildDigestAsync(Guid spaceId, Guid memberId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var activities = await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
                .ThenInclude(project => project!.Core)
            .Include(activity => activity.PendingItems)
            .Where(activity =>
                activity.SpaceId == spaceId &&
                activity.ResponsibleMemberId == memberId &&
                activity.Status != ActivityStatus.Concluido)
            .OrderByDescending(activity => activity.Priority)
            .ThenBy(activity => activity.Title)
            .ToArrayAsync(cancellationToken);

        if (activities.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Resumo diário do Organiza Club");
        builder.AppendLine(localDate.ToString("dd/MM/yyyy"));
        builder.AppendLine();

        foreach (var activity in activities)
        {
            builder.AppendLine($"- {activity.Title} ({activity.Project?.Core?.Name} / {activity.Project?.Name})");
            var openPending = activity.PendingItems
                .Where(item => !item.IsCompleted)
                .OrderBy(item => item.DueDate)
                .ThenByDescending(item => item.Priority)
                .Take(3)
                .ToArray();

            foreach (var pending in openPending)
            {
                var due = pending.DueDate is null ? "" : $" vence {pending.DueDate:dd/MM}";
                builder.AppendLine($"  - {pending.Title}{due}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static DateTimeOffset GetLocalNow(DateTimeOffset now, string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(now, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        }
    }

    private sealed record DailyDigestPreference(
        Guid SpaceId,
        Guid SpaceMemberId,
        string WhatsAppPhoneNumber,
        TimeOnly DailyDigestTime,
        string TimeZoneId);
}
