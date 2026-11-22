using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 测点（对应设备上的一个寄存器 / 线圈地址）。
/// </summary>
/// <remarks>
/// <see cref="Address"/> 存的是 <b>相对地址</b>（从 0 开始），不是 40001 那种绝对地址。
/// 绝对地址 = 区号 + (相对地址 + 1)，只用于给人看和查手册，报文里传的永远是相对地址。
/// </remarks>
public class DataPoint
{
    public int Id { get; set; }

    /// <summary>所属设备 Id。</summary>
    public int DeviceId { get; set; }

    /// <summary>所属设备（导航属性）。</summary>
    public Device? Device { get; set; }

    /// <summary>测点名称，如"温度"。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所属存储区（0/1/3/4 区）。</summary>
    public ModbusArea Area { get; set; } = ModbusArea.InputRegister;

    /// <summary>相对起始地址（从 0 开始）。</summary>
    public ushort Address { get; set; }

    /// <summary>数据类型，决定如何解释寄存器原始值。</summary>
    public DataValueType DataType { get; set; } = DataValueType.UInt16;

    /// <summary>缩放系数。工程值 = 原始值 × Scale + Offset。</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>偏移量。</summary>
    public double Offset { get; set; }

    /// <summary>单位，如 ℃、MPa、rpm。</summary>
    public string? Unit { get; set; }

    /// <summary>显示小数位数。</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>是否启用。停用后不参与采集。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 该测点占用的寄存器数量：32 位类型占 2 个，其余占 1 个。
    /// </summary>
    public ushort RegisterCount => DataType switch
    {
        DataValueType.UInt32 or DataValueType.Int32 or DataValueType.Float => (ushort)2,
        _ => (ushort)1
    };

    /// <summary>是否为位类型（线圈 / 离散输入）。</summary>
    public bool IsBitType =>
        Area is ModbusArea.Coil or ModbusArea.DiscreteInput ||
        DataType == DataValueType.Boolean;

    /// <summary>
    /// 把寄存器原始值换算为工程值。
    /// </summary>
    /// <param name="raw">寄存器原始数值。</param>
    public double ToEngineeringValue(double raw) => raw * Scale + Offset;

    /// <summary>
    /// 把工程值换算回原始值（用于参数下发）。
    /// </summary>
    /// <param name="engineering">工程值。</param>
    public double ToRawValue(double engineering)
    {
        if (Math.Abs(Scale) < double.Epsilon)
        {
            throw new InvalidOperationException($"测点 {Name} 的 Scale 不能为 0。");
        }
        return (engineering - Offset) / Scale;
    }
}
