using System.Collections.Generic;
using AsyncGuard.Logging;

namespace AsyncGuard.Tests.Infrastructure;

internal sealed class FakeAsyncGuardLogger : IAsyncGuardLogger
{
    private readonly List<(Exception Exception, string? TaskName)> _entries = new();

    public IReadOnlyList<(Exception Exception, string? TaskName)> Entries => _entries;

    public void Log(Exception exception, string? taskName = null)
    {
        _entries.Add((exception, taskName));
    }
}
