namespace Brain.Application.Common.Models;

/// <summary>
/// Ideal runtime state machine for the Physical Gold EA (client spec).
/// STATE LAYER: cycle controller decides when a cycle starts, which engine runs next,
/// where abort/hold happens. ENGINE LAYER remains deterministic; TABLE alone emits executable orders.
/// </summary>
public static class CycleStateMachine
{
    // ─── Top-level states ─────────────────────────────────────────────────────
    public const string State_00_Idle = "STATE_00_IDLE";
    public const string State_01_SnapshotReceived = "STATE_01_SNAPSHOT_RECEIVED";
    public const string State_02_CycleStarted = "STATE_02_CYCLE_STARTED";
    public const string State_03_Precheck = "STATE_03_PRECHECK";
    public const string State_04_SessionClassification = "STATE_04_SESSION_CLASSIFICATION";
    public const string State_05_RegimeGate = "STATE_05_REGIME_GATE";
    public const string State_06_ContextBuild = "STATE_06_CONTEXT_BUILD";
    public const string State_06B_PathProjection = "STATE_06B_PATH_PROJECTION";
    public const string State_07_CandidateScan = "STATE_07_CANDIDATE_SCAN";
    public const string State_08_Analyze = "STATE_08_ANALYZE";
    public const string State_09_TableBuild = "STATE_09_TABLE_BUILD";
    public const string State_10_Validate = "STATE_10_VALIDATE";
    public const string State_11_FinalDecision = "STATE_11_FINAL_DECISION";
    public const string State_12_ExecutionReady = "STATE_12_EXECUTION_READY";
    public const string State_13_ExecuteMt5 = "STATE_13_EXECUTE_MT5";
    public const string State_14_PostExecutionMonitor = "STATE_14_POST_EXECUTION_MONITOR";
    public const string State_15_SlipsLedgerUpdate = "STATE_15_SLIPS_LEDGER_UPDATE";
    public const string State_16_LearningAudit = "STATE_16_LEARNING_AUDIT";
    public const string State_17_CycleClosed = "STATE_17_CYCLE_CLOSED";

    // ─── Abort substates (with reason codes) ───────────────────────────────────
    public const string AbortPrecheck = "ABORT_PRECHECK";
    public const string AbortRegime = "ABORT_REGIME";
    public const string AbortNoSetup = "ABORT_NO_SETUP";
    public const string AbortAnalyze = "ABORT_ANALYZE";
    public const string AbortNoTable = "ABORT_NO_TABLE";
    public const string AbortValidate = "ABORT_VALIDATE";
    public const string AbortDecision = "ABORT_DECISION";
    public const string ExecutionFailed = "EXECUTION_FAILED";

    // ─── Hold / wait ──────────────────────────────────────────────────────────
    public const string HoldWait = "HOLD_WAIT";

    // ─── Regime gate outputs ──────────────────────────────────────────────────
    public const string RegimeAllowed = "REGIME_ALLOWED";
    public const string RegimeHighCaution = "REGIME_HIGH_CAUTION";
    public const string RegimeBlocked = "REGIME_BLOCKED";

    // ─── Precheck fail reasons ─────────────────────────────────────────────────
    public const string ReasonStaleSnapshot = "stale_snapshot";
    public const string ReasonSymbolMismatch = "symbol_mismatch";
    public const string ReasonMissingTimeframe = "missing_critical_timeframe";
    public const string ReasonBrokenLedger = "broken_ledger";
    public const string ReasonImpossibleSpread = "impossible_spread";
    public const string ReasonSessionDisabled = "session_disabled";

    // ─── Regime abort reasons (precise labels per client) ──────────────────────
    public const string ReasonFridayNyLateBlock = "FRIDAY_NY_LATE_BLOCK";
    public const string ReasonFridayOverlapCaution = "FRIDAY_OVERLAP_CAUTION";
    public const string ReasonFridayNewsHazard = "FRIDAY_NEWS_HAZARD";
    public const string ReasonFridayExpansionCaution = "FRIDAY_EXPANSION_CAUTION";
    public const string ReasonRedNewsHazard = "RED_NEWS_HAZARD";
    public const string ReasonWaterfallVeto = "WATERFALL_VETO";
    public const string ReasonCrisisMode = "CRISIS_MODE";
    public const string ReasonHardLegalityFail = "HARD_LEGALITY_FAIL";

    // ─── Hold wait reasons ────────────────────────────────────────────────────
    public const string WaitReclaim = "WAIT_RECLAIM";
    public const string WaitRetest = "WAIT_RETEST";
    public const string WaitCompression = "WAIT_COMPRESSION";
    public const string WaitSessionShift = "WAIT_SESSION_SHIFT";
    public const string WaitNewsClearance = "WAIT_NEWS_CLEARANCE";
}
