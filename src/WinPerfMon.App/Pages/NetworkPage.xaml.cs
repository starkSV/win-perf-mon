using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using WinPerfMon.App.ViewModels;

namespace WinPerfMon.App.Pages;

public sealed partial class NetworkPage : Page
{
    private readonly NetworkViewModel _vm;

    public NetworkPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<NetworkViewModel>();

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(NetworkViewModel.GatewayRttMs):
                    GatewayRttText.Text = _vm.GatewayRttMs >= 0 ? $"{_vm.GatewayRttMs:F0} ms" : "—";
                    break;
                case nameof(NetworkViewModel.CustomRttMs):
                    CustomRttText.Text = _vm.CustomRttMs >= 0 ? $"{_vm.CustomRttMs:F0} ms" : "—";
                    break;
            }
        };

        InterfaceList.ItemsSource = _vm.Interfaces;

        BwChart.Series =
        [
            new LineSeries<float>
            {
                Name = "Download",
                Values = _vm.DownHistory,
                Fill = null,
                GeometrySize = 0,
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.LimeGreen, 2),
            },
            new LineSeries<float>
            {
                Name = "Upload",
                Values = _vm.UpHistory,
                Fill = null,
                GeometrySize = 0,
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.CornflowerBlue, 2),
            },
        ];

        BwChart.XAxes = [new Axis { IsVisible = false }];
    }
}
