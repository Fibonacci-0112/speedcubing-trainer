using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Controls;

/// <summary>Picks the right diagram for a case: last-layer view for OLL/PLL, isometric cube for F2L.</summary>
public sealed partial class CaseView : UserControl
{
    public static readonly DependencyProperty CaseProperty = DependencyProperty.Register(
        nameof(Case), typeof(AlgCase), typeof(CaseView), new PropertyMetadata(null, (d, _) => ((CaseView)d).Refresh()));

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(CubeState), typeof(CaseView), new PropertyMetadata(null, (d, _) => ((CaseView)d).Refresh()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(CaseView), new PropertyMetadata(96.0, (d, _) => ((CaseView)d).Refresh()));

    public static readonly DependencyProperty ShowArrowsProperty = DependencyProperty.Register(
        nameof(ShowArrows), typeof(bool), typeof(CaseView), new PropertyMetadata(true, (d, _) => ((CaseView)d).Refresh()));

    private readonly TopFaceView _top = new();
    private readonly IsometricCubeView _iso = new();
    private readonly Grid _root = new();

    public CaseView()
    {
        IsTabStop = false;
        _root.Children.Add(_top);
        _root.Children.Add(_iso);
        Content = _root;
        Refresh();
    }

    /// <summary>The case, which decides the view type and colour mode.</summary>
    public AlgCase? Case
    {
        get => (AlgCase?)GetValue(CaseProperty);
        set => SetValue(CaseProperty, value);
    }

    /// <summary>Optional state to draw instead of the case's canonical state (e.g. a specific setup).</summary>
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

    /// <summary>Whether PLL diagrams show the piece-movement arrows (hidden while the trainer quizzes).</summary>
    public bool ShowArrows
    {
        get => (bool)GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    private void Refresh()
    {
        var c = Case;
        var state = State ?? c?.State ?? CubeState.Solved;
        var isF2L = c?.Set == AlgorithmSetKind.F2L;
        _iso.Visibility = isF2L ? Visibility.Visible : Visibility.Collapsed;
        _top.Visibility = isF2L ? Visibility.Collapsed : Visibility.Visible;
        _iso.Size = Size;
        _top.Size = Size;
        _iso.State = state;
        _top.State = state;
        _top.ShowArrows = ShowArrows;
        _top.ColorMode = c?.Set == AlgorithmSetKind.OLL ? CaseColorMode.OrientationOnly : CaseColorMode.Full;
    }
}
