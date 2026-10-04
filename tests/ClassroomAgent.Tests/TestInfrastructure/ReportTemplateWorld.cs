using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The US-027 use cases with every port in memory (TC-1): the templates, the journal fields, the audit trail, the
/// unit of work, the read-only guard and the clock. It exists so the Application-layer tests prove behaviour —
/// evaluation order, refusals, audit rows, what is written and what is not — without an HTTP host. The time zone is
/// Kyiv (TC-8). Nothing here can reach Google: no Google port is part of any constructor.
/// </summary>
public sealed class ReportTemplateWorld
{
    public const long AdminId = 501;

    public const long DeanId = 502;

    public ReportTemplateWorld(bool readOnly = false, DateTimeOffset? now = null)
    {
        Time = new ManualTimeProvider(now ?? InstallationTestHost.DefaultStart);
        ReadOnly = new DeanAccountWorld.Guard(readOnly);
        Templates = new TemplateRepository(Time);
        Audit = new DeanAccountWorld.AuditRepository();
        Work = new UnitOfWork(Audit, Templates);
        WriteScope = new ServiceWriteScope();
        Fields = new FakeJournalFieldSource();
        Zone = new SchoolTimeZone(JournalTestData.Kyiv);
    }

    public ManualTimeProvider Time { get; }

    public DeanAccountWorld.Guard ReadOnly { get; }

    public TemplateRepository Templates { get; }

    public DeanAccountWorld.AuditRepository Audit { get; }

    public UnitOfWork Work { get; }

    public ServiceWriteScope WriteScope { get; }

    public FakeJournalFieldSource Fields { get; }

    public SchoolTimeZone Zone { get; }

    public ListReportTemplatesQuery List => new(Templates, Zone);

    public GetReportTemplateFormQuery Forms => new(Templates);

    public SaveReportTemplateUseCase Save => new(ReadOnly, Templates, Audit, Work, WriteScope, Time);

    public DeleteReportTemplateUseCase Delete => new(ReadOnly, Templates, Audit, Work, WriteScope, Time);

    public GetReportQuery Report => new(Fields, Templates, Zone, Time);

    /// <summary>
    /// A created template already stored, made through <see cref="ReportTemplate.Create"/> on purpose: seeding any other
    /// way would prove a shape the production code does not produce.
    /// </summary>
    public ReportTemplate SeedTemplate(
        string name = "Test Template One",
        ReportTemplateSettings? settings = null,
        long authorId = DeanId,
        string? authorEmail = "dean.one@school-one.example.test")
    {
        var template = ReportTemplate.Create(name, settings ?? ReportTemplateTestData.Settings(), authorId);
        Templates.Seed(template, authorEmail);
        return template;
    }

    /// <summary>The templates, in memory, counting every call so the evaluation order can be asserted.</summary>
    public sealed class TemplateRepository(TimeProvider time) : IReportTemplateRepository
    {
        private readonly List<ReportTemplate> _templates = [];

        private readonly Dictionary<long, string?> _authorEmails = [];

        private long _nextId = 1;

        public IReadOnlyList<ReportTemplate> Stored => _templates;

        public List<ReportTemplate> Added { get; } = [];

        public List<ReportTemplate> Removed { get; } = [];

        public List<ReportTemplate> MarkedChanged { get; } = [];

        /// <summary>Every read and write the use cases made, by name, in order.</summary>
        public List<string> Calls { get; } = [];

        /// <summary>The <c>exceptId</c> of every uniqueness check.</summary>
        public List<long?> NameChecksExcept { get; } = [];

        /// <summary>Author ids whose account the "purge" removed: their email reads as null (spec FR-014).</summary>
        public HashSet<long> DeletedAuthors { get; } = [];

        public void Seed(ReportTemplate template, string? authorEmail)
        {
            Identify(template);
            _authorEmails[template.AuthorId] = authorEmail;
            _templates.Add(template);
        }

