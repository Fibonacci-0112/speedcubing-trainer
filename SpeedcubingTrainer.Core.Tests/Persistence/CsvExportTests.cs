using Microsoft.VisualBasic.FileIO;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Statistics;

namespace SpeedcubingTrainer.Core.Tests.Persistence;

public class CsvExportTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(1,2)")]
    [InlineData("＝1+1")]
    [InlineData("＋1+1")]
    [InlineData("－1+1")]
    [InlineData("＠SUM(1,2)")]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    [InlineData("\n=1+1")]
    [InlineData(" \t\r\n=1+1")]
    [InlineData("\u0001=1+1")]
    [InlineData("\u00a0=1+1")]
    [InlineData("=HYPERLINK(\"https://example.invalid/\",\"test\")")]
    public void FormulaLikeTextIsPrefixedInBothColumns(string text)
    {
        var session = CreateSession(text, text);

        var fields = ReadSolve(CsvExport.Session(session));

        Assert.Equal("'" + text, fields[5]);
        Assert.Equal("'" + text, fields[6]);
        Assert.Equal(text, session.Solves[0].Scramble);
        Assert.Equal(text, session.Solves[0].Comment);
    }

    [Theory]
    [InlineData("R U R' U'", "nice, one")]
    [InlineData("R U", "a \"quoted\" comment")]
    [InlineData("R U", "line one\nline two")]
    [InlineData("R U", "line one\rline two")]
    [InlineData("R U", "line one\r\nline two")]
    [InlineData("R U", "text\",=1+1")]
    [InlineData("R U", "text\r=1+1")]
    [InlineData("R U", "already '=1+1")]
    [InlineData("", "")]
    [InlineData("R U", null)]
    public void OrdinaryTextAndCsvBoundariesArePreserved(string scramble, string? comment)
    {
        var fields = ReadSolve(CsvExport.Session(CreateSession(scramble, comment)));

        Assert.Equal(scramble, fields[5]);
        Assert.Equal(comment ?? string.Empty, fields[6]);
    }

    [Theory]
    [InlineData(ImportMode.Merge)]
    [InlineData(ImportMode.Replace)]
    public async Task ImportedFormulasAreNeutralizedOnExport(ImportMode mode)
    {
        const string json = """
            {"schemaVersion":1,"sessions":[{"id":"s","name":"Imported","solves":[
              {"id":"1","at":"2026-09-26T10:00:00Z","timeMs":12345,"penalty":"None",
               "scramble":"=1+1","comment":"\r=SUM(1,2)"}
            ]}]}
            """;
        var repository = new JsonSessionRepository(new MemoryStorage());
        Assert.Equal(1, await repository.ImportJsonAsync(json, mode));
        var session = (await repository.GetAsync("s"))!;

        var fields = ReadSolve(CsvExport.Session(session));

        Assert.Equal("'=1+1", fields[5]);
        Assert.Equal("'\r=SUM(1,2)", fields[6]);
        Assert.Equal("=1+1", session.Solves[0].Scramble);
        Assert.Equal("\r=SUM(1,2)", session.Solves[0].Comment);
    }

    private static Session CreateSession(string scramble, string? comment) => new()
    {
        Id = "s",
        Name = "S",
        Solves = [new SolveRecord("1", new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero),
            12345, Penalty.None, scramble, comment)],
    };

    private static string[] ReadSolve(string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv)) { TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        var header = parser.ReadFields();
        Assert.NotNull(header);
        Assert.Equal(["No", "Date", "Time", "Penalty", "Effective", "Scramble", "Comment"], header);
        var fields = parser.ReadFields()!;
        Assert.Equal(7, fields.Length);
        Assert.Equal(["1", "2026-09-26 10:00:00", "12.345", "", "12.345"], fields[..5]);
        Assert.True(parser.EndOfData);
        return fields;
    }
}
