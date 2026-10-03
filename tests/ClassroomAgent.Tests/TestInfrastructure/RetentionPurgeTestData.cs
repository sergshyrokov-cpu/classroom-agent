namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The fixed values of US-037's tests. Every host test runs at <see cref="InstallationTestHost.DefaultStart"/> with
/// <c>Retention:Years</c> = 5, so the cutoff of spec FR-001 is a known instant and each date is placed relative to it.
/// </summary>
public static class RetentionPurgeTestData
{
    public const int Years = 5;

    /// <summary>The audit action code of a purge run (db-design §2.3).</summary>
    public const string Action = "retention_purge_run";

    public static DateTimeOffset Now => InstallationTestHost.DefaultStart;

    /// <summary><c>now − 5 years</c>: a date equal to it is kept, one tick earlier is expired (spec FR-001, AC-014).</summary>
    public static DateTimeOffset Cutoff => Now.AddYears(-Years);

    /// <summary>Clearly expired.</summary>
    public static DateTimeOffset Old => Cutoff.AddDays(-30);

    /// <summary>Clearly within N.</summary>
    public static DateTimeOffset Recent => Cutoff.AddDays(30);

    /// <summary>The log events the tests fix for IMPLEMENTATION (OD-007).</summary>
    public static class Events
    {
        public const string Started = "RetentionPurgeStarted";

        public const string Completed = "RetentionPurgeCompleted";

        public const string UnitFailed = "RetentionPurgeUnitFailed";

        public const string WaitingForSync = "RetentionPurgeWaitingForSync";
    }

    /// <summary>The constraint and index names of db-design §2.4, §2.5 and §3.</summary>
    public static class Names
    {
        public const string PurgeCounts = "ck_audit_event_purge_counts";

        public const string PurgeCountsAbsent = "ck_audit_event_purge_counts_absent";

        public const string PurgeCountsNonNegative = "ck_audit_event_purge_counts_non_negative";

        public const string PurgeActor = "ck_audit_event_purge_actor";

        public const string AuditOccurredAtIndex = "ix_audit_event_occurred_at";

        public const string OffRosterIndex = "ix_course_membership_off_roster_last_seen";
    }
}
