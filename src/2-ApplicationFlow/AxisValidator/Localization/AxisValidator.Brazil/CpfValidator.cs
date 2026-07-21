namespace AxisValidator.Brazil;

public static class CpfValidator
{
    public static bool Validate(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;

        var cpfNumbers = new string(cpf.Where(char.IsDigit).ToArray());
        if (cpfNumbers.Length != 11) return false;

        string[] notValidList =
        [
            "00000000000", "11111111111", "22222222222", "33333333333",
            "44444444444", "55555555555", "66666666666", "77777777777",
            "88888888888", "99999999999", "12345678909"
        ];
        if (notValidList.Contains(cpfNumbers)) return false;

        int[] multiplicator1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] multiplicator2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

        var cpfNumber = cpfNumbers[..9];
        var sum = 0;

        for (var i = 0; i < 9; i++)
            sum += int.Parse(cpfNumber[i].ToString()) * multiplicator1[i];

        var rest = sum % 11;
        rest = rest < 2 ? 0 : 11 - rest;

        var digit = rest.ToString();
        cpfNumber += digit;
        sum = 0;

        for (var i = 0; i < 10; i++)
            sum += int.Parse(cpfNumber[i].ToString()) * multiplicator2[i];

        rest = sum % 11;
        rest = rest < 2 ? 0 : 11 - rest;

        digit += rest.ToString();

        return cpfNumbers.EndsWith(digit);
    }

}