using System.Security.Claims;
using YASS.Shared.Interfaces;

namespace YASS.Server.Api.Services;

/// <summary>
/// 默认认证服务实现（无认证，用于开发）
/// 后续可替换为 Keycloak OIDC 实现
/// </summary>
public class DefaultAuthService : IAuthService
{
    public Task<AuthResult> ValidateTokenAsync(string token)
    {
        // 开发模式：始终通过
        return Task.FromResult(new AuthResult
        {
            IsValid = true,
            UserId = "dev-user",
            UserName = "Developer"
        });
    }

    public Task<UserInfo?> GetUserInfoAsync(ClaimsPrincipal principal)
    {
        // 开发模式：返回默认用户
        return Task.FromResult<UserInfo?>(new UserInfo
        {
            Id = "dev-user",
            UserName = "Developer",
            DisplayName = "Developer User",
            Roles = ["admin"]
        });
    }

    public Task<bool> HasPermissionAsync(string userId, string roomId, RoomPermission permission)
    {
        // 开发模式：始终有权限
        return Task.FromResult(true);
    }
}
