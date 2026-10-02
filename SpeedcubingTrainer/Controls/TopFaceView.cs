using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;
using Windows.Foundation;

namespace SpeedcubingTrainer.Controls;

/// <summary>
/// The classic last-layer diagram in the familiar printed style: the top face seen from above as a
/// black-lined grid, with the top row of each side drawn around it.
/// In full-colour mode (PLL) the cube is shown yellow on top, each side's top row is a thin strip of
/// stickers against the grid, and black arrows show where each piece has to go.
/// In orientation-only mode (OLL) oriented stickers are yellow and the rest grey, and a short yellow
/// bar beside the grid marks each last-layer sticker that faces sideways (no bar where it does not).
/// </summary>
public sealed partial class TopFaceView : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(CubeState), typeof(TopFaceView), new PropertyMetadata(null, (d, _) => ((TopFaceView)d).Refresh()));

    public static readonly DependencyProperty ColorModeProperty = DependencyProperty.Register(
        nameof(ColorMode), typeof(CaseColorMode), typeof(TopFaceView), new PropertyMetadata(CaseColorMode.Full, (d, _) => ((TopFaceView)d).Rebuild()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(TopFaceView), new PropertyMetadata(96.0, (d, _) => ((TopFaceView)d).Rebuild()));

    public static readonly DependencyProperty ShowArrowsProperty = DependencyProperty.Register(
        nameof(ShowArrows), typeof(bool), typeof(TopFaceView), new PropertyMetadata(true, (d, _) => ((TopFaceView)d).Refresh()));

    private readonly Canvas _canvas = new();
    private readonly Rectangle[] _top = new Rectangle[9];
    private readonly Rectangle[] _front = new Rectangle[3];
    private readonly Rectangle[] _right = new Rectangle[3];
    private readonly Rectangle[] _back = new Rectangle[3];
    private readonly Rectangle[] _left = new Rectangle[3];
    private readonly List<UIElement> _arrows = [];

    // Grid geometry from the last build, used to place the arrows.
    private double _gridStart;
    private double _cell;
    private double _line;

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

    /// <summary>Whether full-colour diagrams show the piece-movement arrows.</summary>
    public bool ShowArrows
    {
        get => (bool)GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    private bool OrientationOnly => ColorMode == CaseColorMode.OrientationOnly;

    private void Rebuild()
    {
        _canvas.Children.Clear();
        _arrows.Clear();
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

    private Rectangle Make(double x, double y, double w, double h)
    {
        var r = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = CubeColors.Brush(CubeColors.Grey),
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        _canvas.Children.Add(r);
        return r;
    }

    private double CellOffset(int i) => _gridStart + i * (_cell + _line);

    /// <summary>
    /// Draws the black backdrop that shows through between the stickers as the grid lines and outer
    /// border, and the nine top stickers on it. Returns the outer border's thickness.
    /// </summary>
    private double BuildGrid(double margin)
    {
        var grid = Size - 2 * margin;
        _line = Math.Max(1, grid / 40);
        var border = _line * 1.5;
        _cell = (grid - 2 * border - 2 * _line) / 3;
        _gridStart = margin + border;

        var backdrop = Make(margin, margin, grid, grid);
        backdrop.Fill = CubeColors.Brush(CubeColors.Outline);
        for (var i = 0; i < 9; i++)
        {
            _top[i] = Make(CellOffset(i % 3), CellOffset(i / 3), _cell, _cell);
        }
        return border;
    }

    private void BuildFullLayout()
    {
        var size = Size;
        var strip = size * 0.07;
        var border = BuildGrid(strip);

        // Each side sticker is outlined and tucked under the grid's border, so only one line
        // separates it from the top face.
        var length = _cell + 2 * _line;
        var depth = strip + border;
        Rectangle Strip(double x, double y, double w, double h)
        {
            var r = Make(x, y, w, h);
            r.Stroke = CubeColors.Brush(CubeColors.Outline);
            r.StrokeThickness = _line;
            return r;
        }

        for (var i = 0; i < 3; i++)
        {
            var along = CellOffset(i) - _line;
            var mirrored = CellOffset(2 - i) - _line;
            _back[i] = Strip(mirrored, 0, length, depth);                 // back row is seen mirrored from above
            _front[i] = Strip(along, size - depth, length, depth);
            _left[i] = Strip(0, mirrored, depth, length);                 // left strip reads back-to-front
            _right[i] = Strip(size - depth, along, depth, length);
        }
    }

    private void BuildOrientationLayout()
    {
        var size = Size;
        var bar = Math.Max(2, size * 0.06);
        var barGap = Math.Max(1, size * 0.035);
        BuildGrid(bar + barGap);
        var barLength = _cell * 0.8;
        var barInset = (_cell - barLength) / 2;
        var barStroke = Math.Max(0.75, bar / 6);

        Rectangle Bar(double x, double y, double w, double h)
        {
            var r = Make(x, y, w, h);
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
        foreach (var arrow in _arrows)
        {
            _canvas.Children.Remove(arrow);
        }
        _arrows.Clear();

        var state = State ?? CubeState.Solved;
        var image = CaseImage.From(state);
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

        static Brush Paint(Face f) => CubeColors.Brush(CubeColors.ForYellowTop(f));
        for (var i = 0; i < 9; i++)
        {
            _top[i].Fill = Paint(image.Top[i]);
        }
        for (var i = 0; i < 3; i++)
        {
            _front[i].Fill = Paint(image.Front[i]);
            _right[i].Fill = Paint(image.Right[i]);
            _back[i].Fill = Paint(image.Back[i]);
            _left[i].Fill = Paint(image.Left[i]);
        }
        if (ShowArrows)
        {
            DrawArrows(LastLayerArrows.From(state));
        }
    }

    private static Visibility SideBar(Face face) => face == Face.U ? Visibility.Visible : Visibility.Collapsed;

    private void DrawArrows(IReadOnlyList<PieceArrow> arrows)
    {
        var moving = arrows.SelectMany(a => new[] { a.From, a.To }).ToHashSet();
        var thickness = Math.Max(1.5, _cell * 0.1);
        var headLength = _cell * 0.34;
        var headWidth = _cell * 0.34;
        var inset = _cell * 0.15;
        var centre = CellOffset(1) + _cell / 2;
        var brush = CubeColors.Brush(CubeColors.Outline);

        Point Centre(int i) => new(CellOffset(i % 3) + _cell / 2, CellOffset(i / 3) + _cell / 2);

        foreach (var arrow in arrows)
        {
            var from = Centre(arrow.From);
            var to = Centre(arrow.To);

            // A corner-to-corner arrow along a side would run through the edge between them; when that
            // edge is moving too, nudge the arrow towards the middle so the two do not overlap.
            var middle = (arrow.From + arrow.To) / 2;
            if (IsCorner(arrow.From) && IsCorner(arrow.To) && middle != 4 && moving.Contains(middle))
            {
                var shift = _cell * 0.25;
                double Towards(double value) => value + Math.Sign(centre - value) * shift;
                if (arrow.From / 3 == arrow.To / 3)
                {
                    from = new Point(from.X, Towards(from.Y));
                    to = new Point(to.X, Towards(to.Y));
                }
                else
                {
                    from = new Point(Towards(from.X), from.Y);
                    to = new Point(Towards(to.X), to.Y);
                }
            }

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var ux = dx / length;
            var uy = dy / length;
            var start = new Point(from.X + ux * inset, from.Y + uy * inset);
            var end = new Point(to.X - ux * inset, to.Y - uy * inset);

            // The shaft stops at the base of each head so its square end does not poke through the tip.
            var shaftStart = arrow.TwoWay ? new Point(start.X + ux * headLength * 0.9, start.Y + uy * headLength * 0.9) : start;
            var shaftEnd = new Point(end.X - ux * headLength * 0.9, end.Y - uy * headLength * 0.9);
            Add(new Line
            {
                X1 = shaftStart.X,
                Y1 = shaftStart.Y,
                X2 = shaftEnd.X,
                Y2 = shaftEnd.Y,
                Stroke = brush,
                StrokeThickness = thickness,
            });
            Add(Head(end, ux, uy));
            if (arrow.TwoWay)
            {
                Add(Head(start, -ux, -uy));
            }
        }

        Polygon Head(Point tip, double ux, double uy)
        {
            var baseX = tip.X - ux * headLength;
            var baseY = tip.Y - uy * headLength;
            var px = -uy * headWidth / 2;
            var py = ux * headWidth / 2;
            return new Polygon
            {
                Fill = brush,
                Points = [tip, new Point(baseX + px, baseY + py), new Point(baseX - px, baseY - py)],
            };
        }

        void Add(UIElement element)
        {
            _arrows.Add(element);
            _canvas.Children.Add(element);
        }
    }

    private static bool IsCorner(int topIndex) => topIndex is 0 or 2 or 6 or 8;
}
