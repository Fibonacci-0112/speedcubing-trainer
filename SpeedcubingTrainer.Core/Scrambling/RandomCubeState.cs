using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Scrambling;

/// <summary>Generates cube states uniformly at random over all 43 quintillion reachable states.</summary>
public static class RandomCubeState
{
    public static CubieCube Next(IRandomSource random)
    {
        var cube = CubieCube.Solved;
        Shuffle(cube.Cp, random);
        Shuffle(cube.Ep, random);
        if (cube.CornerParity != cube.EdgeParity)
        {
            (cube.Ep[0], cube.Ep[1]) = (cube.Ep[1], cube.Ep[0]);
        }

        var twist = 0;
        for (var i = 0; i < 7; i++)
        {
            cube.Co[i] = (byte)random.Next(3);
            twist += cube.Co[i];
        }
        cube.Co[7] = (byte)((3 - twist % 3) % 3);

        var flip = 0;
        for (var i = 0; i < 11; i++)
        {
            cube.Eo[i] = (byte)random.Next(2);
            flip += cube.Eo[i];
        }
        cube.Eo[11] = (byte)(flip & 1);
        return cube;
    }

    private static void Shuffle(byte[] items, IRandomSource random)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
