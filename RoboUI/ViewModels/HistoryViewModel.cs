using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoboUI.Services;

namespace RoboUI.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly RunHistoryService _history;
    private readonly MainViewModel _main;
    private readonly IDialogService _dialogs;

    public HistoryViewModel(RunHistoryService history, MainViewModel main, IDialogService dialogs)
    {
        _history = history;
        _main = main;
        _dialogs = dialogs;

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            ClearCommand.NotifyCanExecuteChanged();
        };
        _history.Added += run => Items.Insert(0, new HistoryItemViewModel(run));
        _history.SaveFailed += message => Message = $"The history could not be saved: {message}";
        _main.PropertyChanged += OnMainPropertyChanged;
    }

    /// <summary>Asks the window to show the Home page (after a run was loaded).</summary>
    public event Action? ShowHomeRequested;

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    /// <summary>A problem reading or saving the history file, shown at the top of the page.</summary>
    [ObservableProperty] public partial string? Message { get; set; }

    /// <summary>Reads the saved history. Called once at startup; safe to call while runs are being added.</summary>
    public async Task LoadAsync()
    {
        try
        {
            var runs = await _history.LoadAsync();
            // A run may have been added while loading; keep it without duplicating.
            var known = runs.Select(r => r.Id).ToHashSet();
            var added = Items.Where(i => !known.Contains(i.Run.Id)).Select(i => i.Run).ToList();
            var all = added.Concat(runs).OrderByDescending(r => r.StartedAt).ToList();
            Items.Clear();
            foreach (var run in all) Items.Add(new HistoryItemViewModel(run));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = $"The history could not be read: {ex.Message}";
        }
    }

    private bool CanLoad(HistoryItemViewModel? item) => item is not null && !_main.IsRunning;

    /// <summary>Puts the run's settings on the Home page, ready to run again or resume.</summary>
    [RelayCommand(CanExecute = nameof(CanLoad))]
    private void Load(HistoryItemViewModel? item)
    {
        if (item is not null && _main.LoadFromHistory(item.Run)) ShowHomeRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenLog(HistoryItemViewModel? item)
    {
        if (item?.Run.LogPath is { } path && !_dialogs.OpenInShell(path))
            Message = $"The log could not be opened: {path}";
    }

    private bool CanClear() => Items.Count > 0;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private async Task ClearAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Clear history",
            $"Remove all {Items.Count:N0} runs from the history? Log files are kept.",
            confirmText: "Clear history");
        if (!confirmed) return;

        Items.Clear();
        try
        {
            await _history.ClearAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = $"The history could not be saved: {ex.Message}";
        }
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRunning)) LoadCommand.NotifyCanExecuteChanged();
    }
}
