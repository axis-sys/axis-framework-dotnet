using System.Data.Common;
using MySqlConnector;

namespace AxisRepository.MySql;

/// <summary>
/// Single source of truth for which MySQL errors are transient — safe to retry on a fresh attempt:
/// lock contention (deadlock, lock-wait timeout) and connection-level blips. Used by
/// <see cref="MySqlRepositoryBase"/> and by any adapter that retries OUTSIDE the repository
/// unit-of-work (e.g. the AxisSaga MySQL store, which writes in autocommit), so both share the exact
/// same classification.
/// </summary>
public static class MySqlTransientErrors
{
    public static bool IsTransient(MySqlException exception) => exception.Number
        is 1213  // deadlock found when trying to get lock
        or 1205  // lock wait timeout exceeded
        or 1206  // lock table full — total number of locks exceeds the lock table size
        or 1040  // too many connections
        or 1042  // unable to connect to host / connection pool exhausted (MySqlConnector: "Connect Timeout expired. All pooled connections are in use.")
        or 1053  // server shutdown in progress
        or 2002  // can't connect (socket)
        or 2003  // can't connect (TCP)
        or 2004  // can't create TCP/IP socket
        or 2006  // server has gone away
        or 2013  // lost connection during query
        or 4031; // client interaction timeout — idle pooled connection dropped by wait_timeout/interactive_timeout (MySQL 8.0.24+)

    public static bool IsTransient(DbException exception) => exception is MySqlException mysql && IsTransient(mysql);
}
