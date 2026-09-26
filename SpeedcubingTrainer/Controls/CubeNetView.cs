using Microsoft.UI.Xaml.Shapes;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Controls;

/// <summary>Draws a cube state as the standard unfolded net (U on top, L F R B in the middle row, D below).</summary>
public sealed partial class CubeNetView : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(CubeState), typeof(CubeNetView), new PropertyMetadata(null, (d, _) => ((CubeNetView)d).Refresh()));

    public static readonly DependencyProperty StickerSizeProperty = DependencyProperty.Register(
        nameof(StickerSize), typeof(double), typeof(CubeNetView), new PropertyMetadata(14.0, (d, _) => ((CubeNetView)d).Rebuild()));

    private readonly Rectangle[] _stickers = new Rectangle[CubeState.FaceletCount];
    private readonly Grid _grid = new();

    // Column and row of each face's top-left cell in the 12 x 9 net.
    private static readonly (int Col, int Row)[] FaceOrigins =
    [
        (3, 0), // U
        (6, 3), // R
        (3, 3), // F
        (3, 6), // D
        (0, 3), // L
        (9, 3), // B
    ];

    public CubeNetView()
    {
        IsTabStop = false;
        Content = _grid;
        Rebuild();
    }

    public CubeState? State
    {
        get => (CubeState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public double StickerSize
    {
        get => (double)GetValue(StickerSizeProperty);
        set => SetValue(StickerSizeProperty, value);
    }

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        var size = StickerSize;
        var gap = Math.Max(1, size / 8);
        for (var c = 0; c < 12; c++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(size + gap) });
        }
        for (var r = 0; r < 9; r++)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(size + gap) });
        }
        for (var i = 0; i < CubeState.FaceletCount; i++)
        {
            var face = i / 9;
            var cell = i % 9;
            var (col, row) = FaceOrigins[face];
            var rect = new Rectangle
            {
                Width = size,
                Height = size,
                RadiusX = size / 6,
                RadiusY = size / 6,
                Stroke = CubeColors.Brush(CubeColors.Outline),
                StrokeThickness = Math.Max(0.5, size / 14),
                Fill = CubeColors.Brush(CubeColors.Grey),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetColumn(rect, col + cell % 3);
            Grid.SetRow(rect, row + cell / 3);
            _stickers[i] = rect;
            _grid.Children.Add(rect);
        }
        Refresh();
    }

    private void Refresh()
    {
        var state = State ?? CubeState.Solved;
        for (var i = 0; i < CubeState.FaceletCount; i++)
        {
            _stickers[i].Fill = CubeColors.Brush(state[i]);
        }
    }
}
