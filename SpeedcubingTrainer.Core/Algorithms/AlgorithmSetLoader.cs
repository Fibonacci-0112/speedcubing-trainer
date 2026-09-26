using System.Reflection;
using System.Text.Json;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Algorithms;

/// <summary>Loads the built-in algorithm sets from embedded JSON. Sets are parsed once and cached.</summary>
public static class AlgorithmSetLoader
{
    private static readonly Dictionary<AlgorithmSetKind, AlgorithmSet> Cache = new();
    private static readonly object Gate = new();

    public static AlgorithmSet Load(AlgorithmSetKind kind)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(kind, out var cached))
            {
                return cached;
            }
            var name = $"algsets/{kind.ToString().ToLowerInvariant()}.json";
            using var stream = typeof(AlgorithmSetLoader).GetTypeInfo().Assembly.GetManifestResourceStream(name)
                               ?? throw new InvalidOperationException($"Embedded algorithm set '{name}' is missing.");
            var set = Parse(stream);
            Cache[kind] = set;
            return set;
        }
    }

    public static IReadOnlyList<AlgorithmSet> LoadAll() => Enum.GetValues<AlgorithmSetKind>().Select(Load).ToList();

    public static AlgorithmSet Parse(Stream json)
    {
        var file = JsonSerializer.Deserialize(json, AlgorithmSetJsonContext.Default.AlgorithmSetFile)
                   ?? throw new FormatException("Empty algorithm set.");
        var kind = Enum.Parse<AlgorithmSetKind>(file.Set, ignoreCase: true);
        var view = Enum.Parse<CaseView>(file.View, ignoreCase: true);
        var colorMode = Enum.Parse<CaseColorMode>(file.ColorMode, ignoreCase: true);
        var setup = file.Setup is { } s ? new CaseSetupOptions(s.PreAuf, s.PostAuf, s.YRotation) : new CaseSetupOptions();
        var cases = new List<AlgCase>(file.Cases.Count);
        foreach (var c in file.Cases)
        {
            if (c.Algorithms.Count == 0)
            {
                throw new FormatException($"Case {c.Id} has no algorithms.");
            }
            var algorithms = c.Algorithms.Select(text =>
            {
                try
                {
                    return Algorithm.Parse(text);
                }
                catch (AlgorithmParseException ex)
                {
                    throw new FormatException($"Case {c.Id}: cannot parse '{text}': {ex.Message}", ex);
                }
            }).ToList();
            cases.Add(new AlgCase(kind, c.Id, c.Number, c.Name, c.Group, algorithms, c.Recognition, c.Probability));
        }
        return new AlgorithmSet(kind, view, colorMode, setup, file.Groups, cases);
    }
}
