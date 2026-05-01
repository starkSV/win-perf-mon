using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinPerfMon.App.Pages;

namespace WinPerfMon.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;

        // Default to Processes page
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(ProcessesPage));
        PageTitle.Text = "Processes";
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            PageTitle.Text = "Settings";
            return;
        }

        var tag = (args.SelectedItemContainer as NavigationViewItem)?.Tag?.ToString();
        (Type page, string title) = tag switch
        {
            "processes" => (typeof(ProcessesPage), "Processes"),
            "cpu"       => (typeof(CpuPage),       "CPU"),
            "gpu"       => (typeof(GpuPage),        "GPU"),
            "network"   => (typeof(NetworkPage),    "Network"),
            "storage"   => (typeof(StoragePage),    "Storage"),
            _           => (typeof(ProcessesPage),  "Processes"),
        };

        ContentFrame.Navigate(page);
        PageTitle.Text = title;
    }
}
