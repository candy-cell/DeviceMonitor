using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core;

/// <summary>
/// 寄存器原始值 → 工程值的解释器。
/// </summary>
/// <remarks>
/// 把"两个 16 位寄存器怎么拼成 32 位整数/浮点数"这件容易出错的活集中到一个地方。
///
/// 关键规则：
/// - 16 位类型：直接取第一个寄存器。
/// - 32 位类型：用"高字在前（大端）"方式拼两个寄存器——<c>high = regs[0], low = regs[1]</c>。
///   注意：不同品牌 PLC 的高低字顺序可能相反，若现场读出的 32 位值明显不对，
///   优先排查这里（可能需要 high/low 对调）。
/// - 位类型：取 bits[0]。
/// - 最终工程值 = 原始值 × Scale + Offset。
/// </remarks>
public static class ValueInterpreter
{
    /// <summary>
    /// 把一次轮询读到的原始数据解释成工程值。
    /// </summary>
    /// <param name="point">测点定义（含区、类型、量程）。</param>
    /// <param name="regs">寄存器原始值（寄存器区；位区传空数组）。</param>
    /// <param name="bits">位原始值（位区；寄存器区传 null）。</param>
    /// <returns>工程值。若数据不足（如 32 位类型只读到 1 个寄存器）抛异常。</returns>
    public static double ToEngineering(DataPoint point, ushort[] regs, bool[]? bits)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(regs);

        double raw;

        if (point.IsBitType)
        {
            if (bits is null || bits.Length == 0)
            {
                throw new InvalidOperationException($"测点 {point.Name} 是位类型，但未提供位数据。");
            }
            raw = bits[0] ? 1.0 : 0.0;
        }
        else
        {
            raw = point.DataType switch
            {
                DataValueType.UInt16 => regs[0],
                DataValueType.Int16 => (short)regs[0],
                DataValueType.UInt32 => (uint)((regs[0] << 16) | regs[1]),
                DataValueType.Int32 => (int)((regs[0] << 16) | regs[1]),
                DataValueType.Float => BitConverter.Int32BitsToSingle((int)((regs[0] << 16) | regs[1])),
                _ => throw new ArgumentOutOfRangeException(nameof(point), point.DataType, "未知数据类型。")
            };
        }

        return point.ToEngineeringValue(raw);
    }

    /// <summary>
    /// 检查寄存器数据是否够解释该测点（32 位类型需要 2 个寄存器）。
    /// </summary>
    public static bool HasEnoughData(DataPoint point, ushort[] regs)
        => !point.IsBitType && regs.Length >= point.RegisterCount;
}
