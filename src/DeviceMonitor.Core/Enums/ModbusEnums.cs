namespace DeviceMonitor.Core.Enums;

/// <summary>
/// Modbus 四类存储区（区号）。
/// 注意：区号与功能码不是一一顺序对应——3 区用 0x04 读，4 区用 0x03 读（交叉）。
/// </summary>
public enum ModbusArea
{
    /// <summary>0 区：线圈。位（1 bit），可读可写。功能码 0x01 读 / 0x05 写单个 / 0x0F 写多个。</summary>
    Coil = 0,

    /// <summary>1 区：离散输入。位（1 bit），只读。功能码 0x02。</summary>
    DiscreteInput = 1,

    /// <summary>3 区：输入寄存器。字（16 bit），只读。功能码 0x04。</summary>
    InputRegister = 3,

    /// <summary>4 区：保持寄存器。字（16 bit），可读可写。功能码 0x03 读 / 0x06 写单个 / 0x10 写多个。</summary>
    HoldingRegister = 4
}

/// <summary>
/// 测点的数据类型。决定如何把寄存器里的 16 位原始值解释成物理量。
/// </summary>
public enum DataValueType
{
    /// <summary>16 位无符号整数（0 ~ 65535）。</summary>
    UInt16 = 0,

    /// <summary>16 位有符号整数（-32768 ~ 32767）。</summary>
    Int16 = 1,

    /// <summary>32 位无符号整数（占 2 个寄存器）。</summary>
    UInt32 = 2,

    /// <summary>32 位有符号整数（占 2 个寄存器）。</summary>
    Int32 = 3,

    /// <summary>32 位浮点数（占 2 个寄存器）。</summary>
    Float = 4,

    /// <summary>开关量（位）。仅用于线圈 / 离散输入区。</summary>
    Boolean = 5
}

/// <summary>
/// 通信协议类型。RTU 走串口，Tcp 走以太网。
/// </summary>
public enum DeviceProtocol
{
    /// <summary>Modbus RTU（串口，RS-232/422/485）。</summary>
    Rtu = 0,

    /// <summary>Modbus TCP（以太网，默认 502 端口）。</summary>
    Tcp = 1
}

/// <summary>
/// 数据质量戳。用于标记采样值是否可信。
/// </summary>
public enum QualityFlag
{
    /// <summary>正常。</summary>
    Good = 0,

    /// <summary>坏值（通信中断、超时、从站异常等，此时 Value 不可信）。</summary>
    Bad = 1,

    /// <summary>不确定（例如设备刚上线、尚未完成首次完整采集）。</summary>
    Uncertain = 2
}
