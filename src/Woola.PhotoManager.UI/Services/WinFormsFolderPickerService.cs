namespace Woola.PhotoManager.UI.Services;

public class WinFormsFolderPickerService : IFolderPickerService
{
    public string? PickFolder(string description = "Seleccionar carpeta")
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = false
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
