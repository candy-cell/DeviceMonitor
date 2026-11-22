using System.IO;
using System.Windows;
using DeviceMonitor.Alarms;
using DeviceMonitor.Application;
using DeviceMonitor.Communication;
using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Shell.ViewModels;
using DeviceMonitor.Shell.Views;
using DeviceMonitor.Storage;
using Microsoft.EntityFrameworkCore;
using Prism.DryIoc;
using Prism.Ioc;

namespace DeviceMonitor.Shell;

/// <summary>
/// Prism WPF 应用入口。
/// </summary>
/// <remarks>
/// 用 Prism 8.1.97（与你案例 Monitor_WPF 一致），避免 9.x 的破坏性改动。
/// 这里同时把前面五个项目（Core/Communication/Storage/Alarms/Application）的服务
/// 全部注册进 DryIoc 容器，实现真正的依赖注入。
/// </remarks>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // ---------- 数据库 ----------
        // 注册 DbContext 工厂（线程安全，可单例）。DbContext 本身不注册为单例——
        // 仓储会通过工厂按需创建独立实例，避免跨线程共享同一个 DbContext。
        var dbPath = @"D:\上位机\DeviceMonitor\data\DeviceMonitor.db";
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var dbFactory = new MonitorDbContextFactory(dbPath);
        containerRegistry.RegisterInstance<IDbContextFactory<MonitorDbContext>>(dbFactory);

        // ---------- 仓储 ----------
        containerRegistry.Register<IDeviceRepository, DeviceRepository>();
        containerRegistry.Register<ISampleRepository, SampleRepository>();
        containerRegistry.Register<IAlarmRepository, AlarmRepository>();
        containerRegistry.Register<IUserRepository, UserRepository>();
        containerRegistry.Register<IAuditRepository, AuditRepository>();

        // ---------- 认证服务 ----------
        containerRegistry.RegisterSingleton<AuthService>();

        // ---------- 通信（用可切换工厂，运行时能切模拟/真实）----------
        containerRegistry.RegisterSingleton<IDeviceClientFactory, SwitchableDeviceClientFactory>();
        containerRegistry.RegisterSingleton<IModbusLogSink, ConsoleModbusLogSink>();

        // ---------- 报警 ----------
        containerRegistry.RegisterSingleton<AlarmEngine>();

        // ---------- 采集服务 ----------
        containerRegistry.RegisterSingleton<AcquisitionService>();

        // ---------- 视图与 ViewModel ----------
        containerRegistry.RegisterForNavigation<MainWindow, MainWindowViewModel>();
        // LoginWindow 不用 RegisterForNavigation（它是模态登录窗，非导航页），
        // 直接 Register 后由 OnInitialized 手动 new 并传入 VM。
        containerRegistry.Register<LoginViewModel>();
    }

    protected override void OnInitialized()
    {
        // 初始化数据库结构 + 种子数据（首次启动空库时插入演示设备/测点/报警规则 + 默认账号）
        var factory = Container.Resolve<IDbContextFactory<MonitorDbContext>>();
        using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            DataSeeder.EnsureSeededAsync(db).GetAwaiter().GetResult();
        }

        base.OnInitialized();

        // 先弹登录窗口；未登录成功则直接退出应用
        var login = new LoginWindow(Container.Resolve<LoginViewModel>());
        if (login.ShowDialog() != true)
        {
            Shutdown();
        }
    }
}
