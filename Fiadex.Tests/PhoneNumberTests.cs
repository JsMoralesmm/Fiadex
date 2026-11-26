using Microsoft.Data.Sqlite;

namespace Fiadex.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("3001234567")]
    [InlineData("300 123 4567")]
    [InlineData("+57 300 123 4567")]
    [InlineData("(300) 123-4567")]
    public void DigitsOnly_LeavesTheTenColombianDigits(string input)
    {
        Assert.Equal("3001234567", PhoneNumber.DigitsOnly(input));
    }

    [Theory]
    [InlineData("300123456")]
    [InlineData("30012345678")]
    [InlineData("")]
    [InlineData("abcdefghij")]
    public void IsValid_RejectsAnythingButTenDigits(string input)
    {
        Assert.False(PhoneNumber.IsValid(input));
    }

    [Fact]
    public void IsValid_AcceptsATenDigitNumber()
    {
        Assert.True(PhoneNumber.IsValid("6011234567"));
    }

    [Fact]
    public void AddPerson_RejectsAPhoneThatIsNotTenDigits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            var viewModel = new MainViewModel(database, new ToastService());

            viewModel.PersonName = "Ana";
            viewModel.PersonPhone = "300123";
            viewModel.AddPersonCommand.Execute(null);

            Assert.Empty(database.GetPeople());
            Assert.NotEmpty(viewModel.GetErrors(nameof(MainViewModel.PersonPhone)));

            viewModel.PersonPhone = "3001234567";
            viewModel.AddPersonCommand.Execute(null);

            var person = Assert.Single(database.GetPeople());
            Assert.Equal("3001234567", person.Phone);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void AddPerson_StoresOnlyTheDigitsOfTheEnteredPhone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            var viewModel = new MainViewModel(database, new ToastService());

            viewModel.PersonName = "Carlos";
            viewModel.PersonPhone = "300 123 4567";
            viewModel.AddPersonCommand.Execute(null);

            var person = Assert.Single(database.GetPeople());
            Assert.Equal("3001234567", person.Phone);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
