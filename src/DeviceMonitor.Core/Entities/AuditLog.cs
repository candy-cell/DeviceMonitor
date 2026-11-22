namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 操作审计日志。记录"谁在什么时候对什么做了什么，结果如何"。
/// </summary>
/// <remarks>
/// 工业软件里这条很重要——尤其是参数下发（写寄存器），出了事故要能查是谁写的、写成了什么。
/// </remarks>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>发生时刻。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>操作人（未登录时可为空）。</summary>
    public string? UserName { get; set; }

    /// <summary>操作类型，如 Login / WriteRegister / WriteCoil / ConfigChange。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>操作目标，如 "Device3.Point7 addr=0 value=100"。</summary>
    public string? Target { get; set; }

    /// <summary>结果，如 Success / Fail:超时。</summary>
    public string Result { get; set; } = string.Empty;
}
