namespace AxisValidator.Usa.Helpers;

public class RandomUsaDataHelper
{
    private static readonly Random _random = new();

    public static string GenerateSsn(bool format = false)
    {
        int area;
        do
        {
            area = _random.Next(1, 900);
        } while (area == 666);

        var group = _random.Next(1, 100);
        var serial = _random.Next(1, 10000);

        var areaText = area.ToString("D3");
        var groupText = group.ToString("D2");
        var serialText = serial.ToString("D4");

        return format
            ? $"{areaText}-{groupText}-{serialText}"
            : $"{areaText}{groupText}{serialText}";
    }
}
