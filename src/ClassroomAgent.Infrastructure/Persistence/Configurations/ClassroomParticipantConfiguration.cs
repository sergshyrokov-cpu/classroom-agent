using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassroomAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table <c>classroom_participant</c> exactly as US-014 db-design §4 defines it: the Google <c>userId</c> is the
/// upsert key and the person's only identity, the address is optional and <b>not unique</b> (OD-011), and there is
/// no role column at all — the role lives on <see cref="CourseMembership"/> (BR-050, db-design §4.3).
/// </summary>
/// <remarks>
/// Adding <c>IsUnique()</c> to <c>ix_classroom_participant_email</c> would reintroduce the defect OD-011 avoided:
/// Google permits a deleted account's address to be reused by a new <c>userId</c>, and a unique index would turn
/// that legitimate reuse into a failed import for the whole school (db-design §4.2).
/// </remarks>
public sealed class ClassroomParticipantConfiguration : IEntityTypeConfiguration<ClassroomParticipant>
{
    public void Configure(EntityTypeBuilder<ClassroomParticipant> builder)
    {
        builder.ToTable("classroom_participant");

        builder.HasKey(p => p.Id).HasName("pk_classroom_participant");
        builder.Property(p => p.Id).UseIdentityByDefaultColumn();
        builder.Property(p => p.GoogleUserId).HasMaxLength(ClassroomParticipant.MaxGoogleUserIdLength).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(ClassroomParticipant.MaxEmailLength);
        builder.Property(p => p.FullName).HasMaxLength(ClassroomParticipant.MaxFullNameLength);
        builder.Property(p => p.Surname).HasMaxLength(ClassroomParticipant.MaxSurnameLength);
        builder.Property(p => p.GivenName).HasMaxLength(ClassroomParticipant.MaxGivenNameLength);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasIndex(p => p.GoogleUserId)
            .IsUnique()
            .HasDatabaseName("uq_classroom_participant_google_user_id");

        // Not unique, on purpose (OD-011): Epic 4 resolves an address through the roster of the course on the
        // meeting's date (PC-12, BR-051), never against this table globally.
        builder.HasIndex(p => p.Email).HasDatabaseName("ix_classroom_participant_email");

        // US-032 db-design §5.2: ix_classroom_participant_email_lower, an expression index on lower(email), keeps the
        // case-insensitive lookup by a set of emails an index scan. EF Core cannot express an expression index in
        // the model, so it exists only in the AddMeetingCodeLinks migration (migrationBuilder.Sql CREATE/DROP INDEX).
    }
}
