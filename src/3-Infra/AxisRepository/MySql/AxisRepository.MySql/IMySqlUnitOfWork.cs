using Axis;
using MySqlConnector;

namespace AxisRepository.MySql;

/// <summary>
/// MySQL unit of work. All members come from <see cref="IDbUnitOfWork{TCommand}"/> closed over
/// <see cref="MySqlCommand"/>; this interface only fixes the dialect type so consumers keep injecting
/// <c>IMySqlUnitOfWork</c>.
/// </summary>
public interface IMySqlUnitOfWork : IDbUnitOfWork<MySqlCommand>;
