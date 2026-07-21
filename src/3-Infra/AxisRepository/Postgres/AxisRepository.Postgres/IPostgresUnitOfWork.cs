using Axis;
using Npgsql;

namespace AxisRepository.Postgres;

/// <summary>
/// Postgres unit of work. All members (<c>NewCommandAsync</c>, <c>IsFaulted</c>, <c>MarkFaulted</c>)
/// come from <see cref="IDbUnitOfWork{TCommand}"/> closed over <see cref="NpgsqlCommand"/>; this
/// interface only fixes the dialect type so consumers keep injecting <c>IPostgresUnitOfWork</c>.
/// </summary>
public interface IPostgresUnitOfWork : IDbUnitOfWork<NpgsqlCommand>;
