using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Statistics;

namespace SpeedcubingTrainer.Core.Tests.Persistence;

public class JsonSessionRepositoryTests
{
    private static SolveRecord Solve(int ms, Penalty penalty = Penalty.None, string scramble = "R U R' U'") =>
        new(Ids.New(), DateTimeOffset.UtcNow, ms, penalty, scramble);

    [Fact]
    public async Task ActiveSessionIsCreatedOnFirstUse()
    {
        var storage = new MemoryStorage();
        var repo = new JsonSessionRepository(storage);
        var active = await repo.GetActiveAsync();
        Assert.Equal(JsonSessionRepository.DefaultSessionName, active.Name);
        Assert.Contains(JsonSessionRepository.IndexName, storage.Files.Keys);
        Assert.Contains($"sessions/{active.Id}.json", storage.Files.Keys);
        Assert.Same(active, await repo.GetActiveAsync());
    }

    [Fact]
    public async Task SolvesRoundTripThroughStorage()
    {
        var storage = new MemoryStorage();
        var repo = new JsonSessionRepository(storage);
        var session = await repo.GetActiveAsync();
        var solve = Solve(12345, Penalty.Plus2);
        await repo.AddSolveAsync(session.Id, solve);
        await repo.AddSolveAsync(session.Id, Solve(9999));

        var reloaded = new JsonSessionRepository(storage);
        var again = await reloaded.GetActiveAsync();
        Assert.Equal(session.Id, again.Id);
        Assert.Equal(2, again.Solves.Count);
        Assert.Equal(solve, again.Solves[0]);
        Assert.Equal(new SolveTime(12345, Penalty.Plus2), again.Solves[0].Time);
        var list = await reloaded.ListAsync();
        Assert.Equal(2, list.Single().SolveCount);
    }

