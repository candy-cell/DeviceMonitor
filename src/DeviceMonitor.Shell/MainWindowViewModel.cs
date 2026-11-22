using System.Collections.ObjectModel;
using DeviceMonitor.Application;
using DeviceMonitor.Communication;
using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using Prism.Commands;
using Prism.Mvvm;

namespace DeviceMonitor.Shell;

/// <summary>
/// 主窗口 ViewModel。
/// </summary>
public class MainWindowViewModel : BindableBase
{
    private readonly AcquisitionService _acquisition;
    private readonly AuthService _auth;
    private readonly IDeviceRepository _deviceRepo;

    public MainWindowViewModel(
        AcquisitionService acquisition,
        AuthService auth,
        IDeviceRepository deviceRepo)
    {
        _acquisition = acquisition;
        _auth = auth;
        _deviceRepo = deviceRepo;

        _acquisition.DataReceived += OnDataReceived;
        _acquisition.AlarmChanged += OnAlarmChanged;
        _acquisition.DeviceStatusChanged += OnDeviceStatusChanged;
        _auth.LoginStateChanged += OnLoginStateChanged;

        StartCommand = new DelegateCommand(async () => await StartAsync());
        StopCommand = new DelegateCommand(async () => await _acquisition.StopAsync());
        // 演示"写操作"的权限拦截：操作员点了会被拒
        WriteDemoCommand = new DelegateCommand(async () => await WriteDemoAsync());
        // 切换模拟/真实模式
        ToggleModeCommand = new DelegateCommand(() =>
        {
            SwitchableDeviceClientFactory.IsSimulation = !SwitchableDeviceClientFactory.IsSimulation;
            RaisePropertyChanged(nameof(ModeText));
            RaisePropertyChanged(nameof(IsSimulation));
            StatusText = SwitchableDeviceClientFactory.IsSimulation
                ? "已切换到【模拟模式】"
                : "已切换到【真实模式】（需先停止采集再开始才生效）";
        });

        RefreshLoginState();
        _ = LoadDeviceAsync();
    }

    // ==================== 采集控制 ====================

    private async Task StartAsync()
    {
        // 重新加载设备（串口号可能刚改过）
        await LoadDeviceAsync();
        await _acquisition.StartAsync();
        StatusText = "采集已启动";
    }

    private async Task WriteDemoAsync()
    {
        if (!await _auth.EnsureCanWriteAsync("WriteDemo", "演示参数下发"))
        {
            StatusText = "拒绝：当前用户无写权限（需要管理员）";
            return;
        }
        StatusText = "已下发演示参数（管理员权限通过）";
    }

    // ==================== 设备配置 ====================

    private async Task LoadDeviceAsync()
    {
        var devices = await _deviceRepo.GetAllAsync();
        var first = devices.FirstOrDefault();
        if (first is not null)
        {
            PortName = first.PortName ?? "";
            SlaveId = first.SlaveId;
            BaudRate = first.BaudRate;
            DataBits = first.DataBits;
            StopBits = first.StopBits;
            Parity = first.Parity;
        }
    }

    private string _portName = "COM1";
    public string PortName
    {
        get => _portName;
        set => SetProperty(ref _portName, value);
    }

    private byte _slaveId = 1;
    public byte SlaveId
    {
        get => _slaveId;
        set => SetProperty(ref _slaveId, value);
    }

    private int _baudRate = 9600;
    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    private int _dataBits = 8;
    public int DataBits
    {
        get => _dataBits;
        set => SetProperty(ref _dataBits, value);
    }

    private int _stopBits = 1;
    public int StopBits
    {
        get => _stopBits;
        set => SetProperty(ref _stopBits, value);
    }

    private int _parity = 0;
    public int Parity
    {
        get => _parity;
        set => SetProperty(ref _parity, value);
    }

    // 下拉框选项集合（ComboBox 用 SelectedItem 双向绑定）
    public int[] BaudRateOptions { get; } = { 1200, 2400, 4800, 9600, 19200, 38400, 115200 };
    public int[] DataBitsOptions { get; } = { 8, 7 };
    public int[] StopBitsOptions { get; } = { 1, 2 };

    // 校验位：用下标绑定（Parity 值恰好 = 下标：0=None,1=Odd,2=Even）
    public string[] ParityOptions { get; } = { "None（无校验）", "Odd（奇校验）", "Even（偶校验）" };

    private DelegateCommand? _saveConfigCommand;
    public DelegateCommand SaveConfigCommand =>
        _saveConfigCommand ??= new DelegateCommand(async () => await SaveConfigAsync());

