using System.Windows;
using DeviceMonitor.Application;
using Prism.Commands;
using Prism.Mvvm;

namespace DeviceMonitor.Shell.ViewModels;

/// <summary>
/// 登录窗口 ViewModel。
/// </summary>
public class LoginViewModel : BindableBase
{
    private readonly AuthService _auth;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
        LoginCommand = new DelegateCommand(async () => await LoginAsync());
    }

    private string _userName = "";
    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    private string _error = "";
    public string Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    /// <summary>登录成功标记（供窗口 code-behind 读取后关闭）。</summary>
    public bool LoginSucceeded { get; private set; }

    public DelegateCommand LoginCommand { get; }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            Error = "请输入用户名";
            return;
        }

        var pwd = Password;
        var result = await _auth.LoginAsync(UserName, pwd);
        if (result.Success)
        {
            LoginSucceeded = true;
            OnLoginSucceeded?.Invoke();
        }
        else
        {
            Error = result.Message;
            Password = "";
        }
    }

    /// <summary>密码由窗口 code-behind 通过 PasswordBox 注入（不在 VM 里存明文）。</summary>
    public string Password { get; set; } = "";

    /// <summary>登录成功后触发，供窗口关闭。</summary>
    public event Action? OnLoginSucceeded;
}
