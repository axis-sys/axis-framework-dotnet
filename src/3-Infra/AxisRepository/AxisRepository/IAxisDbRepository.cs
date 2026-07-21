using System.Data.Common;

namespace Axis;

/// <summary>
/// Dialect-agnostic execution surface for a repository: the same retry/error/AxisResult machinery as
/// <see cref="AxisRepositoryBase{TCommand,TReader,TParameters}"/>, but over the common ADO.NET
/// abstractions — a named-parameter binder and <see cref="DbDataReader"/>. A shared repository composes
/// this (injected per dialect) instead of inheriting a provider-specific base, so a single repository
/// implementation runs on Postgres, MySQL, or any other provider.
/// </summary>
public interface IAxisDbRepository
{
    /// <summary>
    /// Executes a non-query (insert/update/delete) inside the unit of work. <paramref name="duplicateKeyCode"/>
    /// overrides the conflict error code returned on a unique-constraint violation; when null (the default),
    /// the dialect's default duplicate-key code is used.
    /// </summary>
    Task<AxisResult> ExecuteAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null);

    /// <summary>
    /// Executes a non-query and returns the number of rows affected. Lets a caller branch on "did the
    /// guarded UPDATE actually fire?" / "how many rows did it touch?" without a dialect-specific
    /// <c>RETURNING</c> (Postgres) or <c>ROW_COUNT()</c> (MySQL) — the affected count that
    /// <see cref="System.Data.Common.DbCommand.ExecuteNonQueryAsync(System.Threading.CancellationToken)"/>
    /// already returns flows back through the same retry/fault machinery as the other methods.
    /// </summary>
    Task<AxisResult<int>> ExecuteCountAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null);

    /// <summary>
    /// Executes a query and maps the first row via <paramref name="map"/>, returning <paramref name="notFoundCode"/>
    /// as a NotFound error when no row matches.
    /// </summary>
    Task<AxisResult<T>> GetAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map, string notFoundCode);

    /// <summary>Executes a query and maps every row via <paramref name="map"/> into the result sequence.</summary>
    Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map);
}
