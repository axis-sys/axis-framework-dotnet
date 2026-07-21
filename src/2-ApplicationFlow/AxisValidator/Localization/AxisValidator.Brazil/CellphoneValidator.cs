using System.Text.RegularExpressions;

namespace AxisValidator.Brazil;

public static partial class CellphoneValidator
{
    public static bool TryFormat(string? cellphone, out string? formatted)
    {
        formatted = Format(cellphone);
        return formatted != null;
    }

    public static string? OnlyNumbers(string? cellphone)
    {
        if (string.IsNullOrWhiteSpace(cellphone) || AnyLetters().IsMatch(cellphone))
            return null;

        while (cellphone.StartsWith('0'))
            cellphone = cellphone[1..];

        var numbers = OnlyDigits().Replace(cellphone, "");
        if (numbers.StartsWith("55") && numbers.Length is 12 or 13)
            numbers = numbers[2..];

        switch (numbers.Length)
        {
            case 11:
                {
                    return numbers.Substring(2, 1) != "9" ? null : numbers;
                }
            case >= 12 when numbers.StartsWith('0'):
                {
                    numbers = numbers[1..];
                    if (numbers.Length >= 12 && numbers.StartsWith('0'))
                        numbers = numbers[1..];
                    return numbers;
                }
            case 13:
                return numbers[2..];
        }

        return numbers.Length == 10 ? string.Concat(numbers.AsSpan(0, 2), "9", numbers.AsSpan(2)) : null;
    }

    public static string? Format(string? cellphone)
    {
        if (string.IsNullOrWhiteSpace(cellphone)) return null;

        var onlyNumbers = OnlyNumbers(cellphone);
        if (onlyNumbers == null) return null;

        var match = BrazilianCellphone().Match(onlyNumbers);
        if (!match.Success) return null;

        var ddd = match.Groups[1].Value;
        var part1 = match.Groups[2].Value[..5];
        var part2 = match.Groups[2].Value.Substring(5, 4);
        return $"({ddd}) {part1}-{part2}";
    }

    [GeneratedRegex(@"[a-zA-Z]")]
    private static partial Regex AnyLetters();

    [GeneratedRegex(@"\D")]
    private static partial Regex OnlyDigits();

    [GeneratedRegex(@"^([1-9]{2})(9[1-9][0-9]{7})$")]
    private static partial Regex BrazilianCellphone();
}
