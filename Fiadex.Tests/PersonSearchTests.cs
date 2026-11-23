namespace Fiadex.Tests;

public class PersonSearchFilterTests
{
    private static readonly Person[] People =
    [
        new() { Id = 1, Name = "María González", Phone = "3001234567" },
        new() { Id = 2, Name = "Juan Pérez", Phone = "3109876543" },
        new() { Id = 3, Name = "Ana", Phone = "" }
    ];

    [Fact]
    public void Filter_MatchesNameIgnoringCaseAndAccents()
    {
        var results = PersonSearchFilter.Filter(People, "maria gonzalez").ToList();

        Assert.Single(results);
        Assert.Equal("María González", results[0].Name);
    }

    [Fact]
    public void Filter_MatchesPhone()
    {
        var results = PersonSearchFilter.Filter(People, "3109").ToList();

        Assert.Single(results);
        Assert.Equal("Juan Pérez", results[0].Name);
    }

    [Fact]
    public void Filter_EmptyTermReturnsAllPeople()
    {
        var results = PersonSearchFilter.Filter(People, "  ").ToList();

        Assert.Equal(3, results.Count);
    }
}
