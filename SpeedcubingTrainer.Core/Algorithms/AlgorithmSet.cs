using System.Text.Json.Serialization;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Algorithms;

public enum AlgorithmSetKind
{
    F2L,
    OLL,
    PLL,
}

public enum CaseView
{
    /// <summary>Top face with the top row of each side: the usual OLL/PLL diagram.</summary>
    TopWithSides,
    /// <summary>The whole cube, for F2L.</summary>
    FullCube,
}

public enum CaseColorMode
{
    /// <summary>Only the U colour matters (OLL): every other sticker is drawn grey.</summary>
    OrientationOnly,
    Full,
}

public sealed record CaseSetupOptions(bool PreAuf = true, bool PostAuf = true, bool YRotation = false);

/// <summary>One case of an algorithm set: the state reached by inverting its primary algorithm, and how to solve it.</summary>
public sealed class AlgCase
{
    internal AlgCase(AlgorithmSetKind set, string id, int number, string name, string group, IReadOnlyList<Algorithm> algorithms, string? recognition, string? probability)
    {
        Set = set;
        Id = id;
        Number = number;
        Name = name;
        Group = group;
        Algorithms = algorithms;
        Recognition = recognition;
        Probability = probability;
        State = CubeState.Solved.Apply(Primary.Inverse()).NormalizeUpFace().RecolorToHome();
    }

    public AlgorithmSetKind Set { get; }

    public string Id { get; }

    public int Number { get; }

    public string Name { get; }

    public string Group { get; }

    public IReadOnlyList<Algorithm> Algorithms { get; }

    public Algorithm Primary => Algorithms[0];

    public string? Recognition { get; }

    public string? Probability { get; }

    /// <summary>
    /// The case as it looks with white on top in the standard colour scheme, derived from the primary
    /// algorithm. A y rotation inside the algorithm is kept (the pieces stay where a cuber would see them).
    /// </summary>
    public CubeState State { get; }

    public string Title => Set == AlgorithmSetKind.PLL ? $"{Name} perm" : $"{Set} {Number}: {Name}";

    public override string ToString() => Id;
}

public sealed class AlgorithmSet
{
    internal AlgorithmSet(AlgorithmSetKind kind, CaseView view, CaseColorMode colorMode, CaseSetupOptions setupDefaults, IReadOnlyList<string> groups, IReadOnlyList<AlgCase> cases)
    {
        Kind = kind;
        View = view;
        ColorMode = colorMode;
        SetupDefaults = setupDefaults;
        Groups = groups;
        Cases = cases;
    }

    public AlgorithmSetKind Kind { get; }

    public string Name => Kind.ToString();

    public CaseView View { get; }

    public CaseColorMode ColorMode { get; }

    public CaseSetupOptions SetupDefaults { get; }

    public IReadOnlyList<string> Groups { get; }

    public IReadOnlyList<AlgCase> Cases { get; }

    public AlgCase? Find(string id) => Cases.FirstOrDefault(c => c.Id == id);
}

// On-disk shape of the embedded JSON files.
internal sealed class AlgorithmSetFile
{
    public string Set { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string View { get; set; } = "TopWithSides";
    public string ColorMode { get; set; } = "Full";
    public SetupFile? Setup { get; set; }
    public List<string> Groups { get; set; } = [];
    public List<CaseFile> Cases { get; set; } = [];
}

internal sealed class SetupFile
{
    public bool PreAuf { get; set; } = true;
    public bool PostAuf { get; set; } = true;
    public bool YRotation { get; set; }
}

internal sealed class CaseFile
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public List<string> Algorithms { get; set; } = [];
    public string? Recognition { get; set; }
    public string? Probability { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AlgorithmSetFile))]
internal sealed partial class AlgorithmSetJsonContext : JsonSerializerContext
{
}
