using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 设备（对应一个 Modbus 从站）。
/// 同时容纳 RTU（串口）与 TCP（以太网）两套连接参数，由 <see cref="Protocol"/> 决定用哪一套。
/// </summary>
/// <remarks>
/// 这里刻意用 int 表示 StopBits / Parity，而不是 System.IO.Ports 里的枚举，
/// 目的是让 Core 项目不依赖任何外部包，保持"纯领域模型"。
/// 真正的类型转换放在通信层做。
/// </remarks>
public class Device
{
    public int Id { get; set; }

    /// <summary>设备名称，如"1#温湿度"。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型：RTU 或 TCP。</summary>
    public DeviceProtocol Protocol { get; set; } = DeviceProtocol.Rtu;

    /// <summary>从站地址，合法范围 1~247（0 为广播地址，不用于采集）。</summary>
    public byte SlaveId { get; set; } = 1;

    // ---------------- RTU 专用参数 ----------------

    /// <summary>串口名，如 COM3。仅 RTU 使用。</summary>
    public string? PortName { get; set; }

    /// <summary>波特率。工业现场常用 9600。</summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>数据位，常用 8。</summary>
    public int DataBits { get; set; } = 8;

    /// <summary>停止位：1 / 2（对应 StopBits.One / StopBits.Two）。</summary>
    public int StopBits { get; set; } = 1;

    /// <summary>校验位：0=None，1=Odd，2=Even，3=Mark，4=Space（对应 Parity 枚举顺序）。</summary>
    public int Parity { get; set; } = 0;

    // ---------------- TCP 专用参数 ----------------

    /// <summary>IP 地址，如 192.168.1.10。仅 TCP 使用。</summary>
    public string? IpAddress { get; set; }

    /// <summary>TCP 端口，Modbus 标准为 502。</summary>
    public int TcpPort { get; set; } = 502;

    // ---------------- 采集参数 ----------------

    /// <summary>该设备的轮询周期（毫秒）。</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>是否启用采集。停用后不参与轮询。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>备注。</summary>
    public string? Remark { get; set; }

    /// <summary>该设备下挂的测点集合。</summary>
    public ICollection<DataPoint> DataPoints { get; set; } = new List<DataPoint>();
}
