using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Scrambling;
using Xunit.Abstractions;

namespace SpeedcubingTrainer.Core.Tests.Algorithms;

public class AlgorithmDataTests(ITestOutputHelper output)
{
    private static readonly Move[] UTurns = [new(MoveTarget.U, Turn.Cw), new(MoveTarget.U, Turn.Double), new(MoveTarget.U, Turn.Ccw)];

    [Theory]
    [InlineData(AlgorithmSetKind.F2L, 41)]
    [InlineData(AlgorithmSetKind.OLL, 57)]
    [InlineData(AlgorithmSetKind.PLL, 21)]
    public void SetsLoadWithExpectedCounts(AlgorithmSetKind kind, int count)
    {
        var set = AlgorithmSetLoader.Load(kind);
        Assert.Equal(kind, set.Kind);
        Assert.Equal(count, set.Cases.Count);
        Assert.Equal(count, set.Cases.Select(c => c.Id).Distinct().Count());
        Assert.Equal(count, set.Cases.Select(c => c.Number).Distinct().Count());
        Assert.All(set.Cases, c => Assert.Contains(c.Group, set.Groups));
        Assert.All(set.Cases, c => Assert.NotEmpty(c.Algorithms));
        Assert.Same(set, AlgorithmSetLoader.Load(kind));
    }

