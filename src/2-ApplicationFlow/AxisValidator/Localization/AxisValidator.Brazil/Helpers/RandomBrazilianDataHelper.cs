namespace AxisValidator.Brazil.Helpers;

public class RandomBrazilianDataHelper
{
    private static readonly Random _random = new();

    public static string GenerateCpf(bool format = false)
    {
        var cpf = new int[11];
        for (var i = 0; i < 9; i++) cpf[i] = _random.Next(0, 10);
        cpf[9] = CalcDigit(10);
        cpf[10] = CalcDigit(11);
        var result = string.Join("", cpf);
        return format
            ? $"{result[..3]}.{result.Substring(3, 3)}.{result.Substring(6, 3)}-{result.Substring(9, 2)}"
            : result;

        int CalcDigit(int initialPound)
        {
            var soma = 0;
            for (var i = 0; i < initialPound - 1; i++)
                soma += cpf[i] * (initialPound - i);
            var rest = soma % 11;
            return (rest < 2) ? 0 : 11 - rest;
        }
    }
}
