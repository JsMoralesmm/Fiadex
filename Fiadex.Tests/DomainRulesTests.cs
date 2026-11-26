using Xunit;

namespace Fiadex.Tests;

public class DomainRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000_001d)]
    public void ValidateAmount_RejectsInvalidValues(decimal amount)
    {
        Assert.Throws<InvalidOperationException>(() => DomainRules.ValidateAmount(amount, "Monto"));
    }

    [Fact]
    public void ValidateAmount_AcceptsValidValue()
    {
        DomainRules.ValidateAmount(1250.50m, "Monto");
    }

    [Fact]
    public void ValidateLoanDate_RejectsFutureDate()
    {
        Assert.Throws<InvalidOperationException>(
            () => DomainRules.ValidateLoanDate(DateTime.Today.AddDays(1)));
    }

    [Fact]
    public void ValidatePaymentDate_RejectsBeforeLoanAndFuture()
    {
        var loanDate = DateTime.Today.AddDays(-10);

        Assert.Throws<InvalidOperationException>(
            () => DomainRules.ValidatePaymentDate(loanDate, loanDate.AddDays(-1)));
        Assert.Throws<InvalidOperationException>(
            () => DomainRules.ValidatePaymentDate(loanDate, DateTime.Today.AddDays(1)));
        DomainRules.ValidatePaymentDate(loanDate, DateTime.Today);
    }
}
