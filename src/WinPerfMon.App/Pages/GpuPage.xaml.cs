using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using WinPerfMon.App.ViewModels;

namespace WinPerfMon.App.Pages;

public sealed partial class GpuPage : Page
{
    private readonly GpuViewModel _vm;

    public GpuPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<GpuViewModel>();

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(GpuViewModel.GpuName):
                    GpuNameText.Text = _vm.GpuName;
                    break;
                case nameof(GpuViewModel.CoreLoad):
                    CoreLoadText.Text = $"{_vm.CoreLoad:F0}%";
                    break;
                case nameof(GpuViewModel.VramUsedGb):
                case nameof(GpuViewModel.VramTotalGb):
                    VramText.Text = $"{_vm.VramUsedGb:F1} / {_vm.VramTotalGb:F1} GB";
                    break;
                case nameof(GpuViewModel.Temperature):
                    TempText.Text = $"{_vm.Temperature:F0}°C";
                    break;
                case nameof(GpuViewModel.CoreClock):
                    ClockText.Text = $"{_vm.CoreClock:F0} MHz";
                    break;
            }
        };

        GpuChart.Series =
        [
            new LineSeries<float>
            {
                Values = _vm.CoreLoadHistory,
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3,
            }
        ];

        GpuChart.YAxes = [new Axis { MinLimit = 0, MaxLimit = 100 }];
        GpuChart.XAxes = [new Axis { IsVisible = false }];
    }
}
