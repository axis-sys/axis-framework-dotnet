using System.Data.Common;

namespace Axis;

/// <summary>
/// Surfaces a unit-of-work failure as a <see cref="DbException"/> so the repository base maps it to an
/// <see cref="AxisResult"/> error through the same provider-exception machinery, instead of letting it
/// propagate as a fatal, unmapped exception. Used where the signature cannot carry an <see cref="AxisResult"/>
/// — notably a failed lazy connection start inside <c>NewCommandAsync</c> (a <c>Task&lt;TCommand&gt;</c>).
/// </summary>
public sealed class AxisDbException(string message) : DbException(message);
