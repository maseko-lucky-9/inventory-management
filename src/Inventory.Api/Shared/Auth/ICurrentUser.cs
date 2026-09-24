namespace Inventory.Api.Shared.Auth;

/// <summary>The authenticated caller, as the scoped stores need it: only the id their link join matches on (ADR-006).</summary>
public interface ICurrentUser
{
    long UserId { get; }
}
