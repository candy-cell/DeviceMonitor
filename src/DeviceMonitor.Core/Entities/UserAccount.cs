namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 系统用户。
/// </summary>
/// <remarks>
/// 密码以"哈希 + 盐"形式存储，绝不存明文。
/// 角色只分两级，够用即可：
///   Admin（管理员）：可改配置、可下发写操作；
///   Operator（操作员）：只能看，不能改配置、不能下发。
/// </remarks>
public class UserAccount
{
    public int Id { get; set; }

    /// <summary>登录名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码哈希（Base64）。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>盐值（Base64）。</summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>是否管理员。</summary>
    public bool IsAdmin { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>最后登录时间。</summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>显示名。</summary>
    public string? DisplayName { get; set; }
}
