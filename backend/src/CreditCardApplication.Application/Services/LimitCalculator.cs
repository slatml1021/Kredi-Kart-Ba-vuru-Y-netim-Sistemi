namespace CreditCardApplication.Application.Services;

public sealed class LimitCalculator
{
    private const decimal IncomeMultiplier = 3m;

    public LimitCalculationResult Calculate(
        decimal monthlyNetIncome,
        decimal otherBankTotalCardLimit,
        decimal approvedOwnBankCardLimit = 0)
    {
        if (monthlyNetIncome <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyNetIncome), "Aylık net gelir sıfırdan büyük olmalıdır.");
        }

        if (otherBankTotalCardLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(otherBankTotalCardLimit), "Diğer banka limiti negatif olamaz.");
        }
        if (approvedOwnBankCardLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(approvedOwnBankCardLimit), "Mevcut banka kart limiti negatif olamaz.");

        var theoreticalTotalLimit = monthlyNetIncome * IncomeMultiplier;
        var availableLimit = Math.Max(0, theoreticalTotalLimit - otherBankTotalCardLimit - approvedOwnBankCardLimit);
        return new LimitCalculationResult(theoreticalTotalLimit, availableLimit);
    }
}

public sealed record LimitCalculationResult(decimal TheoreticalTotalLimit, decimal AvailableLimit);
