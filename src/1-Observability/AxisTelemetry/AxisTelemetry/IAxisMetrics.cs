namespace Axis;

public interface IAxisMetrics
{
    void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags);
    void IncrementCounter(string name, long delta = 1, params KeyValuePair<string, object?>[] tags);
}
