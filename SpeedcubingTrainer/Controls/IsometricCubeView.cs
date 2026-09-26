using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Cube;
using Windows.Foundation;

namespace SpeedcubingTrainer.Controls;

/// <summary>Isometric view showing the U, F and R faces, used for F2L cases.</summary>
public sealed partial class IsometricCubeView : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(CubeState), typeof(IsometricCubeView), new PropertyMetadata(null, (d, _) => ((IsometricCubeView)d).Refresh()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(IsometricCubeView), new PropertyMetadata(120.0, (d, _) => ((IsometricCubeView)d).Rebuild()));

    private readonly Canvas _canvas = new();
    private readonly Polygon[] _up = new Polygon[9];
    private readonly Polygon[] _front = new Polygon[9];
    private readonly Polygon[] _right = new Polygon[9];

    public IsometricCubeView()
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

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private void Rebuild()
    {
        _canvas.Children.Clear();
        var size = Size;
        _canvas.Width = size;
        _canvas.Height = size;
        // Cube of edge length e centred in the canvas; isometric axes.
        var e = size / 2.25;
        var cx = size / 2;
        var cy = size / 2;
        var ax = new Point(Math.Cos(Math.PI / 6) * e / 3, Math.Sin(Math.PI / 6) * e / 3);   // +x (right) direction per sticker
        var az = new Point(-Math.Cos(Math.PI / 6) * e / 3, Math.Sin(Math.PI / 6) * e / 3);  // +z (toward viewer) direction
        var ay = new Point(0, -e / 3);                                                        // +y (up) direction
        var origin = new Point(cx, cy); // point (0,0,0) at the centre of the cube volume, in cube units of sticker size

        Point P(double x, double y, double z) => new(
            origin.X + x * ax.X + y * ay.X + z * az.X,
            origin.Y + x * ax.Y + y * ay.Y + z * az.Y);

        Polygon Make(Point a, Point b, Point c, Point d)
        {
            var poly = new Polygon
            {
                Points = [a, b, c, d],
                Fill = CubeColors.Brush(CubeColors.Grey),
                Stroke = CubeColors.Brush(CubeColors.Outline),
                StrokeThickness = Math.Max(0.6, size / 120),
                StrokeLineJoin = PenLineJoin.Round,
            };
            _canvas.Children.Add(poly);
            return poly;
        }

        // Coordinates run from -1.5 to 1.5 in sticker units. U face: y = 1.5, rows from back (z = -1.5) to front.
        for (var i = 0; i < 9; i++)
        {
            var row = i / 3;
            var col = i % 3;
            var x0 = -1.5 + col;
            var z0 = -1.5 + row;
            _up[i] = Make(P(x0, 1.5, z0), P(x0 + 1, 1.5, z0), P(x0 + 1, 1.5, z0 + 1), P(x0, 1.5, z0 + 1));
        }
        // F face: z = 1.5, rows from top (y = 1.5) down.
        for (var i = 0; i < 9; i++)
        {
            var row = i / 3;
            var col = i % 3;
            var x0 = -1.5 + col;
            var y0 = 1.5 - row;
            _front[i] = Make(P(x0, y0, 1.5), P(x0 + 1, y0, 1.5), P(x0 + 1, y0 - 1, 1.5), P(x0, y0 - 1, 1.5));
        }
        // R face: x = 1.5, columns from front (z = 1.5) to back, rows from top down.
        for (var i = 0; i < 9; i++)
        {
            var row = i / 3;
            var col = i % 3;
            var z0 = 1.5 - col;
            var y0 = 1.5 - row;
            _right[i] = Make(P(1.5, y0, z0), P(1.5, y0, z0 - 1), P(1.5, y0 - 1, z0 - 1), P(1.5, y0 - 1, z0));
        }
        Refresh();
    }

    private void Refresh()
    {
        var state = State ?? CubeState.Solved;
        var up = state.GetFace(Face.U);
        var front = state.GetFace(Face.F);
        var right = state.GetFace(Face.R);
        for (var i = 0; i < 9; i++)
        {
            _up[i].Fill = CubeColors.Brush(up[i]);
            _front[i].Fill = CubeColors.Brush(front[i]);
            _right[i].Fill = CubeColors.Brush(right[i]);
        }
    }
}
