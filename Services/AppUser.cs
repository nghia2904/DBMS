using System.Security.Claims;

namespace QuanLyThuVien.Services;

public static class AppUser
{
    public static int? MaTK(this ClaimsPrincipal user)
    {
        var s = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(s, out var id) ? id : null;
    }

    public static string Role(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role) ?? "";

    public static bool IsRole(this ClaimsPrincipal user, params string[] roles)
        => roles.Contains(user.Role());
}
