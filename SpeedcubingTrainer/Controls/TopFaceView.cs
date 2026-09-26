using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Controls;

/// <summary>
/// The classic last-layer diagram: the top face seen from above with the top row of each side
/// drawn as a thin strip around it. In orientation-only mode every non-U colour is grey.
/// </summary>
public sealed partial class TopFaceView : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(CubeState), typeof(TopFaceView), new PropertyMetadata(null, (d, _) => ((TopFaceView)d).Refresh()));

    public static readonly DependencyProperty ColorModeProperty = DependencyProperty.Register(
        nameof(ColorMode), typeof(CaseColorMode), typeof(TopFaceView), new PropertyMetadata(CaseColorMode.Full, (d, _) => ((TopFaceView)d).Refresh()));

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

    private void Rebuild()
    {
        _canvas.Children.Clear();
        var size = Size;
        var strip = size / 9;
        var gap = Math.Max(1, size / 48);
        var cell = (size - 2 * strip - 4 * gap) / 3;
        var inner = strip + gap;
        _canvas.Width = size;
        _canvas.Height = size;

        Rectangle Make(double x, double y, double w, double h)
        {
            var r = new Rectangle
            {
                Width = w,
                Height = h,
                RadiusX = 2,
                RadiusY = 2,
                Fill = CubeColors.Brush(CubeColors.Grey),
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            _canvas.Children.Add(r);
            return r;
        }

        for (var i = 0; i < 9; i++)
        {
            var row = i / 3;
            var col = i % 3;
            _top[i] = Make(inner + col * (cell + gap), inner + row * (cell + gap), cell, cell);
        }
        for (var i = 0; i < 3; i++)
        {
            var offset = inner + i * (cell + gap);
            _back[i] = Make(inner + (2 - i) * (cell + gap), 0, cell, strip);          // back row is seen mirrored from above
            _front[i] = Make(offset, size - strip, cell, strip);
            _left[i] = Make(0, inner + (2 - i) * (cell + gap), strip, cell);          // left strip reads back-to-front
            _right[i] = Make(size - strip, offset, strip, cell);
        }
        Refresh();
    }

    private void Refresh()
    {
        var state = State ?? CubeState.Solved;
        var image = CaseImage.From(state);
        var orientationOnly = ColorMode == CaseColorMode.OrientationOnly;
        Brush Paint(Face f) => orientationOnly && f != Face.U ? CubeColors.Brush(CubeColors.Grey) : CubeColors.Brush(f);
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
    }
}