    [Fact]
    public async Task UpdateAndDeleteSolves()
    {
        var repo = new JsonSessionRepository(new MemoryStorage());
        var session = await repo.GetActiveAsync();
        var solve = Solve(10000);
        await repo.AddSolveAsync(session.Id, solve);
        var changes = 0;
        repo.Changed += () => changes++;
        await repo.UpdateSolveAsync(session.Id, solve with { Penalty = Penalty.Dnf, Comment = "slipped" });
        Assert.Equal(Penalty.Dnf, session.Solves[0].Penalty);
        Assert.Equal("slipped", session.Solves[0].Comment);
        await repo.DeleteSolveAsync(session.Id, solve.Id);
        Assert.Empty(session.Solves);
        Assert.Equal(2, changes);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repo.UpdateSolveAsync(session.Id, solve));
    }

    [Fact]
    public async Task SessionsCanBeCreatedRenamedSwitchedAndDeleted()
    {
        var repo = new JsonSessionRepository(new MemoryStorage());
        var first = await repo.GetActiveAsync();
        var second = await repo.CreateAsync("OH", ScrambleKind.RandomMoves);
        Assert.Equal(2, (await repo.ListAsync()).Count);
        Assert.Equal(first.Id, (await repo.GetActiveAsync()).Id);
        await repo.SetActiveAsync(second.Id);
        Assert.Equal(second.Id, (await repo.GetActiveAsync()).Id);
        await repo.RenameAsync(second.Id, "One-handed");
        Assert.Equal("One-handed", (await repo.ListAsync()).Single(s => s.Id == second.Id).Name);
        await repo.DeleteSessionAsync(second.Id);
        Assert.Single(await repo.ListAsync());
        Assert.Equal(first.Id, (await repo.GetActiveAsync()).Id);
        Assert.Null(await repo.GetAsync(second.Id));
    }

    [Fact]
    public async Task IndexIsRebuiltFromSessionFiles()
    {
        var storage = new MemoryStorage();
        var repo = new JsonSessionRepository(storage);
        var session = await repo.GetActiveAsync();
        await repo.AddSolveAsync(session.Id, Solve(5000));
        storage.Files.Remove(JsonSessionRepository.IndexName);

        var recovered = new JsonSessionRepository(storage);
        var list = await recovered.ListAsync();
        Assert.Equal(session.Id, list.Single().Id);
        Assert.Equal(1, list.Single().SolveCount);
    }

    [Fact]
    public async Task ExportAndImportRoundTrip()
    {
        var source = new JsonSessionRepository(new MemoryStorage());
        var a = await source.CreateAsync("A");
        await source.AddSolveAsync(a.Id, Solve(11111));
        var b = await source.CreateAsync("B");
        await source.AddSolveAsync(b.Id, Solve(22222, Penalty.Dnf));
        var json = await source.ExportJsonAsync();

        var target = new JsonSessionRepository(new MemoryStorage());
        await target.CreateAsync("Existing");
        var imported = await target.ImportJsonAsync(json, ImportMode.Merge);
        Assert.Equal(2, imported);
        Assert.Equal(3, (await target.ListAsync()).Count);
        Assert.Equal(11111, (await target.GetAsync(a.Id))!.Solves[0].TimeMs);

        var replaced = new JsonSessionRepository(new MemoryStorage());
        await replaced.CreateAsync("Gone");
        await replaced.ImportJsonAsync(json, ImportMode.Replace);
        Assert.Equal(new[] { "A", "B" }, (await replaced.ListAsync()).Select(s => s.Name).ToArray());

        await Assert.ThrowsAnyAsync<Exception>(() => target.ImportJsonAsync("{\"nope\": true", ImportMode.Merge));
    }

    [Fact]
    public async Task NewerSchemaIsRejected()
    {
        var storage = new MemoryStorage();
        storage.Files[JsonSessionRepository.IndexName] = "{\"schemaVersion\": 99, \"sessions\": []}";
        var repo = new JsonSessionRepository(storage);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ListAsync());
    }

    [Fact]
    public void CsvExportHasOneRowPerSolve()
    {
        var session = new Session { Id = "s", Name = "S" };
        session.Solves.Add(new SolveRecord("1", new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero), 12345, Penalty.None, "R U", "nice, one"));
        session.Solves.Add(new SolveRecord("2", new DateTimeOffset(2026, 9, 26, 10, 1, 0, TimeSpan.Zero), 10000, Penalty.Plus2, "F B"));
        var csv = CsvExport.Session(session).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, csv.Length);
        Assert.Contains("1,2026-09-26 10:00:00,12.345,,12.345,R U,\"nice, one\"", csv[1]);
        Assert.Contains("2,2026-09-26 10:01:00,10.000,+2,12.000,F B,", csv[2]);
    }

    [Fact]
    public async Task ProgressRepositoryPersistsRecords()
    {
        var storage = new MemoryStorage();
        var repo = new JsonAlgorithmProgressRepository(storage);
        var record = await repo.GetAsync("OLL-21");
        Assert.Equal(LearningStatus.NotStarted, record.Status);
        record.Status = LearningStatus.Learning;
        record.Favorite = true;
        await repo.SaveAsync("OLL-21", record);

        var reloaded = new JsonAlgorithmProgressRepository(storage);
        var again = await reloaded.GetAsync("OLL-21");
        Assert.Equal(LearningStatus.Learning, again.Status);
        Assert.True(again.Favorite);
        await reloaded.ResetAsync();
        Assert.Equal(LearningStatus.NotStarted, (await reloaded.GetAsync("OLL-21")).Status);
    }

    [Fact]
    public async Task FileStorageWritesAtomicallyAndListsFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "sct-tests", Ids.New());
        try
        {
            var storage = new FileAppStorage(root);
            await storage.WriteTextAsync("sessions/a.json", "{}");
            await storage.WriteBytesAsync("solver/x.bin", [1, 2, 3]);
            Assert.Equal("{}", await storage.ReadTextAsync("sessions/a.json"));
            Assert.Equal([1, 2, 3], await storage.ReadBytesAsync("solver/x.bin"));
            Assert.True(await storage.ExistsAsync("sessions/a.json"));
            Assert.Equal(["sessions/a.json"], await storage.ListAsync("sessions/"));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "sessions"), "*.tmp"));

            // A leftover temp file from an interrupted write is used when the target is missing.
            File.Delete(Path.Combine(root, "sessions", "a.json"));
            await File.WriteAllTextAsync(Path.Combine(root, "sessions", "a.json.tmp"), "{\"recovered\":true}");
            Assert.Equal("{\"recovered\":true}", await storage.ReadTextAsync("sessions/a.json"));

            await storage.DeleteAsync("sessions/a.json");
            Assert.False(await storage.ExistsAsync("sessions/a.json"));
            Assert.Null(await storage.ReadTextAsync("missing.json"));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadTextAsync("../escape.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
