using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;
using SmartRemoconMonitor.Services.Navigation;

namespace SmartRemoconMonitor.Models;

/// <summary>
/// 左側のメニューの1項目
/// </summary>
public sealed partial class NavigationMenuItem : ObservableObject
{
    /// <summary>
    /// この項目が選択中か。
    /// </summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>
    /// メニュー項目を作る。
    /// </summary>
    public NavigationMenuItem(AppSection section, string label, PackIconMaterialKind icon)
    {
        Section = section;
        Label = label;
        Icon = icon;
    }

    /// <summary>
    /// 選択したときに表示する画面
    /// </summary>
    public AppSection Section { get; }

    /// <summary>
    /// 表示名
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// アイコン
    /// </summary>
    public PackIconMaterialKind Icon { get; }
}
