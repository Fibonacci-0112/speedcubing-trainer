using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Statistics;
using Windows.UI;

namespace SpeedcubingTrainer.Controls;

/// <summary>Line chart of solve times with rolling ao5 and ao12 overlays, drawn with XAML shapes.</summary>
public sealed partial class TrendChartView : UserControl
{
    public static readonly DependencyProperty SolvesProperty = DependencyProperty.Register(
        nameof(Solves), typeof(IReadOnlyList<SolveTime>), typeof(TrendChartView), new PropertyMetadata(null, (d, _) => ((TrendChartView)d).Redraw()));

    private static readonly Color SingleColor = Color.FromArgb(255, 120, 200, 255);
    private static readonly Color Ao5Color = Color.FromArgb(255, 255, 190, 60);
    private static readonly Color Ao12Color = Color.FromArgb(255, 230, 90, 200);
    private static readonly Color AxisColor = Color.FromArgb(90, 160, 160, 160);

    private readonly Canvas _canvas = new();
    private readonly Grid _root = new();
    private readonly TextBlock _empty = new() { Text = "Times will be charted here after a few solves", Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public TrendChartView()
    {
        _root.Children.Add(_canvas);
        _root.Children.Add(_empty);
        Content = _root;
        MinHeight = 160;
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
        var solves = Solves;
        var width = ActualWidth;
        var height = ActualHeight;
        if (solves is null || solves.Count < 2 || width < 40 || height < 40)
        {
            _empty.Visibility = Visibility.Visible;
            return;
        }
        _empty.Visibility = Visibility.Collapsed;

        var singles = solves.Select(s => s.EffectiveMs).ToArray();
        var ao5 = SessionStatistics.Series(solves, StatKind.Ao5);
        var ao12 = SessionStatistics.Series(solves, StatKind.Ao12);
        var values = singles.Concat(ao5).Concat(ao12).Where(v => v is not null).Select(v => v!.Value).ToList();
        if (values.Count == 0)
        {
            _empty.Visibility = Visibility.Visible;
            return;
        }
        double min = values.Min();
        double max = values.Max();
        if (max == min)
        {
            max = min + 1000;
        }
        var pad = (max - min) * 0.08;
        min = Math.Max(0, min - pad);
        max += pad;

        const double left = 44;
        const double bottom = 18;
        const double top = 8;
        var plotWidth = width - left - 8;
        var plotHeight = height - top - bottom;

        double X(int i) => left + (solves.Count == 1 ? 0 : plotWidth * i / (solves.Count - 1));
        double Y(double ms) => top + plotHeight * (1 - (ms - min) / (max - min));

        // Horizontal grid lines with labels.
        for (var k = 0; k <= 4; k++)
        {
            var ms = min + (max - min) * k / 4;
            var y = Y(ms);
            _canvas.Children.Add(new Line { X1 = left, X2 = width - 8, Y1 = y, Y2 = y, Stroke = CubeColors.Brush(AxisColor), StrokeThickness = 1 });
            var label = new TextBlock { Text = AxisLabel(ms), FontSize = 10, Opacity = 0.7 };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 7);
            _canvas.Children.Add(label);
        }

        AddSeries(singles, SingleColor, 1.5, X, Y, dots: solves.Count <= 200);
        AddSeries(ao5, Ao5Color, 2, X, Y, dots: false);
        AddSeries(ao12, Ao12Color, 2, X, Y, dots: false);

        // Legend
        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (name, color) in new[] { ("single", SingleColor), ("ao5", Ao5Color), ("ao12", Ao12Color) })
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            item.Children.Add(new Rectangle { Width = 10, Height = 3, Fill = CubeColors.Brush(color), VerticalAlignment = VerticalAlignment.Center });
            item.Children.Add(new TextBlock { Text = name, FontSize = 10, Opacity = 0.8 });
            legend.Children.Add(item);
        }
        Canvas.SetLeft(legend, left);
        Canvas.SetTop(legend, height - bottom + 2);
        _canvas.Children.Add(legend);
    }

    private static string AxisLabel(double ms) => ms < 60000 ? (ms / 1000).ToString("0.0") : TimeFormat.Format((int)ms, showHundredths: false);

    private void AddSeries(int?[] series, Color color, double thickness, Func<int, double> x, Func<double, double> y, bool dots)
    {
        var brush = CubeColors.Brush(color);
        Polyline? current = null;
        for (var i = 0; i < series.Length; i++)
        {
            if (series[i] is not { } ms)
            {
                current = null;
                continue;
            }
            if (current is null)
            {
                current = new Polyline { Stroke = brush, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
                _canvas.Children.Add(current);
            }
            current.Points.Add(new Windows.Foundation.Point(x(i), y(ms)));
            if (dots)
            {
                var dot = new Ellipse { Width = 4, Height = 4, Fill = brush };
                Canvas.SetLeft(dot, x(i) - 2);
                Canvas.SetTop(dot, y(ms) - 2);
                _canvas.Children.Add(dot);
            }
        }
    }
}
