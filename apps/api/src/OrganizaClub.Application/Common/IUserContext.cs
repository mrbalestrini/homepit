using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;

namespace OrganizaClub.Application.Common;

public interface IUserContext
{
    Guid UserId { get; }
    SystemRole SystemRole { get; }
    Guid? SpaceId { get; }
    bool IsIntegration => false;
    Guid? IntegrationConnectionId => null;
    IntegrationAccessMode? IntegrationAccessMode => null;
}
