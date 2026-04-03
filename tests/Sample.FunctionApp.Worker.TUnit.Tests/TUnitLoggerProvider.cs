using Microsoft.Extensions.Logging;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Routes framework logs to TUnit's <see cref="TestContext"/> so infrastructure output
/// (gRPC, worker startup, etc.) appears with the active test.
/// </summary>
public sealed class TUnitLoggerProvider : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new TUnitForwardingLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class TUnitForwardingLogger : ILogger
    {
        private readonly string _category;

        public TUnitForwardingLogger(string category) => _category = category;

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                var current = TestContext.Current;
                if (current is null)
                {
                    return;
                }

                current.Output.WriteLine($"[{logLevel,-11}] {_category}: {formatter(state, exception)}");
                if (exception != null)
                {
                    current.Output.WriteLine(exception.ToString());
                }
            }
            catch (InvalidOperationException)
            {
                // No active test context (e.g. background work after teardown).
            }
        }
    }
}
