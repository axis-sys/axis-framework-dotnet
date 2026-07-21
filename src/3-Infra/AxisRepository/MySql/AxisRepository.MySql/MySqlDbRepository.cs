using Axis;
using AxisMediator.Contracts;

namespace AxisRepository.MySql;

/// <summary>
/// The ready-to-use concrete <see cref="IAxisDbRepository"/> executor for MySQL — the dialect twin of
/// <c>AxisRepository.Postgres.PostgresDbRepository</c>. A dialect-agnostic repository composes this (one
/// instance per unit-of-work key) instead of inheriting a provider-specific base. Register it with
/// <see cref="DependencyInjection.AddMySqlDbRepository"/>.
/// </summary>
public sealed class MySqlDbRepository(
    IAxisMediator mediator,
    IAxisLogger<MySqlRepositoryBase> logger,
    IMySqlUnitOfWork uow
) : MySqlRepositoryBase(mediator, logger, uow);
