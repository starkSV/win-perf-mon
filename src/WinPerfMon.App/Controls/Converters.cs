using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WinPerfMon.Shared.Models;

namespace WinPerfMon.App.Controls;

public sealed class BytesPerSecConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        long bytes = value switch
        {
            long l => l,
            int i  => i,
            _      => 0,
        };

        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB/s",
            >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB/s",
            >= 1_024         => $"{bytes / 1_024.0:F0} KB/s",
            _                => $"{bytes} B/s",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public sealed class HealthColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var color = value is SmartHealth h ? h switch
        {
            SmartHealth.Healthy => Colors.SeaGreen,
            SmartHealth.Warning => Colors.DarkOrange,
            SmartHealth.Failing => Colors.Crimson,
            _                   => Colors.Gray,
        } : Colors.Gray;

        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public sealed class CoreIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => $"Core {value}";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>Formats a float as "12.3%" or "—" when zero.</summary>
public sealed class CpuPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        float v = value is float f ? f : 0f;
        return v < 0.05f ? "—" : $"{v:F1}%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>Colors a load value: &lt;60 neutral, 60–80 amber, 80–90 orange, 90+ red.</summary>
public sealed class LoadColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        float v = value is float f ? f : 0f;
        var color = v switch
        {
            >= 90f => Windows.UI.Color.FromArgb(255, 239, 68,  68),  // red
            >= 80f => Windows.UI.Color.FromArgb(255, 249, 115,  22), // orange
            >= 60f => Windows.UI.Color.FromArgb(255, 245, 158,  11), // amber
            _      => Windows.UI.Color.FromArgb(255, 232, 234, 240), // neutral
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>Hides a TextBlock when its string binding is null or empty.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrEmpty(value as string)
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
