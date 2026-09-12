using System.Security.Cryptography;

namespace CreditCardApplication.Application.Applications;

/// <summary>
/// Produces a presentation-safe payment card PAN from a configured 6 or 8 digit BIN/IIN.
/// The full PAN is intentionally kept in memory only; persistence uses <see cref="Mask"/>.
/// </summary>
public static class PaymentCardNumberGenerator
{
    public static string Generate(string bin, int totalLength = 16)
    {
        if (string.IsNullOrWhiteSpace(bin) || bin.Any(character => !char.IsDigit(character)))
            throw new ArgumentException("BIN/IIN yalnızca rakamlardan oluşmalıdır.", nameof(bin));
        if (bin.Length is not (6 or 8))
            throw new ArgumentException("BIN/IIN 6 veya 8 haneli olmalıdır.", nameof(bin));
        if (totalLength <= bin.Length + 1 || totalLength > 19)
            throw new ArgumentOutOfRangeException(nameof(totalLength), "Kart numarası BIN/IIN ve kontrol hanesini içermelidir.");

        var digits = new List<int>(totalLength);
        digits.AddRange(bin.Select(character => character - '0'));
        while (digits.Count < totalLength - 1)
            digits.Add(RandomNumberGenerator.GetInt32(10));
        digits.Add(CalculateCheckDigit(digits));
        return string.Concat(digits);
    }

    public static bool IsValid(string pan)
    {
        if (string.IsNullOrWhiteSpace(pan) || pan.Length is < 12 or > 19 || pan.Any(character => !char.IsDigit(character)))
            return false;
        return LuhnSum(pan.Select(character => character - '0')) % 10 == 0;
    }

    public static string Mask(string pan, int binLength)
    {
        if (!IsValid(pan)) throw new ArgumentException("Geçerli bir Luhn kart numarası bekleniyor.", nameof(pan));
        if (binLength is not (6 or 8) || binLength >= pan.Length - 4)
            throw new ArgumentOutOfRangeException(nameof(binLength));
        return $"{pan[..binLength]} {new string('*', pan.Length - binLength - 4)} {pan[^4..]}";
    }

    private static int CalculateCheckDigit(IReadOnlyCollection<int> digitsWithoutCheckDigit)
    {
        var withPlaceholder = digitsWithoutCheckDigit.Append(0);
        return (10 - (LuhnSum(withPlaceholder) % 10)) % 10;
    }

    private static int LuhnSum(IEnumerable<int> digits)
    {
        var values = digits.ToArray();
        var sum = 0;
        var doubleDigit = values.Length % 2 == 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if ((index % 2 == 0) == doubleDigit)
            {
                value *= 2;
                if (value > 9) value -= 9;
            }
            sum += value;
        }
        return sum;
    }
}
