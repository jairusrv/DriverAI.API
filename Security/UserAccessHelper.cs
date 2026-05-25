using System.Security.Claims;

namespace DriverAI.API.Security;

public static class UserAccessHelper
{
    public static int? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(value, out var userId))
        {
            return userId;
        }

        return null;
    }

    public static bool IsAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole("Admin");
    }

    public static bool CanAccessUser(ClaimsPrincipal user, int targetUserId)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var currentUserId = GetUserId(user);

        return currentUserId == targetUserId;
    }
}