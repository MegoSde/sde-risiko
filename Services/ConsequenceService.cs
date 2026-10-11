namespace AlpacaMedic.Services;
public static class ConsequenceService
{
    public static decimal Calculate(int c, int i, int a) => Math.Round((2m * (c + i + a) / 3m - 1m) * 2m, MidpointRounding.AwayFromZero) / 2m;
}
