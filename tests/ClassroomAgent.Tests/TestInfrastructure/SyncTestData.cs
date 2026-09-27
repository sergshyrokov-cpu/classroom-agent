namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The fixed values of US-013: the configuration key of the run interval, its default, the log event names and
/// the table the Story adds. Kept in one place so a rename shows up as one edit (the US-011 and US-012 pattern).
/// </summary>
public static class SyncTestData
{
    /// <summary>US-013 spec FR-013: the optional installation setting, in whole minutes.</summary>
    public const string IntervalSetting = "Sync:IntervalMinutes";

    /// <summary>US-013 spec FR-013 and I-6: one hour when the setting is absent.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);

    /// <summary>US-013 spec VR-001: the permitted range of the setting, in minutes.</summary>
    public const int MinimumIntervalMinutes = 1;

    /// <summary>US-013 spec VR-001: a day is the longest interval the setting accepts.</summary>
    public const int MaximumIntervalMinutes = 1440;

    /// <summary>US-013 db-design §5: the table the migration creates.</summary>
    public const string Table = "sync_state";

    /// <summary>US-013 db-design §5: the migration's name suffix.</summary>
    public const string Migration = "_AddSyncState";

    /// <summary>US-013 spec FR-005: the operation name the read-only refusal carries.</summary>
    public const string Operation = "Sync.Run";

    /// <summary>US-013 spec FR-012: the four log events of a run.</summary>
    public static class LogEvents
    {
        public const string RunStarted = "SyncRunStarted";

        public const string RunCompleted = "SyncRunCompleted";

        public const string RunFailed = "SyncRunFailed";

        public const string RunSkipped = "SyncRunSkipped";
    }

    /// <summary>US-013 db-design §3.2: the constraints the table enforces.</summary>
    public static class Constraints
    {
        public const string Singleton = "ck_sync_state_singleton";

        public const string UniqueSingleton = "uq_sync_state_singleton";

        public const string Status = "ck_sync_state_status";

        public const string Counter = "ck_sync_state_counter";

        public const string FinishedAfterStarted = "ck_sync_state_finished_after_started";

        public const string TerminalFields = "ck_sync_state_terminal_fields";

        public const string ErrorLength = "ck_sync_state_error_length";
    }

    /// <summary>The status codes stored in the <c>status</c> column (db-design §3.1).</summary>
    public static class Status
    {
        public const string Running = "running";

        public const string Completed = "completed";

        public const string Failed = "failed";
    }

    /// <summary>A synthetic failure message: a category and a short sentence, as spec FR-008 requires.</summary>
    public const string SyntheticError = "Transient:the synthetic port refused this run";
}
