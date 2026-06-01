using System.Text;
using HomePit.Application.Common;
using HomePit.Domain.Notifications;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Notifications;

public sealed class DailyDigestService(
    IHomePitDbContext db,
    IWhatsAppClient whatsAppClient,
    TimeProvider timeProvider)
{
    public const string RunKind = "daily-project-digest";

    public async Task<int> SendDueDigestsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var preferences = await db.NotificationPreferences
            .Include(preference => preference.HouseholdMember)
                .ThenInclude(member => member!.User)
            .Where(preference => preference.DailyDigestEnabled && preference.WhatsAppPhoneNumber != null)
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
                run.HouseholdId == preference.HouseholdId &&
                run.HouseholdMemberId == preference.HouseholdMemberId &&
                run.Kind == RunKind &&
                run.LocalDate == localDate,
                cancellationToken);

            if (alreadySent)
            {
                continue;
            }

            var message = await BuildDigestAsync(preference.HouseholdId, preference.HouseholdMemberId, localDate, cancellationToken);
            if (message is null)
            {
                continue;
            }

            var result = await whatsAppClient.SendTextAsync(preference.WhatsAppPhoneNumber!, message, cancellationToken);
            db.NotificationRuns.Add(new NotificationRun
            {
                HouseholdId = preference.HouseholdId,
                HouseholdMemberId = preference.HouseholdMemberId,
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

    private async Task<string?> BuildDigestAsync(Guid householdId, Guid memberId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var activities = await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
                .ThenInclude(project => project!.Universe)
            .Include(activity => activity.PendingItems)
            .Where(activity =>
                activity.HouseholdId == householdId &&
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
        builder.AppendLine("Resumo diário do HomePit");
        builder.AppendLine(localDate.ToString("dd/MM/yyyy"));
        builder.AppendLine();

        foreach (var activity in activities)
        {
            builder.AppendLine($"- {activity.Title} ({activity.Project?.Universe?.Name} / {activity.Project?.Name})");
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
}
