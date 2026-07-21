using Axis;
using AxisMediator.Contracts;

namespace AxisRepository.Postgres;

/// <summary>
/// The ready-to-use concrete <see cref="IAxisDbRepository"/> executor for Postgres — the composition-pattern
/// counterpart to subclassing <see cref="PostgresRepositoryBase"/>. A dialect-agnostic repository composes this
/// (one instance per unit-of-work key) instead of inheriting a provider-specific base, so the same repository
/// runs on Postgres or MySQL by swapping the injected executor. Register it with
/// <see cref="DependencyInjection.AddPostgresDbRepository"/>.
/// </summary>
public sealed class PostgresDbRepository(
    IAxisMediator mediator,
    IAxisLogger<PostgresRepositoryBase> logger,
    IPostgresUnitOfWork uow
) : PostgresRepositoryBase(mediator, logger, uow);
