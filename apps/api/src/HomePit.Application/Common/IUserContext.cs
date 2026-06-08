using HomePit.Domain.Households;

namespace HomePit.Application.Common;

public interface IUserContext
{
    Guid UserId { get; }
    SystemRole SystemRole { get; }
    Guid? HouseholdId { get; }
}
