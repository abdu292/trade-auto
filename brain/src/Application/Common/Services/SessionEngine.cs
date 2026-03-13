namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 1: SESSION ENGINE
/// Purpose: Manages geographical and temporal trading sessions (Server/KSA/IST, Japan, India, London, NY)
/// and the Start/Mid/End/Transition phases within a session.
/// 
/// Spec: 00_instructions - Explicit time arrays for IST and Server time
/// MT5 Server Time = KSA − 50 minutes
/// IST (India) = KSA + 2h30m
/// Therefore: IST = Server Time + 3h20m
/// </summary>
public static class SessionEngine
{
    // Explicit time arrays per spec 00_instructions
    
    // JAPAN SESSION
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) JapanIst = 
        (new TimeSpan(5, 30, 0), new TimeSpan(6, 30, 0), new TimeSpan(8, 30, 0));
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) JapanServer = 
        (new TimeSpan(2, 10, 0), new TimeSpan(3, 10, 0), new TimeSpan(5, 10, 0));
    
    // INDIA SESSION
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) IndiaIst = 
        (new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0), new TimeSpan(12, 30, 0));
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) IndiaServer = 
        (new TimeSpan(6, 10, 0), new TimeSpan(7, 10, 0), new TimeSpan(9, 10, 0));
    
    // LONDON SESSION
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) LondonIst = 
        (new TimeSpan(13, 30, 0), new TimeSpan(14, 30, 0), new TimeSpan(17, 0, 0));
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) LondonServer = 
        (new TimeSpan(10, 10, 0), new TimeSpan(11, 10, 0), new TimeSpan(13, 40, 0));
    
    // NEW YORK SESSION
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) NyIst = 
        (new TimeSpan(18, 30, 0), new TimeSpan(19, 30, 0), new TimeSpan(22, 0, 0));
    private static readonly (TimeSpan Start, TimeSpan Mid, TimeSpan End) NyServer = 
        (new TimeSpan(15, 10, 0), new TimeSpan(16, 10, 0), new TimeSpan(18, 40, 0));
    
    // TRANSITION WINDOWS
    private static readonly (TimeSpan Start, TimeSpan End) JapanToIndiaIst = 
        (new TimeSpan(9, 15, 0), new TimeSpan(9, 45, 0));
    private static readonly (TimeSpan Start, TimeSpan End) JapanToIndiaServer = 
        (new TimeSpan(5, 55, 0), new TimeSpan(6, 25, 0));
    
    private static readonly (TimeSpan Start, TimeSpan End) IndiaToLondonIst = 
        (new TimeSpan(13, 15, 0), new TimeSpan(13, 45, 0));
    private static readonly (TimeSpan Start, TimeSpan End) IndiaToLondonServer = 
        (new TimeSpan(9, 55, 0), new TimeSpan(10, 25, 0));
    
    private static readonly (TimeSpan Start, TimeSpan End) LondonToNyIst = 
        (new TimeSpan(18, 15, 0), new TimeSpan(18, 45, 0));
    private static readonly (TimeSpan Start, TimeSpan End) LondonToNyServer = 
        (new TimeSpan(14, 55, 0), new TimeSpan(15, 25, 0));

    /// <summary>
    /// Resolves session and phase from Server Time (canonical base clock per spec)
    /// </summary>
    public static SessionEngineResult Resolve(DateTimeOffset serverTime)
    {
        var serverTimeOfDay = serverTime.TimeOfDay;
        var istTime = serverTime.AddHours(3).AddMinutes(20); // IST = Server Time + 3h20m
        var istTimeOfDay = istTime.TimeOfDay;
        var ksaTime = serverTime.AddMinutes(50); // KSA = Server Time + 50 minutes
        var ksaTimeOfDay = ksaTime.TimeOfDay;

        // Check transitions first (special caution windows)
        if (IsInTransition(serverTimeOfDay, istTimeOfDay, out var transition))
        {
            return new SessionEngineResult(
                Session: transition.Session,
                Phase: "TRANSITION",
                SessionStart: transition.StartWindow,
                SessionMid: transition.MidWindow,
                SessionEnd: transition.EndWindow,
                IsTransition: true,
                TransitionWindow: transition.TransitionWindow,
                ServerTime: serverTime,
                IstTime: istTime,
                KsaTime: ksaTime);
        }

        // Check sessions in order: NY -> London -> India -> Japan
        if (IsInSession(serverTimeOfDay, istTimeOfDay, NyServer, NyIst, out var nyPhase))
        {
            return new SessionEngineResult(
                Session: "NEW_YORK",
                Phase: nyPhase,
                SessionStart: NyServer.Start,
                SessionMid: NyServer.Mid,
                SessionEnd: NyServer.End,
                IsTransition: false,
                TransitionWindow: null,
                ServerTime: serverTime,
                IstTime: istTime,
                KsaTime: ksaTime);
        }

        if (IsInSession(serverTimeOfDay, istTimeOfDay, LondonServer, LondonIst, out var londonPhase))
        {
            return new SessionEngineResult(
                Session: "LONDON",
                Phase: londonPhase,
                SessionStart: LondonServer.Start,
                SessionMid: LondonServer.Mid,
                SessionEnd: LondonServer.End,
                IsTransition: false,
                TransitionWindow: null,
                ServerTime: serverTime,
                IstTime: istTime,
                KsaTime: ksaTime);
        }

        if (IsInSession(serverTimeOfDay, istTimeOfDay, IndiaServer, IndiaIst, out var indiaPhase))
        {
            return new SessionEngineResult(
                Session: "INDIA",
                Phase: indiaPhase,
                SessionStart: IndiaServer.Start,
                SessionMid: IndiaServer.Mid,
                SessionEnd: IndiaServer.End,
                IsTransition: false,
                TransitionWindow: null,
                ServerTime: serverTime,
                IstTime: istTime,
                KsaTime: ksaTime);
        }

        if (IsInSession(serverTimeOfDay, istTimeOfDay, JapanServer, JapanIst, out var japanPhase))
        {
            return new SessionEngineResult(
                Session: "JAPAN",
                Phase: japanPhase,
                SessionStart: JapanServer.Start,
                SessionMid: JapanServer.Mid,
                SessionEnd: JapanServer.End,
                IsTransition: false,
                TransitionWindow: null,
                ServerTime: serverTime,
                IstTime: istTime,
                KsaTime: ksaTime);
        }

        // OFFHOURS
        return new SessionEngineResult(
            Session: "OFFHOURS",
            Phase: "UNKNOWN",
            SessionStart: TimeSpan.Zero,
            SessionMid: TimeSpan.Zero,
            SessionEnd: TimeSpan.Zero,
            IsTransition: false,
            TransitionWindow: null,
            ServerTime: serverTime,
            IstTime: istTime,
            KsaTime: ksaTime);
    }

    private static bool IsInSession(
        TimeSpan serverTime,
        TimeSpan istTime,
        (TimeSpan Start, TimeSpan Mid, TimeSpan End) serverWindow,
        (TimeSpan Start, TimeSpan Mid, TimeSpan End) istWindow,
        out string phase)
    {
        // Use Server Time as canonical base (per spec)
        if (serverTime >= serverWindow.Start && serverTime < serverWindow.End)
        {
            if (serverTime < serverWindow.Mid)
            {
                phase = "START";
            }
            else if (serverTime >= serverWindow.Mid && serverTime < serverWindow.End)
            {
                phase = "MID";
            }
            else
            {
                phase = "END";
            }
            return true;
        }

        phase = "UNKNOWN";
        return false;
    }

    private static bool IsInTransition(
        TimeSpan serverTime,
        TimeSpan istTime,
        out TransitionInfo transition)
    {
        // Japan → India
        if (serverTime >= JapanToIndiaServer.Start && serverTime < JapanToIndiaServer.End)
        {
            transition = new TransitionInfo(
                Session: "INDIA",
                StartWindow: IndiaServer.Start,
                MidWindow: IndiaServer.Mid,
                EndWindow: IndiaServer.End,
                TransitionWindow: (JapanToIndiaServer.Start, JapanToIndiaServer.End));
            return true;
        }

        // India → London
        if (serverTime >= IndiaToLondonServer.Start && serverTime < IndiaToLondonServer.End)
        {
            transition = new TransitionInfo(
                Session: "LONDON",
                StartWindow: LondonServer.Start,
                MidWindow: LondonServer.Mid,
                EndWindow: LondonServer.End,
                TransitionWindow: (IndiaToLondonServer.Start, IndiaToLondonServer.End));
            return true;
        }

        // London → New York
        if (serverTime >= LondonToNyServer.Start && serverTime < LondonToNyServer.End)
        {
            transition = new TransitionInfo(
                Session: "NEW_YORK",
                StartWindow: NyServer.Start,
                MidWindow: NyServer.Mid,
                EndWindow: NyServer.End,
                TransitionWindow: (LondonToNyServer.Start, LondonToNyServer.End));
            return true;
        }

        transition = default;
        return false;
    }

    private sealed record TransitionInfo(
        string Session,
        TimeSpan StartWindow,
        TimeSpan MidWindow,
        TimeSpan EndWindow,
        (TimeSpan Start, TimeSpan End) TransitionWindow);
}

/// <summary>
/// Session Engine output contract
/// </summary>
public sealed record SessionEngineResult(
    string Session,
    string Phase,
    TimeSpan SessionStart,
    TimeSpan SessionMid,
    TimeSpan SessionEnd,
    bool IsTransition,
    (TimeSpan Start, TimeSpan End)? TransitionWindow,
    DateTimeOffset ServerTime,
    DateTimeOffset IstTime,
    DateTimeOffset KsaTime);
