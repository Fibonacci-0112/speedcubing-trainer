using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Controls;

/// <summary>
/// The classic last-layer diagram: the top face seen from above with the top row of each side
/// drawn as a thin strip around it. In orientation-only mode (OLL) it follows the familiar printed
/// style: a black-lined grid with oriented stickers yellow and the rest grey, and a short yellow bar
/// beside the grid wherever a last-layer sticker faces sideways (no bar where it does not).
/// </summary>
public sealed partial class TopFaceView : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(CubeState), typeof(TopFaceView), new PropertyMetadata(null, (d, _) => ((TopFaceView)d).Refresh()));

    public static readonly DependencyProperty ColorModeProperty = DependencyProperty.Register(
        nameof(ColorMode), typeof(CaseColorMode), typeof(TopFaceView), new PropertyMetadata(CaseColorMode.Full, (d, _) => ((TopFaceView)d).Rebuild()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(TopFaceView), new PropertyMetadata(96.0, (d, _) => ((TopFaceView)d).Rebuild()));

    private readonly Canvas _canvas = new();
    private readonly Rectangle[] _top = new Rectangle[9];
    private readonly Rectangle[] _front = new Rectangle[3];
    private readonly Rectangle[] _right = new Rectangle[3];
    private readonly Rectangle[] _back = new Rectangle[3];
    private readonly Rectangle[] _left = new Rectangle[3];

    public TopFaceView()
    {
        IsTabStop = false;
        Content = _canvas;
        Rebuild();
    }

    public CubeState? State
    {
        get => (CubeState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public CaseColorMode ColorMode
    {
        get => (CaseColorMode)GetValue(ColorModeProperty);
        set => SetValue(ColorModeProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private bool OrientationOnly => ColorMode == CaseColorMode.OrientationOnly;

    private void Rebuild()
    {
        _canvas.Children.Clear();
        _canvas.Width = Size;
        _canvas.Height = Size;
        if (OrientationOnly)
        {
            BuildOrientationLayout();
        }
        else
        {
            BuildFullLayout();
        }
        Refresh();
    }

    private Rectangle Make(double x, double y, double w, double h, double radius)
    {
        var r = new Rectangle
        {
            Width = w,
            Height = h,
            RadiusX = radius,
            RadiusY = radius,
            Fill = CubeColors.Brush(CubeColors.Grey),
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        _canvas.Children.Add(r);
        return r;
    }

    private void BuildFullLayout()
    {
        var size = Size;
        var strip = size / 9;
        var gap = Math.Max(1, size / 48);
        var cell = (size - 2 * strip - 4 * gap) / 3;
        var inner = strip + gap;

        for (var i = 0; i < 9; i++)
        {
            var row = i / 3;
            var col = i % 3;
            _top[i] = Make(inner + col * (cell + gap), inner + row * (cell + gap), cell, cell, 2);
        }
        for (var i = 0; i < 3; i++)
        {
            var offset = inner + i * (cell + gap);
            _back[i] = Make(inner + (2 - i) * (cell + gap), 0, cell, strip, 2);          // back row is seen mirrored from above
            _front[i] = Make(offset, size - strip, cell, strip, 2);
            _left[i] = Make(0, inner + (2 - i) * (cell + gap), strip, cell, 2);          // left strip reads back-to-front
            _right[i] = Make(size - strip, offset, strip, cell, 2);
        }
    }

    private void BuildOrientationLayout()
    {
        var size = Size;
        var bar = Math.Max(2, size * 0.06);
        var barGap = Math.Max(1, size * 0.035);
        var margin = bar + barGap;
        var grid = size - 2 * margin;
        var line = Math.Max(1, grid / 40);
        var border = line * 1.5;
        var cell = (grid - 2 * border - 2 * line) / 3;
        var barLength = cell * 0.8;
        var barInset = (cell - barLength) / 2;
        var barStroke = Math.Max(0.75, bar / 6);

        // The black backdrop shows through between the stickers as the grid lines and outer border.
        var backdrop = Make(margin, margin, grid, grid, 0);
        backdrop.Fill = CubeColors.Brush(CubeColors.Outline);

        double CellOffset(int i) => margin + border + i * (cell + line);

        for (var i = 0; i < 9; i++)
        {
            _top[i] = Make(CellOffset(i % 3), CellOffset(i / 3), cell, cell, 0);
        }

        Rectangle Bar(double x, double y, double w, double h)
        {
            var r = Make(x, y, w, h, 0);
            r.Fill = CubeColors.Brush(CubeColors.Yellow);
            r.Stroke = CubeColors.Brush(CubeColors.Outline);
            r.StrokeThickness = barStroke;
            return r;
        }

        for (var i = 0; i < 3; i++)
        {
            var along = CellOffset(i) + barInset;
            var mirrored = CellOffset(2 - i) + barInset;
            _back[i] = Bar(mirrored, 0, barLength, bar);                      // back row is seen mirrored from above
            _front[i] = Bar(along, size - bar, barLength, bar);
            _left[i] = Bar(0, mirrored, bar, barLength);                      // left strip reads back-to-front
            _right[i] = Bar(size - bar, along, bar, barLength);
        }
    }

    private void Refresh()
    {
        var image = CaseImage.From(State ?? CubeState.Solved);
        if (OrientationOnly)
        {
            for (var i = 0; i < 9; i++)
            {
                _top[i].Fill = CubeColors.Brush(image.Top[i] == Face.U ? CubeColors.Yellow : CubeColors.Grey);
            }
            for (var i = 0; i < 3; i++)
            {
                _front[i].Visibility = SideBar(image.Front[i]);
                _right[i].Visibility = SideBar(image.Right[i]);
                _back[i].Visibility = SideBar(image.Back[i]);
                _left[i].Visibility = SideBar(image.Left[i]);
            }
            return;
        }

        for (var i = 0; i < 9; i++)
        {
            _top[i].Fill = CubeColors.Brush(image.Top[i]);
        }
        for (var i = 0; i < 3; i++)
        {
            _front[i].Fill = CubeColors.Brush(image.Front[i]);
            _right[i].Fill = CubeColors.Brush(image.Right[i]);
            _back[i].Fill = CubeColors.Brush(image.Back[i]);
            _left[i].Fill = CubeColors.Brush(image.Left[i]);
        }
    }

    private static Visibility SideBar(Face face) => face == Face.U ? Visibility.Visible : Visibility.Collapsed;
}
