using System.IO;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace RoboUI.Services;

public interface IDialogService
{
    /// <summary>Shows a confirmation. Returns true if the user chose <paramref name="confirmText"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false, string cancelText = "Cancel");

    /// <summary>Shows a folder picker. Returns the chosen folder, or null if cancelled.</summary>
    string? PickFolder(string title, string? initialFolder);

    /// <summary>Puts text on the clipboard. Returns false if the clipboard was busy.</summary>
    bool CopyToClipboard(string text);
}

public sealed class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false, string cancelText = "Cancel")
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = System.Windows.TextWrapping.Wrap,
                MaxWidth = 460,
            },
            PrimaryButtonText = confirmText,
            PrimaryButtonAppearance = destructive ? ControlAppearance.Danger : ControlAppearance.Primary,
            CloseButtonText = cancelText,
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        var result = await box.ShowDialogAsync();
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    public string? PickFolder(string title, string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            dialog.InitialDirectory = initialFolder;

        var owner = System.Windows.Application.Current?.MainWindow;
        var ok = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return ok == true ? dialog.FolderName : null;
    }

    public bool CopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}
