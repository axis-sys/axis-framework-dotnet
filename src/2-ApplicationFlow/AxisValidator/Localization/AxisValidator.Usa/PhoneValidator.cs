using System.Text.RegularExpressions;

namespace AxisValidator.Usa;

public static partial class PhoneValidator
{
    public static bool TryFormat(string? phone, out string? formatted)
    {
        formatted = Format(phone);
        return formatted != null;
    }

    public static string? OnlyNumbers(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || AnyLetters().IsMatch(phone))
            return null;

        var numbers = OnlyDigits().Replace(phone, "");

        if (numbers.Length == 11 && numbers.StartsWith('1'))
            numbers = numbers[1..];

        return numbers.Length == 10 ? numbers : null;
    }

    public static string? Format(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var onlyNumbers = OnlyNumbers(phone);
        if (onlyNumbers == null) return null;

        var match = UsaPhoneNumber().Match(onlyNumbers);
        if (!match.Success) return null;

        var areaCode = match.Groups[1].Value;
        var exchange = match.Groups[2].Value;
        var lineNumber = match.Groups[3].Value;
        return $"({areaCode}) {exchange}-{lineNumber}";
    }

    [GeneratedRegex(@"[a-zA-Z]")]
    private static partial Regex AnyLetters();

    [GeneratedRegex(@"\D")]
    private static partial Regex OnlyDigits();

    [GeneratedRegex(@"^([2-9][0-9]{2})([2-9][0-9]{2})([0-9]{4})$")]
    private static partial Regex UsaPhoneNumber();
}
