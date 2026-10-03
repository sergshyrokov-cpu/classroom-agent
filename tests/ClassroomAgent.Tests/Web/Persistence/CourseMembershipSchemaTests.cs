using ClassroomAgent.Tests.TestInfrastructure;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-014 db-design §5: the migration creates <c>course_membership</c> as the explicit entity between a course and
/// a person, unique on (course, participant) per PC-8 — the correction Specification v2 made — carrying the role
/// and the three BR-051 observations. Against real PostgreSQL via Testcontainers (TC-2).
/// </summary>
public sealed class CourseMembershipSchemaTests(PostgreSqlFixture database)
{
    /// <summary>db-design §5.1: nine columns, all three observation columns required.</summary>
    [Fact]
    public async Task TheMigration_CreatesTheTable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var columns = await host.QueryAsync(
            """
            SELECT column_name, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'course_membership'
            ORDER BY column_name
            """,
            r => (Name: r.GetString(0), Nullable: r.GetString(1), Length: r.IsDBNull(2) ? (int?)null : r.GetInt32(2)),
            ct);

        Assert.Equal(9, columns.Count);
        Assert.Contains(columns, c => c.Name == "id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "course_id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "participant_id" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "role" && c.Nullable == "NO" && c.Length == 16);
        Assert.Contains(columns, c => c.Name == "first_seen_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "last_seen_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "on_roster" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "created_at" && c.Nullable == "NO");
        Assert.Contains(columns, c => c.Name == "updated_at" && c.Nullable == "NO");
    }

    /// <summary>
    /// db-design §5.3, PC-8, spec v2 FR-007 and I-9: one participation per person per course. This is the guard
    /// behind the correction that blocked DB_DESIGN at attempt 1 — the role is a field, not part of identity, so a
    /// second row for the same pair is rejected even with a different role.
    /// </summary>
    [Fact]
    public async Task ASecondMembershipOfTheSamePersonInTheSameCourse_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseRows.InsertMembershipAsync(host, course, participant, ct, role: CourseTestData.Roles.Student);

        var second = async () => await CourseRows.InsertMembershipAsync(
            host,
            course,
            participant,
            ct,
            role: CourseTestData.Roles.Teacher);

        var error = await Assert.ThrowsAsync<PostgresException>(second);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.MembershipCourseParticipant, error.ConstraintName);
    }

    /// <summary>db-design §5.2, BR-050: one person may hold memberships of two courses, with different roles.</summary>
    [Fact]
    public async Task OnePerson_MayTeachOneCourseAndStudyAnother()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var taught = await CourseRows.InsertCourseAsync(host, ct, googleId: CourseTestData.CourseId(1));
        var studied = await CourseRows.InsertCourseAsync(
            host,
            ct,
            googleId: CourseTestData.CourseId(2),
            name: CourseTestData.CourseName(2));
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        await CourseRows.InsertMembershipAsync(host, taught, participant, ct, role: CourseTestData.Roles.Teacher);
        await CourseRows.InsertMembershipAsync(host, studied, participant, ct, role: CourseTestData.Roles.Student);

        var roles = await host.QueryAsync(
            "SELECT role FROM course_membership WHERE participant_id = @id ORDER BY role",
            r => r.GetString(0),
            ct,
            ("id", participant));
        Assert.Equal(new[] { CourseTestData.Roles.Student, CourseTestData.Roles.Teacher }, roles);
    }

    /// <summary>db-design §5.2: each of the two §3 roles is storable.</summary>
    [Theory]
    [InlineData(CourseTestData.Roles.Teacher)]
    [InlineData(CourseTestData.Roles.Student)]
    public async Task EveryRoleOfTheVocabulary_IsAccepted(string role)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        var rows = await CourseRows.InsertMembershipAsync(host, course, participant, ct, role: role);

        Assert.Equal(1, rows);
    }

    /// <summary>db-design §5.2: a role outside the two-value vocabulary is rejected.</summary>
    [Fact]
    public async Task ARoleOutsideTheVocabulary_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);

        var insert = async () => await CourseRows.InsertMembershipAsync(host, course, participant, ct, role: "owner");

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.MembershipRole, error.ConstraintName);
    }

    /// <summary>db-design §5.2: the two observation instants cannot be written out of order.</summary>
    [Fact]
    public async Task ALastSeenEarlierThanFirstSeen_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        var now = host.Time.GetUtcNow();

        var insert = async () => await CourseRows.InsertMembershipAsync(
            host,
            course,
            participant,
            ct,
            firstSeenAt: now,
            lastSeenAt: now - TimeSpan.FromMinutes(1));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.MembershipSeenOrder, error.ConstraintName);
    }

    /// <summary>
    /// db-design §5.2: equal instants are the legitimate shape — a person seen in this very run and still on the
    /// roster. This is why the constraint is <c>&gt;=</c> and why <c>on_roster</c> is deliberately not constrained
    /// against the dates.
    /// </summary>
    [Fact]
    public async Task EqualObservationInstants_AreAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        var now = host.Time.GetUtcNow();

        var rows = await CourseRows.InsertMembershipAsync(
            host,
            course,
            participant,
            ct,
            firstSeenAt: now,
            lastSeenAt: now);

        Assert.Equal(1, rows);
    }

    /// <summary>
    /// db-design §5.4: both foreign keys are <c>Restrict</c>, so deleting a course with memberships is refused.
    /// The purge (US-037) must therefore delete in an explicit order — a cascade would have taken grades with it
    /// once US-015 hangs submissions off these rows.
    /// </summary>
    [Fact]
    public async Task DeletingACourseThatHasMemberships_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseRows.InsertMembershipAsync(host, course, participant, ct);

        var delete = async () => await host.ExecuteAsync(
            "DELETE FROM course WHERE id = @id",
            ct,
            ("id", course));

        var error = await Assert.ThrowsAsync<PostgresException>(delete);
        Assert.Equal("23503", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.MembershipCourseForeignKey, error.ConstraintName);
    }

    /// <summary>db-design §5.4: deleting a participant who still holds a membership is refused for the same reason.</summary>
    [Fact]
    public async Task DeletingAParticipantThatHasMemberships_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct);
        var participant = await CourseRows.InsertParticipantAsync(host, ct);
        await CourseRows.InsertMembershipAsync(host, course, participant, ct);

        var delete = async () => await host.ExecuteAsync(
            "DELETE FROM classroom_participant WHERE id = @id",
            ct,
            ("id", participant));

        var error = await Assert.ThrowsAsync<PostgresException>(delete);
        Assert.Equal("23503", error.SqlState);
        Assert.Equal(CourseTestData.Constraints.MembershipParticipantForeignKey, error.ConstraintName);
    }

    /// <summary>
    /// db-design §5.3, PC-7: the roster-on-a-date composite exists — the unique index cannot serve it, because it
    /// has no date column — and the second foreign key has its own index. US-037 db-design §3 adds the partial
    /// index of the leaver purge.
    /// </summary>
    [Fact]
    public async Task TheRequiredIndexes_Exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var indexes = await InstallationSchemaQueries.IndexNamesAsync(host, CourseTestData.MembershipTable, ct);

        Assert.Equal(
            new[]
            {
                CourseTestData.Constraints.MembershipCourseSeen,
                CourseTestData.Constraints.MembershipParticipant,
                CourseTestData.Constraints.MembershipOffRosterLastSeen,
                CourseTestData.Constraints.MembershipPrimaryKey,
                CourseTestData.Constraints.MembershipCourseParticipant,
            }.Order(StringComparer.Ordinal),
            indexes.Order(StringComparer.Ordinal));
    }

    /// <summary>db-design §5.3: the roster-on-a-date index carries the course and both instants, in that order.</summary>
    [Fact]
    public async Task TheRosterOnADateIndex_LeadsWithTheCourse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var definition = await host.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE indexname = @name",
            ct,
            ("name", CourseTestData.Constraints.MembershipCourseSeen));

        Assert.NotNull(definition);
        Assert.Contains("(course_id, first_seen_at, last_seen_at)", definition, StringComparison.Ordinal);
    }

    /// <summary>db-design §5.2: the role vocabulary and the instant ordering are the table's two check constraints.</summary>
    [Fact]
    public async Task TheCheckConstraints_AreTheRoleAndTheInstantOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var constraints = await InstallationSchemaQueries.CheckConstraintsAsync(host, CourseTestData.MembershipTable, ct);

        Assert.Equal(
            new[] { CourseTestData.Constraints.MembershipRole, CourseTestData.Constraints.MembershipSeenOrder },
            constraints);
    }
}
