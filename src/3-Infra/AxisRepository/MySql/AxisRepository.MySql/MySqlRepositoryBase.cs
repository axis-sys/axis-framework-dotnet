using System.Data.Common;
using Axis;
using AxisMediator.Contracts;
using MySqlConnector;

namespace AxisRepository.MySql;

/// <inheritdoc cref="AxisRepositoryBase{TCommand,TReader,TParameters}"/>
public abstract class MySqlRepositoryBase(
    IAxisMediator mediator,
    IAxisLogger<MySqlRepositoryBase> logger,
    IMySqlUnitOfWork uow
) : AxisRepositoryBase<MySqlCommand, MySqlDataReader, MySqlParameterCollection>(mediator, uow)
{
    protected override bool IsTransient(DbException exception) => MySqlTransientErrors.IsTransient(exception);

    protected override bool IsDuplicateKey(DbException exception)
        => exception is MySqlException { Number: 1062 }; // duplicate entry for key

    protected override bool IsSchemaMissing(DbException exception)
        => exception is MySqlException { Number: 1146 or 1049 }; // no such table / unknown database

    protected override string ErrorPrefix => "MYSQL";

    protected override void LogError(Exception exception, string message) => logger.LogError(exception, message);
}
