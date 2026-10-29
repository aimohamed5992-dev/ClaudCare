using System.Security.Claims;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;

namespace ClaudCare.Api.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int Id { get; }
    UserRole Role { get; }
    int? BranchId { get; }
    string? Ip { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int Id => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw ApiException.Unauthorized("unauthenticated", "يجب تسجيل الدخول.");

    public UserRole Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var r)
        ? r
        : throw ApiException.Unauthorized("unauthenticated", "يجب تسجيل الدخول.");

    public int? BranchId => int.TryParse(Principal?.FindFirstValue(ClaimNames.BranchId), out var b) ? b : null;

    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
