using OrganizaClub.Domain.Projects;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class ProjectDomainTests
{
    [Fact]
    public void Pending_item_reports_completion_from_completed_at()
    {
        var item = new PendingItem
        {
            SpaceId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Title = "Comprar tinta",
            CompletedAt = DateTimeOffset.UtcNow
        };

        Assert.True(item.IsCompleted);
    }

    [Fact]
    public void Activity_defaults_match_notion_workflow()
    {
        var activity = new Activity
        {
            SpaceId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Title = "Revisar orçamento"
        };

        Assert.Equal(ActivityStatus.NaoIniciada, activity.Status);
        Assert.Equal(Priority.Media, activity.Priority);
        Assert.Null(activity.DueDate);
        Assert.Null(activity.CompletedAt);
        Assert.Null(activity.ImageObjectKey);
        Assert.Null(activity.ImageContentType);
        Assert.Null(activity.ImageUpdatedAt);
    }
}
