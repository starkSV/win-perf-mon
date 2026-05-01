using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using WinPerfMon.App.ViewModels;

namespace WinPerfMon.App.Pages;

public sealed partial class StoragePage : Page
{
    private readonly StorageViewModel _vm;

    public StoragePage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<StorageViewModel>();
        DiskRepeater.ItemsSource = _vm.Disks;
    }
}
