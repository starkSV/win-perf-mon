using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinPerfMon.App.ViewModels;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.App.Pages;

public sealed partial class ProcessesPage : Page
{
    private readonly ProcessesViewModel _vm;
    private ProcessEntry? _contextTarget; // process under right-click

    public ProcessesPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ProcessesViewModel>();
        ProcessList.ItemsSource = _vm.Processes;

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ProcessesViewModel.TotalProcesses):
                    ProcessCountText.Text = $"{_vm.TotalProcesses} processes";
                    break;
                case nameof(ProcessesViewModel.TotalThreads):
                    ThreadCountText.Text = $"· {_vm.TotalThreads} threads";
                    break;
            }
        };
    }

    // ── Search ─────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs _)
        => _vm.Filter = sender.Text;

    // ── Column sort headers ────────────────────────────────────────────────

    private void SortName_Click(object s, RoutedEventArgs e)    => _vm.ToggleSort(ProcessSortColumn.Name);
    private void SortPid_Click(object s, RoutedEventArgs e)     => _vm.ToggleSort(ProcessSortColumn.Pid);
    private void SortCpu_Click(object s, RoutedEventArgs e)     => _vm.ToggleSort(ProcessSortColumn.Cpu);
    private void SortMemory_Click(object s, RoutedEventArgs e)  => _vm.ToggleSort(ProcessSortColumn.Memory);
    private void SortDisk_Click(object s, RoutedEventArgs e)    => _vm.ToggleSort(ProcessSortColumn.Disk);
    private void SortNetwork_Click(object s, RoutedEventArgs e) => _vm.ToggleSort(ProcessSortColumn.Network);
    private void SortGpu_Click(object s, RoutedEventArgs e)     => _vm.ToggleSort(ProcessSortColumn.Gpu);

    // ── Selection ──────────────────────────────────────────────────────────

    private void ProcessList_SelectionChanged(object _, SelectionChangedEventArgs e)
    {
        var selected = ProcessList.SelectedItem as ProcessEntry;
        EndTaskButton.IsEnabled = selected != null;

        if (selected is null)
        {
            SelectedProcessInfo.Text = string.Empty;
            return;
        }

        SelectedProcessInfo.Text =
            $"{selected.Name}  PID {selected.Pid}  " +
            $"CPU {selected.CpuPercent:F1}%  " +
            $"RAM {selected.WorkingSetDisplay}  " +
            $"Threads {selected.ThreadCount}  Handles {selected.HandleCount}";
    }

    // ── End Task (toolbar button) ──────────────────────────────────────────

    private async void EndTaskButton_Click(object _, RoutedEventArgs e)
    {
        if (ProcessList.SelectedItem is not ProcessEntry proc) return;
        await ConfirmAndKill(proc);
    }

    // ── Right-click context menu ───────────────────────────────────────────

    private void ProcessList_RightTapped(object _, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is ProcessEntry proc)
        {
            _contextTarget = proc;
            ProcessList.SelectedItem = proc;
            var flyout = (MenuFlyout)ProcessList.Resources["ProcessContextMenu"];
            flyout.ShowAt(ProcessList, e.GetPosition(ProcessList));
        }
    }

    private async void MenuEndTask_Click(object _, RoutedEventArgs e)
    {
        if (_contextTarget is null) return;
        await ConfirmAndKill(_contextTarget);
    }

    private void MenuOpenPath_Click(object _, RoutedEventArgs e)
    {
        if (_contextTarget is null || string.IsNullOrEmpty(_contextTarget.FullPath)) return;
        var dir = System.IO.Path.GetDirectoryName(_contextTarget.FullPath);
        if (dir != null) Process.Start("explorer.exe", $"/select,\"{_contextTarget.FullPath}\"");
    }

    private void MenuPriority_Click(object sender, RoutedEventArgs e)
    {
        if (_contextTarget is null) return;
        var tag = (sender as MenuFlyoutItem)?.Tag?.ToString();
        var priority = tag switch
        {
            "Realtime"    => ProcessPriorityClass.RealTime,
            "High"        => ProcessPriorityClass.High,
            "AboveNormal" => ProcessPriorityClass.AboveNormal,
            "Normal"      => ProcessPriorityClass.Normal,
            "BelowNormal" => ProcessPriorityClass.BelowNormal,
            "Idle"        => ProcessPriorityClass.Idle,
            _             => ProcessPriorityClass.Normal,
        };
        _vm.SetPriority(_contextTarget.Pid, priority);
    }

    private async void MenuAffinity_Click(object _, RoutedEventArgs e)
    {
        if (_contextTarget is null) return;
        // Simple dialog — full CPU picker UI is v2
        var dialog = new ContentDialog
        {
            Title = $"Set affinity — {_contextTarget.Name}",
            Content = new TextBox
            {
                Header = $"Affinity mask (hex) — {Environment.ProcessorCount} logical CPUs",
                PlaceholderText = $"e.g. {((1L << Environment.ProcessorCount) - 1):X}",
            },
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var tb = (TextBox)dialog.Content;
            if (long.TryParse(tb.Text.TrimStart('0', 'x'), System.Globalization.NumberStyles.HexNumber, null, out long mask))
                _vm.SetAffinity(_contextTarget.Pid, mask);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task ConfirmAndKill(ProcessEntry proc)
    {
        var dialog = new ContentDialog
        {
            Title = "End task",
            Content = $"End \"{proc.Name}\" (PID {proc.Pid})?\nUnsaved data will be lost.",
            PrimaryButtonText = "End task",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            _vm.KillProcess(proc.Pid);
    }
}
