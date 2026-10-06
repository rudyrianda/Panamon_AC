namespace MonitoringSystem.Pages.ACOEE;

public static class AcOeeMetricsCalculator
{
    public const double TemporaryStopTimeMinutes = 1d;

    public static double CalculateOperatingPercent(double loadTimeMinutes)
    {
        if (loadTimeMinutes <= 0d)
        {
            return 0d;
        }

        return Math.Round(
            Math.Clamp(
                ((loadTimeMinutes - TemporaryStopTimeMinutes) / loadTimeMinutes) * 100d,
                0d,
                100d),
            1);
    }

    public static double CalculateOeePercent(double operating, double ability, double quality)
    {
        return Math.Round((operating + ability + quality) / 3d, 1);
    }

    public static double GenerateAbilityPercent()
    {
        return Random.Shared.Next(950, 1001) / 10d;
    }
}
