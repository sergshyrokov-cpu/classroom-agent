using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// The one definition of a search match (US-070 spec FR-001, BR-087; db-design §2); nothing else builds a search
/// condition. A field and a word are folded the same way in SQL: lowered under the explicit collation, with the three
/// apostrophes made one. A word is found by <c>strpos</c>, so <c>%</c>, <c>_</c> and <c>\</c> mean nothing. It stays a bound
/// parameter: it is read from a holder object, which EF Core parameterizes, never placed in the tree as a constant.
/// </summary>
internal static class SearchMatch
{
    private const string Collation = ClassroomAgentDbContext.CourseListCollation;

    private static readonly Expression<Func<string?, WordHolder, bool>> FoundInField = (field, word) =>
        field != null
        && EF.Functions.Collate(field, Collation).ToLower().Replace("’", "'").Replace("ʼ", "'")
            .Contains(EF.Functions.Collate(word.Value, Collation).ToLower().Replace("’", "'").Replace("ʼ", "'"));

    private static readonly Expression<Func<string, WordHolder, bool>> FoundInCode = (code, word) =>
        EF.Functions.Collate(code, Collation).ToLower().Replace("’", "'").Replace("ʼ", "'").Replace("-", string.Empty)
            .Contains(EF.Functions.Collate(word.Value, Collation).ToLower().Replace("’", "'").Replace("ʼ", "'"));

    /// <summary>FR-001 rule 1: the words of a validated text, split at any white space, with no empty word.</summary>
    public static IReadOnlyList<string> Words(string? text) =>
        text is null ? [] : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>FR-001 rules 3 to 5, 7: the word is in this field; a null field does not hold it.</summary>
    public static Expression<Func<T, bool>> InField<T>(Expression<Func<T, string?>> field, string word) =>
        Apply<T, string?>(FoundInField, field, word);

    /// <summary>
    /// FR-001 rule 6: the word is in this Meet code, hyphens ignored on both sides. Null when the word has nothing left, as
    /// <c>strpos(x, '')</c> would match every code; the word may still match the record's other fields.
    /// </summary>
    public static Expression<Func<T, bool>>? InCode<T>(Expression<Func<T, string>> code, string word)
    {
        var withoutHyphens = word.Replace("-", string.Empty, StringComparison.Ordinal);
        return withoutHyphens.Length == 0 ? null : Apply<T, string>(FoundInCode, code, withoutHyphens);
    }

    /// <summary>FR-001 rule 2: a record holds a word when at least one of these terms does. A null term is left out.</summary>
    public static Expression<Func<T, bool>> AnyOf<T>(params Expression<Func<T, bool>>?[] terms)
    {
        var record = Expression.Parameter(typeof(T), "record");
        var body = terms
            .OfType<Expression<Func<T, bool>>>()
            .Select(term => new Replacer(term.Parameters[0], record).Visit(term.Body))
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<T, bool>>(body, record);
    }

    private static Expression<Func<T, bool>> Apply<T, TValue>(
        Expression<Func<TValue, WordHolder, bool>> template, Expression<Func<T, TValue>> field, string word)
    {
        var body = new Replacer(template.Parameters[0], field.Body).Visit(template.Body);
        body = new Replacer(template.Parameters[1], Expression.Constant(new WordHolder(word))).Visit(body);
        return Expression.Lambda<Func<T, bool>>(body, field.Parameters);
    }

    /// <summary>Db-design §2: the word's home, so that it is parameterized rather than written into the SQL.</summary>
    private sealed class WordHolder(string value)
    {
        public string Value { get; } = value;
    }

    private sealed class Replacer(Expression from, Expression to) : ExpressionVisitor
    {
        [return: NotNullIfNotNull(nameof(node))]
        public override Expression? Visit(Expression? node) => node == from ? to : base.Visit(node);
    }
}