    private async Task SaveConfigAsync()
    {
        var devices = await _deviceRepo.GetAllAsync();
        var first = devices.FirstOrDefault();
        if (first is null)
        {
            StatusText = "数据库中没有设备，请先运行种子数据";
            return;
        }

        first.PortName = PortName;
        first.SlaveId = SlaveId;
        first.BaudRate = BaudRate;
        first.DataBits = DataBits;
        first.StopBits = StopBits;
        first.Parity = Parity;
        await _deviceRepo.UpdateAsync(first);

        StatusText = $"已保存：{PortName}，从站{SlaveId}，{BaudRate}/{DataBits}/{StopBits}/{ParityOptions[Parity]}";
    }

    // ==================== 权限 / 登录状态 ====================

    private void RefreshLoginState()
    {
        CurrentUserName = _auth.IsLoggedIn ? _auth.CurrentUserName : "未登录";
        RoleText = _auth.CanWrite ? "管理员（可写）" : "操作员（只读）";
        CanWrite = _auth.CanWrite;
    }

    private void OnLoginStateChanged() => RefreshLoginState();

    private string _currentUserName = "";
    public string CurrentUserName
    {
        get => _currentUserName;
        set => SetProperty(ref _currentUserName, value);
    }

    private string _roleText = "";
    public string RoleText
    {
        get => _roleText;
        set => SetProperty(ref _roleText, value);
    }

    private bool _canWrite;
    public bool CanWrite
    {
        get => _canWrite;
        set => SetProperty(ref _canWrite, value);
    }

    // ==================== 数据回调 ====================

    private void OnDataReceived(DataPoint point, double value, DateTime time)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            var row = Items.FirstOrDefault(i => i.PointId == point.Id);
            if (row == null)
            {
                row = new PointRow { PointId = point.Id, Name = point.Name, Unit = point.Unit ?? "" };
                Items.Add(row);
            }
            row.Value = value;
            row.Time = time;
        });
    }

    private void OnAlarmChanged(AlarmRecord record)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            Alarms.Insert(0, new AlarmRow
            {
                Message = record.Message,
                State = record.State.ToString(),
                Level = record.Level.ToString(),
                Time = record.StartTime
            });
            StatusText = $"报警：{record.Message}";
        });
    }

    // ==================== 连接状态 ====================

    private void OnDeviceStatusChanged(DeviceStatusChanged status)
    {
        // 采集线程回调，切回 UI 线程
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            if (status.Online)
            {
                ConnectionText = $"设备 {status.DeviceName} 已恢复连接";
                IsConnected = true;
                StatusText = $"✅ {status.DeviceName} 恢复正常";
            }
            else
            {
                ConnectionText = $"⚠️ 设备 {status.DeviceName} 已断开（{status.Reason}）";
                IsConnected = false;
                StatusText = $"❌ {status.DeviceName} 断开：{status.Reason}";

                // 弹窗提醒（非模态，不阻塞采集）
                System.Windows.MessageBox.Show(
                    $"设备【{status.DeviceName}】连接已断开！\n\n原因：{status.Reason}\n\n请检查串口/网线连接。",
                    "设备断开连接",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

                // 声音提醒（系统提示音）
                System.Media.SystemSounds.Exclamation.Play();
            }
        });
    }

    private string _connectionText = "未连接";
    public string ConnectionText
    {
        get => _connectionText;
        set => SetProperty(ref _connectionText, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>是否模拟模式（绑定用）。</summary>
    public bool IsSimulation => SwitchableDeviceClientFactory.IsSimulation;

    /// <summary>当前运行模式标识（模拟/真实）。</summary>
    public string ModeText => SwitchableDeviceClientFactory.IsSimulation
        ? "【模拟模式】数据为虚拟信号"
        : "【真实模式】连接实际设备";

    public ObservableCollection<PointRow> Items { get; } = new();
    public ObservableCollection<AlarmRow> Alarms { get; } = new();

    public DelegateCommand StartCommand { get; }
    public DelegateCommand StopCommand { get; }
    public DelegateCommand WriteDemoCommand { get; }
    public DelegateCommand ToggleModeCommand { get; }
}

/// <summary>测点显示行。</summary>
public class PointRow : BindableBase
{
    public int PointId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";

    private double _value;
    public double Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    private DateTime _time;
    public DateTime Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }
}

/// <summary>报警显示行。</summary>
public class AlarmRow
{
    public string Message { get; set; } = "";
    public string State { get; set; } = "";
    public string Level { get; set; } = "";
    public DateTime Time { get; set; }
}
