using System.Security.Claims;

namespace YASS.Shared.Interfaces;

/// <summary>
/// 认证服务接口 - 预留用于后续接入 Keycloak OIDC
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 验证访问令牌
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <returns>验证结果</returns>
    Task<AuthResult> ValidateTokenAsync(string token);

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    /// <param name="principal">用户主体</param>
    /// <returns>用户信息</returns>
    Task<UserInfo?> GetUserInfoAsync(ClaimsPrincipal principal);

    /// <summary>
    /// 检查用户是否有权限访问房间
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roomId">房间ID</param>
    /// <param name="permission">权限类型</param>
    /// <returns>是否有权限</returns>
    Task<bool> HasPermissionAsync(string userId, string roomId, RoomPermission permission);
}

/// <summary>
/// 认证结果
/// </summary>
public class AuthResult
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Error { get; set; }
    public ClaimsPrincipal? Principal { get; set; }
}

/// <summary>
/// 用户信息
/// </summary>
public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public List<string> Roles { get; set; } = [];
}

/// <summary>
/// 房间权限
/// </summary>
public enum RoomPermission
{
    /// <summary>
    /// 查看
    /// </summary>
    View,

    /// <summary>
    /// 推流
    /// </summary>
    Publish,

    /// <summary>
    /// 管理
    /// </summary>
    Manage
}
