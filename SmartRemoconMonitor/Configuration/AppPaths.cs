using System.IO;

namespace SmartRemoconMonitor.Configuration;

/// <summary>
/// アプリ名、データの保存先など
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// アプリ名
    /// </summary>
    public const string ProductName = "SmartRemoconMonitor";

    /// <summary>
    /// %LOCALAPPDATA%\SmartRemoconMonitor\配下のフルパス
    /// </summary>
    public static string InLocalAppData(string fileName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductName,
        fileName);
}
