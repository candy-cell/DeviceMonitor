using System.Windows;
using System.Windows.Input;
using DeviceMonitor.Shell.ViewModels;

namespace DeviceMonitor.Shell.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    /// <summary>
    /// 登录窗口：完全手动装配（不用 AutoWireViewModel），保证时序可控。
    /// </summary>
    /// <param name="viewModel">由 App 从容器解析后传入。</param>
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _vm;

        _vm.OnLoginSucceeded += OnLoginSucceeded;

        // 加载完成后让用户名框获得焦点，方便直接输入
        Loaded += (_, _) => UserNameBox.Focus();
    }

    private void OnLoginSucceeded()
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// PasswordBox 的密码不进 ViewModel（避免在 VM 里存明文），
    /// 通过 PasswordChanged 事件把密码传给 VM。
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.Password = PasswordBox.Password;
    }

    /// <summary>密码框里按回车直接触发登录。</summary>
    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.LoginCommand.CanExecute())
        {
            _vm.LoginCommand.Execute();
        }
    }
}
