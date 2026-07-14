using HomePit.Domain.Households;
using HomePit.Domain.Integrations;

namespace HomePit.Application.Common;

public interface IUserContext
{
    Guid UserId { get; }
    SystemRole SystemRole { get; }
    Guid? HouseholdId { get; }
    bool IsIntegration => false;
    Guid? IntegrationConnectionId => null;
    IntegrationAccessMode? IntegrationAccessMode => null;
}