    [Fact]
    public void EveryOllAlgorithmOrientsTheLastLayerWithoutBreakingF2L()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.OLL);
        var failures = new List<string>();
        foreach (var c in set.Cases)
        {
            Assert.True(IsF2LSolved(c.State), $"{c.Id}: the case state breaks F2L");
            Assert.False(IsLastLayerOriented(c.State), $"{c.Id}: the case state is already oriented");
            foreach (var alg in c.Algorithms)
            {
                if (!FromSomeAngle(c.State, alg, s => IsF2LSolved(s) && IsLastLayerOriented(s)))
                {
                    failures.Add($"{c.Id}: '{alg}' does not solve the case");
                }
            }
        }
        Assert.Empty(failures);
    }

    /// <summary>Alternative algorithms are often written for another starting angle; accept any pre-AUF.</summary>
    private static bool FromSomeAngle(CubeState state, Algorithm alg, Func<CubeState, bool> solved)
    {
        var s = state;
        for (var k = 0; k < 4; k++)
        {
            if (solved(s.Apply(alg).NormalizeUpFace().RecolorToHome()))
            {
                return true;
            }
            s = s.Apply(UTurns[0]);
        }
        return false;
    }

    [Fact]
    public void EveryPllAlgorithmSolvesTheCaseUpToAuf()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.PLL);
        var failures = new List<string>();
        foreach (var c in set.Cases)
        {
            Assert.True(IsF2LSolved(c.State) && IsLastLayerOriented(c.State), $"{c.Id}: case state is not an OLL-solved cube");
            Assert.False(IsSolvedUpToAuf(c.State), $"{c.Id}: the case state is already solved");
            foreach (var alg in c.Algorithms)
            {
                if (!FromSomeAngle(c.State, alg, IsSolvedUpToAuf))
                {
                    failures.Add($"{c.Id}: '{alg}' does not solve the case");
                }
            }
        }
        Assert.Empty(failures);
    }

    [Fact]
    public void EveryF2LAlgorithmSolvesItsPairWithoutBreakingTheRest()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.F2L);
        var failures = new List<string>();
        foreach (var c in set.Cases)
        {
            if (!IsCrossAndOtherSlotsSolved(c.State))
            {
                failures.Add($"{c.Id}: the case state disturbs pieces outside the FR slot and top layer");
            }
            if (IsFrSlotSolved(c.State))
            {
                failures.Add($"{c.Id}: the case state is already solved");
            }
            foreach (var alg in c.Algorithms)
            {
                if (!FromSomeAngle(c.State, alg, IsF2LSolved))
                {
                    failures.Add($"{c.Id}: '{alg}' does not solve the pair");
                }
            }
        }
        Assert.Empty(failures);
    }

    [Fact]
    public void OllCasesAreDistinctOrientationPatterns()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.OLL);
        var seen = new Dictionary<string, string>();
        foreach (var c in set.Cases)
        {
            var key = OrientationPatternKey(c.State);
            Assert.False(seen.TryGetValue(key, out var other), $"{c.Id} and {other} are the same OLL case");
            seen[key] = c.Id;
        }
    }

    [Fact]
    public void PllCasesAreDistinctPermutations()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.PLL);
        var seen = new Dictionary<string, string>();
        foreach (var c in set.Cases)
        {
            var key = PermutationKey(c.Primary);
            Assert.False(seen.TryGetValue(key, out var other), $"{c.Id} and {other} are the same PLL case");
            seen[key] = c.Id;
        }
    }

    [Fact]
    public void F2LCasesCoverAllFortyOne()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.F2L);
        var expected = ExpectedF2LSignatures();
        Assert.Equal(41, expected.Count);
        var byCase = set.Cases.ToDictionary(c => c.Id, c => F2LSignature(c.State));
        var duplicates = byCase.GroupBy(kv => kv.Value).Where(g => g.Count() > 1).Select(g => string.Join(" = ", g.Select(kv => kv.Key))).ToList();
        var missing = expected.Except(byCase.Values).ToList();
        foreach (var d in duplicates)
        {
            output.WriteLine($"duplicate: {d}");
        }
        foreach (var m in missing)
        {
            output.WriteLine($"missing: {m}  suggestion: {F2LAlgorithmFinder.Suggest(m)}");
        }
        Assert.Empty(duplicates);
        Assert.Empty(missing);
    }

    [Fact]
    public void SetupScramblesReproduceTheCase()
    {
        var generator = new CaseSetupGenerator(new SeededRandom(3));
        foreach (var set in AlgorithmSetLoader.LoadAll())
        {
            foreach (var c in set.Cases)
            {
                for (var i = 0; i < 3; i++)
                {
                    var setup = generator.Create(c, set.SetupDefaults);
                    Assert.Equal(setup.State, CubeState.Solved.Apply(setup.Setup));
                    var solved = setup.State.Apply(setup.Solution).NormalizeUpFace().RecolorToHome();
                    switch (set.Kind)
                    {
                        case AlgorithmSetKind.PLL:
                            Assert.True(IsSolvedUpToAuf(solved), $"{c.Id}: setup {setup.Setup} is not solved by {setup.Solution}");
                            break;
                        case AlgorithmSetKind.OLL:
                            Assert.True(IsF2LSolved(solved) && IsLastLayerOriented(solved), $"{c.Id}: setup {setup.Setup} is not solved by {setup.Solution}");
                            break;
                        default:
                            Assert.True(IsF2LSolved(solved), $"{c.Id}: setup {setup.Setup} is not solved by {setup.Solution}");
                            break;
                    }
                }
            }
        }
    }

    [Fact]
    public void CaseImageExposesTopAndSides()
    {
        var t = AlgorithmSetLoader.Load(AlgorithmSetKind.PLL).Find("PLL-T")!;
        var image = CaseImage.From(t.State);
        Assert.Equal(9, image.Top.Length);
        Assert.All(image.Top, f => Assert.Equal(Face.U, f));
        Assert.Equal(3, image.Front.Length);
        // T perm: headlights on the left, bar on the right after the standard algorithm's inverse.
        Assert.Equal(image.Left[0], image.Left[2]);
    }

    private static bool IsF2LSolved(CubeState s)
    {
        for (var i = 0; i < 9; i++)
        {
            if (s[27 + i] != Face.D)
            {
                return false;
            }
        }
        foreach (var face in new[] { Face.R, Face.F, Face.L, Face.B })
        {
            var stickers = s.GetFace(face);
            for (var i = 3; i < 9; i++)
            {
                if (stickers[i] != face)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsLastLayerOriented(CubeState s)
    {
        for (var i = 0; i < 9; i++)
        {
            if (s[i] != Face.U)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsSolvedUpToAuf(CubeState s)
    {
        if (s.IsSolved)
        {
            return true;
        }
        foreach (var u in UTurns)
        {
            if (s.Apply(u).IsSolved)
            {
                return true;
            }
        }
        return false;
    }

    // Facelets of the FR slot: corner DFR (D3 F9 R7) and edge FR (F6 R4).
    private static readonly HashSet<int> FrSlotFacelets = [29, 26, 15, 23, 12];

    private static bool IsCrossAndOtherSlotsSolved(CubeState s)
    {
        for (var i = 9; i < 54; i++)
        {
            var face = (Face)(i / 9);
            var cell = i % 9;
            if (face != Face.D && cell < 3)
            {
                continue; // top row of a side face belongs to the U layer
            }
            if (FrSlotFacelets.Contains(i))
            {
                continue;
            }
            if (s[i] != face)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsFrSlotSolved(CubeState s) => FrSlotFacelets.All(i => s[i] == (Face)(i / 9));

    private static string OrientationPatternKey(CubeState state)
    {
        var best = string.Empty;
        var s = state;
        for (var k = 0; k < 4; k++)
        {
            var image = CaseImage.From(s);
            var key = string.Concat(image.Top.Concat(image.Front).Concat(image.Right).Concat(image.Back).Concat(image.Left).Select(f => f == Face.U ? '1' : '0'));
            if (best.Length == 0 || string.CompareOrdinal(key, best) < 0)
            {
                best = key;
            }
            s = s.Apply(UTurns[0]);
        }
        return best;
    }

    private static string PermutationKey(Algorithm primary)
    {
        var best = string.Empty;
        var inverse = primary.Inverse();
        for (var a = 0; a < 4; a++)
        {
            var start = a == 0 ? CubeState.Solved : CubeState.Solved.Apply(UTurns[a - 1]);
            var s = start.Apply(inverse).NormalizeOrientation();
            for (var b = 0; b < 4; b++)
            {
                var key = s.ToString();
                if (best.Length == 0 || string.CompareOrdinal(key, best) < 0)
                {
                    best = key;
                }
                s = s.Apply(UTurns[0]);
            }
        }
        return best;
    }

    /// <summary>Where the DFR corner and FR edge are, modulo U turns.</summary>
    internal static string F2LSignature(CubeState state)
    {
        var best = string.Empty;
        var s = state;
        for (var k = 0; k < 4; k++)
        {
            var cube = s.ToCubieCube();
            var cornerSlot = Array.IndexOf(cube.Cp, (byte)Corner.DFR);
            var edgeSlot = Array.IndexOf(cube.Ep, (byte)Edge.FR);
            var key = $"c{cornerSlot}o{cube.Co[cornerSlot]}e{edgeSlot}o{cube.Eo[edgeSlot]}";
            if (best.Length == 0 || string.CompareOrdinal(key, best) < 0)
            {
                best = key;
            }
            s = s.Apply(UTurns[0]);
        }
        return best;
    }

    private static HashSet<string> ExpectedF2LSignatures()
    {
        var result = new HashSet<string>();
        int[] cornerSlots = [(int)Corner.URF, (int)Corner.UFL, (int)Corner.ULB, (int)Corner.UBR, (int)Corner.DFR];
        int[] edgeSlots = [(int)Edge.UR, (int)Edge.UF, (int)Edge.UL, (int)Edge.UB, (int)Edge.FR];
        foreach (var cs in cornerSlots)
        {
            for (var co = 0; co < 3; co++)
            {
                foreach (var es in edgeSlots)
                {
                    for (var eo = 0; eo < 2; eo++)
                    {
                        if (cs == (int)Corner.DFR && co == 0 && es == (int)Edge.FR && eo == 0)
                        {
                            continue;
                        }
                        var cube = CubieCube.Solved;
                        // Place the pair; swap the displaced pieces into the vacated slots (validity is irrelevant here).
                        (cube.Cp[cs], cube.Cp[(int)Corner.DFR]) = (cube.Cp[(int)Corner.DFR], cube.Cp[cs]);
                        cube.Co[cs] = (byte)co;
                        (cube.Ep[es], cube.Ep[(int)Edge.FR]) = (cube.Ep[(int)Edge.FR], cube.Ep[es]);
                        cube.Eo[es] = (byte)eo;
                        var best = string.Empty;
                        for (var k = 0; k < 4; k++)
                        {
                            var cornerSlot = Array.IndexOf(cube.Cp, (byte)Corner.DFR);
                            var edgeSlot = Array.IndexOf(cube.Ep, (byte)Edge.FR);
                            var key = $"c{cornerSlot}o{cube.Co[cornerSlot]}e{edgeSlot}o{cube.Eo[edgeSlot]}";
                            if (best.Length == 0 || string.CompareOrdinal(key, best) < 0)
                            {
                                best = key;
                            }
                            cube.Multiply(CubieCube.MoveCubes[(int)Face.U]);
                        }
                        result.Add(best);
                    }
                }
            }
        }
        return result;
    }
}
