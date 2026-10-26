using Microsoft.Win32;

namespace SmartRemoconMonitor.Services.Ui;

/// <summary>
/// <see cref="IFileDialogService"/>。
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    /// <inheritdoc/>
    public string? AskSavePath(string title, string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
