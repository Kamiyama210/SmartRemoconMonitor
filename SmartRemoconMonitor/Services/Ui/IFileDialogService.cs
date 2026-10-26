namespace SmartRemoconMonitor.Services.Ui;

/// <summary>
/// ファイルの保存先ダイアログ
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// 保存ダイアログを表示し、指定したパスを返す。取り消されたらnull。
    /// </summary>
    string? AskSavePath(string title, string defaultFileName, string filter);
}
