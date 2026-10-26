using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using SmartRemoconMonitor.Configuration;

namespace SmartRemoconMonitor.ViewModels;

/// <summary>
/// タイトル画面
/// </summary>
public sealed class AppTitleViewModel : ObservableObject
{
    /// <summary>
    /// アプリ名とバージョンを読み取る
    /// </summary>
    public AppTitleViewModel()
    {
        var assembly = Assembly.GetExecutingAssembly();

        Title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                ?? assembly.GetName().Name
                ?? AppPaths.ProductName;

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational ?? assembly.GetName().Version?.ToString() ?? "0.0.0";

        var plus = version.IndexOf('+', StringComparison.Ordinal);
        Version = plus >= 0 ? version[..plus] : version;
    }

    /// <summary>
    /// アプリ名
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// バージョン
    /// </summary>
    public string Version { get; }
}
