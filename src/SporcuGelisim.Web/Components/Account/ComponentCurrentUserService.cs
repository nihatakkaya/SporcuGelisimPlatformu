using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SporcuGelisim.Application.Services;

namespace SporcuGelisim.Web.Components.Account;

// Server circuits must use their revalidated identity, including after HttpContext is gone.
public sealed class ComponentCurrentUserService(AuthenticationStateProvider authentication, IHttpContextAccessor http) : ICurrentUserService
{
    private ClaimsPrincipal Principal
    {
        get
        {
            try
            {
                var state = authentication.GetAuthenticationStateAsync();
                return state.IsCompletedSuccessfully ? state.Result.User : new ClaimsPrincipal();
            }
            catch (InvalidOperationException)
            {
                // Normal HTTP endpoints do not have a component authentication state.
                return http.HttpContext?.User ?? new ClaimsPrincipal();
            }
        }
    }
    public Guid? UserId => Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public IReadOnlySet<string> Roles => Principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
