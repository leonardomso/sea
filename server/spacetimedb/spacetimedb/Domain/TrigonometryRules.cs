namespace Sea.Server;

public static class TrigonometryRules
{
    private const int SamplesPerDegree = 4;
    private const int SampleCount = 360 * SamplesPerDegree;
    private static readonly float[] Sine = BuildSineTable();

    public static float SinDegrees(float degrees) =>
        Sine[SampleIndex(degrees)];

    public static float CosDegrees(float degrees) =>
        Sine[SampleIndex(degrees + 90f)];

    private static int SampleIndex(float degrees)
    {
        var normalized = degrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return (int)(normalized * SamplesPerDegree + 0.5f) % SampleCount;
    }

    private static float[] BuildSineTable()
    {
        var values = new float[SampleCount];
        for (var index = 0; index < values.Length; index++)
        {
            var radians = index / (float)SamplesPerDegree * (MathF.PI / 180f);
            values[index] = MathF.Sin(radians);
        }

        return values;
    }
}
