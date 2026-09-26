using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Statistics;
using Windows.UI;

namespace SpeedcubingTrainer.Controls;

/// <summary>Distribution of solve times in equal-width bins.</summary>
public sealed partial class HistogramView : UserControl
{
    public static readonly DependencyProperty SolvesProperty = DependencyProperty.Register(
        nameof(Solves), typeof(IReadOnlyList<SolveTime>), typeof(HistogramView), new PropertyMetadata(null, (d, _) => ((HistogramView)d).Redraw()));

    private static readonly Color BarColor = Color.FromArgb(255, 120, 200, 255);

    private readonly Canvas _canvas = new();

    public HistogramView()
    {
        Content = _canvas;
        MinHeight = 120;
        SizeChanged += (_, _) => Redraw();
    }

    public IReadOnlyList<SolveTime>? Solves
    {
        get => (IReadOnlyList<SolveTime>?)GetValue(SolvesProperty);
        set => SetValue(SolvesProperty, value);
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        var times = Solves?.Select(s => s.EffectiveMs).Where(v => v is not null).Select(v => v!.Value).ToList();
        var width = ActualWidth;
        var height = ActualHeight;
        if (times is null || times.Count < 3 || width < 40 || height < 40)
        {
            return;
        }
        var min = times.Min();
        var max = times.Max();
        var binCount = Math.Clamp((int)Math.Sqrt(times.Count), 5, 20);
        var binWidthMs = Math.Max(1, (max - min) / (double)binCount);
        var bins = new int[binCount];
        foreach (var t in times)
        {
            var b = Math.Min(binCount - 1, (int)((t - min) / binWidthMs));
            bins[b]++;
        }
        var tallest = bins.Max();
        const double bottom = 16;
        var plotHeight = height - bottom - 4;
        var barWidth = width / binCount;
        for (var i = 0; i < binCount; i++)
        {
            var h = plotHeight * bins[i] / tallest;
            var bar = new Rectangle { Width = Math.Max(1, barWidth - 3), Height = h, Fill = CubeColors.Brush(BarColor), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(bar, i * barWidth + 1.5);
            Canvas.SetTop(bar, 4 + plotHeight - h);
            _canvas.Children.Add(bar);
            if (i % Math.Max(1, binCount / 5) == 0)
            {
                var startMs = min + i * binWidthMs;
                var label = new TextBlock { Text = startMs < 60000 ? (startMs / 1000).ToString("0.0") : TimeFormat.Format((int)startMs, showHundredths: false), FontSize = 10, Opacity = 0.7 };
                Canvas.SetLeft(label, i * barWidth + 2);
                Canvas.SetTop(label, height - bottom + 2);
                _canvas.Children.Add(label);
            }
        }
    }
}
