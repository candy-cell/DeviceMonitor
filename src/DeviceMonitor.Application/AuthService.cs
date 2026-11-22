using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Security;

namespace DeviceMonitor.Application;

/// <summary>
/// 认证与会话服务：登录校验、当前用户、权限判断、审计记录。
/// </summary>
/// <remarks>
/// 权限模型只有两级：
/// - <b>Admin</b>（管理员）：可登录、可改配置、可下发写操作；
/// - <b>Operator</b>（操作员）：可登录，但<b>只能看，不能写</b>。
///
/// 权限判断的核心就一个方法：<see cref="CanWrite"/>。
/// 上层在"下发参数 / 修改配置"前先调它，返回 false 就拒绝并记审计。
/// </remarks>
public sealed class AuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IAuditRepository _auditRepo;

    public AuthService(IUserRepository userRepo, IAuditRepository auditRepo)
    {
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
    }

    /// <summary>当前登录用户；未登录为 null。</summary>
    public UserAccount? CurrentUser { get; private set; }

    /// <summary>是否已登录。</summary>
    public bool IsLoggedIn => CurrentUser is not null;

    /// <summary>当前用户是否有写权限（只有管理员能写）。</summary>
    public bool CanWrite => CurrentUser?.IsAdmin == true;

    /// <summary>当前用户名（未登录返回空串）。</summary>
    public string CurrentUserName => CurrentUser?.UserName ?? string.Empty;

    /// <summary>登录状态变化时触发。</summary>
    public event Action? LoginStateChanged;

    /// <summary>
    /// 登录校验。
    /// </summary>
    /// <returns>登录结果。</returns>
    public async Task<LoginResult> LoginAsync(string userName, string password)
    {
        var user = await _userRepo.GetByUserNameAsync(userName.Trim());

        if (user is null || !user.Enabled)
        {
            return LoginResult.Fail("用户名或密码错误");
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash, user.Salt))
        {
            await WriteAuditAsync(userName, "Login", userName, "Fail:密码错误");
            return LoginResult.Fail("用户名或密码错误");
        }

        CurrentUser = user;
        user.LastLoginTime = DateTime.Now;
        await _userRepo.UpdateAsync(user);

        await WriteAuditAsync(userName, "Login", userName, "Success");
        LoginStateChanged?.Invoke();
        return LoginResult.Ok(user);
    }

    /// <summary>退出登录。</summary>
    public async Task LogoutAsync()
    {
        if (CurrentUser is not null)
        {
            await WriteAuditAsync(CurrentUser.UserName, "Logout", CurrentUser.UserName, "Success");
        }
        CurrentUser = null;
        LoginStateChanged?.Invoke();
    }

    /// <summary>
    /// 权限检查：无写权限时记审计并返回 false。
    /// 调用方应据此拒绝写操作，而不是抛异常。
    /// </summary>
    public async Task<bool> EnsureCanWriteAsync(string action, string? target = null)
    {
        if (CanWrite)
        {
            return true;
        }

        await WriteAuditAsync(CurrentUserName, action, target, "Fail:无写权限");
        return false;
    }

    private Task WriteAuditAsync(string userName, string action, string? target, string result)
    {
        return _auditRepo.AddAsync(new AuditLog
        {
            Timestamp = DateTime.Now,
            UserName = userName,
            Action = action,
            Target = target,
            Result = result
        });
    }
}

/// <summary>登录结果。</summary>
public sealed class LoginResult
{
    private LoginResult(bool success, string message, UserAccount? user)
    {
        Success = success;
        Message = message;
        User = user;
    }

    public bool Success { get; }
    public string Message { get; }
    public UserAccount? User { get; }

    public static LoginResult Ok(UserAccount user) => new(true, "登录成功", user);
    public static LoginResult Fail(string message) => new(false, message, null);
}
