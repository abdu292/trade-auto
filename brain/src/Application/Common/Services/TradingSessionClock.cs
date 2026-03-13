namespace Brain.Application.Common.Services;

/// <summary>
/// Session Engine per spec/00_instructions and spec/01_master_constitution.md
/// Uses explicit time arrays with Server Time as base clock.
/// MT5 Server Time = KSA − 50 minutes
/// IST = KSA + 2h30m = Server Time + 3h20m
/// </summary>
public static class TradingSessionClock
{
    // Japan Session - Server Time windows
    private static readonly TimeSpan JapanStartServer = new(2, 10, 0);  // 02:10
    private static readonly TimeSpan JapanMidStartServer = new(3, 10, 0);  // 03:10
    private static readonly TimeSpan JapanMidEndServer = new(5, 10, 0);  // 05:10
    private static readonly TimeSpan JapanEndServer = new(6, 10, 0);  // 06:10

    // India Session - Server Time windows
    private static readonly TimeSpan IndiaStartServer = new(6, 10, 0);  // 06:10
    private static readonly TimeSpan IndiaMidStartServer = new(7, 10, 0);  // 07:10
    private static readonly TimeSpan IndiaMidEndServer = new(9, 10, 0);  // 09:10
    private static readonly TimeSpan IndiaEndServer = new(10, 10, 0);  // 10:10

    // London Session - Server Time windows
    private static readonly TimeSpan LondonStartServer = new(10, 10, 0);  // 10:10
    private static readonly TimeSpan LondonMidStartServer = new(11, 10, 0);  // 11:10
    private static readonly TimeSpan LondonMidEndServer = new(13, 40, 0);  // 13:40
    private static readonly TimeSpan LondonEndServer = new(15, 10, 0);  // 15:10

    // New York Session - Server Time windows
    private static readonly TimeSpan NyStartServer = new(15, 10, 0);  // 15:10
    private static readonly TimeSpan NyMidStartServer = new(16, 10, 0);  // 16:10
    private static readonly TimeSpan NyMidEndServer = new(18, 40, 0);  // 18:40
    private static readonly TimeSpan NyEndServer = new(20, 10, 0);  // 20:10

    // Transition windows - Server Time
    private static readonly TimeSpan JapanToIndiaTransitionStart = new(5, 55, 0);  // 05:55
    private static readonly TimeSpan JapanToIndiaTransitionEnd = new(6, 25, 0);  // 06:25
    private static readonly TimeSpan IndiaToLondonTransitionStart = new(9, 55, 0);  // 09:55
    private static readonly TimeSpan IndiaToLondonTransitionEnd = new(10, 25, 0);  // 10:25
    private static readonly TimeSpan LondonToNyTransitionStart = new(14, 55, 0);  // 14:55
    private static readonly TimeSpan LondonToNyTransitionEnd = new(15, 25, 0);  // 15:25

    /// <summary>
    /// Resolves session and phase from Server Time (MT5 server time).
    /// Per spec: Use Server Time as canonical base clock.
    /// </summary>
    public static (string Session, string Phase) Resolve(DateTimeOffset serverTime)
    {
        var t = serverTime.TimeOfDay;

        // Check transitions first
        if (IsWithin(t, JapanToIndiaTransitionStart, JapanToIndiaTransitionEnd))
        {
            return ("TRANSITION", "JAPAN_TO_INDIA");
        }
        if (IsWithin(t, IndiaToLondonTransitionStart, IndiaToLondonTransitionEnd))
        {
            return ("TRANSITION", "INDIA_TO_LONDON");
        }
        if (IsWithin(t, LondonToNyTransitionStart, LondonToNyTransitionEnd))
        {
            return ("TRANSITION", "LONDON_TO_NY");
        }

        // Check sessions in priority order (NY -> London -> India -> Japan)
        if (IsWithin(t, NyStartServer, NyEndServer))
        {
            return ("NEW_YORK", ResolvePhase(t, NyStartServer, NyMidStartServer, NyMidEndServer, NyEndServer));
        }

        if (IsWithin(t, LondonStartServer, LondonEndServer))
        {
            return ("LONDON", ResolvePhase(t, LondonStartServer, LondonMidStartServer, LondonMidEndServer, LondonEndServer));
        }

        if (IsWithin(t, IndiaStartServer, IndiaEndServer))
        {
            return ("INDIA", ResolvePhase(t, IndiaStartServer, IndiaMidStartServer, IndiaMidEndServer, IndiaEndServer));
        }

        if (IsWithin(t, JapanStartServer, JapanEndServer))
        {
            return ("JAPAN", ResolvePhase(t, JapanStartServer, JapanMidStartServer, JapanMidEndServer, JapanEndServer));
        }

        // Late NY: 00:00–03:00 KSA = 23:10–02:10 server (we don't deal in this window)
        var lateNyStartServer = new TimeSpan(23, 10, 0);
        var lateNyEndServer = new TimeSpan(2, 10, 0);
        if (t >= lateNyStartServer || t < lateNyEndServer)
        {
            return ("LATE_NY", "UNKNOWN");
        }

        return ("OFFHOURS", "UNKNOWN");
    }

    private static bool IsWithin(TimeSpan value, TimeSpan start, TimeSpan end)
    {
        return value >= start && value < end;
    }

    /// <summary>
    /// Granular phase per client spec: OPEN, EARLY, MID, LATE, END.
    /// Enables precise rules e.g. "Friday NY LATE/END only = hard block".
    /// </summary>
    private static string ResolvePhase(TimeSpan now, TimeSpan startWindow, TimeSpan midStart, TimeSpan midEnd, TimeSpan endWindow)
    {
        var sessionDuration = endWindow - startWindow;
        var openEnd = startWindow.Add(TimeSpan.FromMinutes(Math.Min(15, sessionDuration.TotalMinutes * 0.15)));
        var lateStart = endWindow.Subtract(TimeSpan.FromMinutes(Math.Min(10, sessionDuration.TotalMinutes * 0.1)));

        if (now >= startWindow && now < openEnd)
            return "OPEN";
        if (now >= openEnd && now < midStart)
            return "EARLY";
        if (now >= midStart && now < midEnd)
            return "MID";
        if (now >= midEnd && now < lateStart)
            return "LATE";
        if (now >= lateStart && now < endWindow)
            return "END";
        return "UNKNOWN";
    }

    /// <summary>
    /// Returns true when session is NEW_YORK and phase is LATE or END (for Friday hard block rule).
    /// </summary>
    public static bool IsNewYorkLateOrEnd(string session, string phase)
    {
        var s = (session ?? string.Empty).Trim().ToUpperInvariant();
        var p = (phase ?? string.Empty).Trim().ToUpperInvariant();
        return (s == "NEW_YORK" || s == "NY") && (p == "LATE" || p == "END");
    }

    /// <summary>
    /// Converts Server Time to IST (India Standard Time).
    /// IST = Server Time + 3h20m
    /// </summary>
    public static DateTimeOffset ServerTimeToIst(DateTimeOffset serverTime)
    {
        return serverTime.AddHours(3).AddMinutes(20);
    }

    /// <summary>
    /// Converts Server Time to KSA.
    /// KSA = Server Time + 50 minutes
    /// </summary>
    public static DateTimeOffset ServerTimeToKsa(DateTimeOffset serverTime)
    {
        return serverTime.AddMinutes(50);
    }
}
