namespace AxisValidator.Usa;

public static class SsnValidator
{
    private static readonly string[] NotValidList =
    [
        "000000000", "111111111", "222222222", "333333333",
        "444444444", "555555555", "666666666", "777777777",
        "888888888", "999999999", "123456789",
        "078051120", "219099999"
    ];

    public static bool Validate(string? ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn)) return false;

        var digits = new string(ssn.Where(char.IsDigit).ToArray());
        if (digits.Length != 9) return false;

        if (NotValidList.Contains(digits)) return false;

        var area = int.Parse(digits[..3]);
        var group = digits.Substring(3, 2);
        var serial = digits.Substring(5, 4);

        if (area == 0 || area == 666 || area >= 900) return false;
        if (group == "00") return false;
        if (serial == "0000") return false;

        return true;
    }
}
