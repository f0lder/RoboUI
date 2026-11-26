using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RoboUI.Behaviors;

/// <summary>
/// Attached behavior: lets a folder be dragged from Explorer onto a TextBox.
/// Sets the Text and pushes it to the binding. Files and multiple items are rejected.
/// </summary>
public static class FolderDrop
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(FolderDrop), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;

        box.PreviewDragEnter -= OnDragOver;
        box.PreviewDragOver -= OnDragOver;
        box.PreviewDrop -= OnDrop;

        if (e.NewValue is true)
        {
            box.AllowDrop = true;
            box.PreviewDragEnter += OnDragOver;
            box.PreviewDragOver += OnDragOver;
            box.PreviewDrop += OnDrop;
        }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetFolder(e) is not null && sender is TextBox { IsEnabled: true, IsReadOnly: false }
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not TextBox { IsEnabled: true, IsReadOnly: false } box || GetFolder(e) is not { } folder) return;

        box.SetCurrentValue(TextBox.TextProperty, folder);
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    private static string? GetFolder(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths && Directory.Exists(paths[0])
            ? paths[0]
            : null;
}