        /// <summary>Sets the last-change time a stored row would carry (the interceptor's job in production).</summary>
        public void SetUpdatedAt(ReportTemplate template, DateTimeOffset at) =>
            typeof(ReportTemplate).GetProperty(nameof(ReportTemplate.UpdatedAt))!.SetValue(template, at);

        public Task<IReadOnlyList<ReportTemplateListRecord>> ListAsync(CancellationToken cancellationToken)
        {
            Calls.Add(nameof(ListAsync));
            IReadOnlyList<ReportTemplateListRecord> rows = _templates
                .Select(t => new ReportTemplateListRecord(
                    t.Id,
                    t.Name,
                    t.UpdatedAt,
                    DeletedAuthors.Contains(t.AuthorId) ? null : _authorEmails.GetValueOrDefault(t.AuthorId)))
                .Reverse() // the port is unordered: the use case must sort
                .ToList();
            return Task.FromResult(rows);
        }

        public Task<ReportTemplate?> GetAsync(long id, bool forUpdate, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(GetAsync));
            return Task.FromResult(_templates.SingleOrDefault(t => t.Id == id));
        }

        public Task<bool> NameExistsAsync(string normalizedName, long? exceptId, CancellationToken cancellationToken)
        {
            Calls.Add(nameof(NameExistsAsync));
            NameChecksExcept.Add(exceptId);
            return Task.FromResult(_templates.Any(t =>
                string.Equals(t.NormalizedName, normalizedName, StringComparison.Ordinal) && t.Id != exceptId));
        }

        public void Add(ReportTemplate template)
        {
            Calls.Add(nameof(Add));
            Added.Add(template);
            _templates.Add(template);
        }

        public void MarkChanged(ReportTemplate template)
        {
            Calls.Add(nameof(MarkChanged));
            MarkedChanged.Add(template);
        }

        public void Remove(ReportTemplate template)
        {
            Calls.Add(nameof(Remove));
            Removed.Add(template);
            _templates.Remove(template);
        }

        /// <summary>What a commit does to a new row: the identity and the timestamps.</summary>
        internal void Commit()
        {
            foreach (var template in Added.Where(t => t.Id == 0))
            {
                Identify(template);
            }

            foreach (var template in MarkedChanged)
            {
                SetUpdatedAt(template, time.GetUtcNow());
            }
        }

        private void Identify(ReportTemplate template)
        {
            if (template.Id == 0)
            {
                typeof(ReportTemplate).GetProperty(nameof(ReportTemplate.Id))!.SetValue(template, _nextId++);
            }

            var now = time.GetUtcNow();
            typeof(ReportTemplate).GetProperty(nameof(ReportTemplate.CreatedAt))!.SetValue(template, now);
            SetUpdatedAt(template, now);
        }
    }

    /// <summary>Commits, counted; can be told to fail the next commit as the unique name index would.</summary>
    public sealed class UnitOfWork(DeanAccountWorld.AuditRepository audit, TemplateRepository templates) : IUnitOfWork
    {
        public int Commits { get; private set; }

        public int Transactions { get; private set; }

        /// <summary>How many templates were added and audit rows staged at each commit, in order.</summary>
        public List<(int Templates, int AuditRows)> Staged { get; } = [];

        /// <summary>When set, the next commit throws <see cref="UniqueReportTemplateNameViolationException"/> (db-design §2.1).</summary>
        public bool FailNextWithDuplicateName { get; set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FailNextWithDuplicateName)
            {
                FailNextWithDuplicateName = false;
                throw new UniqueReportTemplateNameViolationException(new InvalidOperationException("23505"));
            }

            Commits++;
            templates.Commit();
            Staged.Add((templates.Added.Count, audit.Written.Count));
            audit.WrittenAtLastCommit = audit.Written.Count;
            return Task.CompletedTask;
        }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            Transactions++;
            await work(cancellationToken);
        }
    }
}
