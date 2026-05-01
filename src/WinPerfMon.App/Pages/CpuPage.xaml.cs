using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinPerfMon.App.ViewModels;

namespace WinPerfMon.App.Pages;

public sealed partial class CpuPage : Page
{
    private readonly CpuViewModel _vm;

    public CpuPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<CpuViewModel>();

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CpuViewModel.TotalLoad):
                    TotalLoadText.Text = $"{_vm.TotalLoad:F0}%";
                    break;
                case nameof(CpuViewModel.PackageTemp):
                    PackageTempText.Text = $"{_vm.PackageTemp:F0}°C";
                    break;
                case nameof(CpuViewModel.ProcessCount):
                    ProcessCountText.Text = _vm.ProcessCount.ToString();
                    break;
                case nameof(CpuViewModel.ThreadCount):
                    ThreadCountText.Text = _vm.ThreadCount.ToString();
                    break;
            }
        };

        CoresRepeater.ItemsSource = _vm.Cores;

        LoadChart.Series =
        [
            new LineSeries<float>
            {
                Values = _vm.TotalLoadHistory,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3,
            }
        ];

        LoadChart.YAxes = [new Axis { MinLimit = 0, MaxLimit = 100 }];
        LoadChart.XAxes = [new Axis { IsVisible = false }];
    }
}
