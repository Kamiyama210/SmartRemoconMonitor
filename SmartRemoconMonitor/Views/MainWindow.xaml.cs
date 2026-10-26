using System.Windows;
using SmartRemoconMonitor.ViewModels;

namespace SmartRemoconMonitor.Views;

/// <summary>
/// メインウィンドウ。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// 画面を組み立て、ViewModelを結び付ける。
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
