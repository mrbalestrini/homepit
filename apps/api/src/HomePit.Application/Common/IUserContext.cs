namespace HomePit.Application.Common;

public interface IUserContext
{
    Guid UserId { get; }
    Guid? HouseholdId { get; }
}
