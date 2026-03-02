namespace Brain.Application.Common.Services;

public static class TradingSessionClock
{
    private static readonly TimeSpan TokyoStart = new(3, 0, 0);
    private static readonly TimeSpan TokyoEnd = new(9, 0, 0);
    private static readonly TimeSpan IndiaStart = new(6, 30, 0);
    private static readonly TimeSpan IndiaEnd = new(14, 30, 0);
    private static readonly TimeSpan LondonStart = new(10, 0, 0);
    private static readonly TimeSpan LondonEnd = new(18, 0, 0);
    private static readonly TimeSpan NewYorkStart = new(15, 30, 0);
    private static readonly TimeSpan NewYorkEnd = new(23, 30, 0);

    public static (string Session, string Phase) Resolve(DateTimeOffset ksaTime)
    {
        var t = ksaTime.TimeOfDay;
        if (IsWithin(t, NewYorkStart, NewYorkEnd))
        {
            return ("NY", ResolvePhase(t, NewYorkStart, NewYorkEnd));
        }

        if (IsWithin(t, LondonStart, LondonEnd))
        {
            return ("LONDON", ResolvePhase(t, LondonStart, LondonEnd));
        }

        if (IsWithin(t, IndiaStart, IndiaEnd))
        {
            return ("INDIA", ResolvePhase(t, IndiaStart, IndiaEnd));
        }

        if (IsWithin(t, TokyoStart, TokyoEnd))
        {
            return ("JAPAN", ResolvePhase(t, TokyoStart, TokyoEnd));
        }

        return ("OFFHOURS", "UNKNOWN");
    }

    private static bool IsWithin(TimeSpan value, TimeSpan start, TimeSpan end)
    {
        return value >= start && value < end;
    }

    private static string ResolvePhase(TimeSpan now, TimeSpan start, TimeSpan end)
    {
        var durationMinutes = (end - start).TotalMinutes;
        var elapsedMinutes = (now - start).TotalMinutes;

        if (elapsedMinutes <= Math.Max(30, durationMinutes * 0.20))
        {
            return "START";
        }

        if (elapsedMinutes >= durationMinutes * 0.80)
        {
            return "END";
        }

        return "MID";
    }
}
