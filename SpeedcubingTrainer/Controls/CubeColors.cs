using SpeedcubingTrainer.Core.Cube;
using Windows.UI;

namespace SpeedcubingTrainer.Controls;

public static class CubeColors
{
    public static readonly Color White = Color.FromArgb(255, 255, 255, 255);
    public static readonly Color Red = Color.FromArgb(255, 220, 30, 30);
    public static readonly Color Green = Color.FromArgb(255, 20, 170, 60);
    public static readonly Color Yellow = Color.FromArgb(255, 255, 215, 0);
    public static readonly Color Orange = Color.FromArgb(255, 255, 130, 0);
    public static readonly Color Blue = Color.FromArgb(255, 30, 90, 230);
    public static readonly Color Grey = Color.FromArgb(255, 110, 110, 110);
    public static readonly Color Outline = Color.FromArgb(255, 20, 20, 20);

    public static Color For(Face face) => face switch
    {
        Face.U => White,
        Face.R => Red,
        Face.F => Green,
        Face.D => Yellow,
        Face.L => Orange,
        Face.B => Blue,
        _ => Grey,
    };

    private static readonly Dictionary<Color, SolidColorBrush> Brushes = new();

    public static SolidColorBrush Brush(Color color)
    {
        if (!Brushes.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(color);
            Brushes[color] = brush;
        }
        return brush;
    }

    public static SolidColorBrush Brush(Face face) => Brush(For(face));
}
