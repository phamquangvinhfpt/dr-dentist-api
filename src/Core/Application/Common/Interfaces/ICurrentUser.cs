using System.Security.Claims;

namespace FSH.WebApi.Application.Common.Interfaces;

public interface ICurrentUser
{
    string? Name { get; }

    Guid GetUserId();

    string? GetUserEmail();

    string? GetTenant();

    bool IsAuthenticated();

    bool IsInRole(string role);
    string GetRole();

    IEnumerable<Claim>? GetUserClaims();
    void SetCurrentUser(ClaimsPrincipal user);
}