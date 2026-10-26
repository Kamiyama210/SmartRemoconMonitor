namespace SmartRemoconMonitor.Models;

/// <summary>
/// 状態ランプの色の段階
/// </summary>
public enum StatusLevel
{
    /// <summary>
    /// 正常（緑）
    /// </summary>
    Ok,

    /// <summary>
    /// 注意（黄）
    /// </summary>
    Warning,

    /// <summary>
    /// 異常（赤）
    /// </summary>
    Error,
}
