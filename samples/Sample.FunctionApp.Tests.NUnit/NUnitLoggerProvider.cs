using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Sample.FunctionApp.Tests.NUnit;

/// <summary>
/// Routes framework logs to NUnit's <see cref="TestContext"/> so that infrastructure output
/// (gRPC, worker startup, etc.) appears inline with the test that produced it.
/// </summary>
public sealed class NUnitLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new NUnitLogger(categoryName);

    public void Dispose() { }

    private sealed class NUnitLogger : ILogger
    {
        private readonly string _category;

        public NUnitLogger(string category) => _category = category;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            try
            {
                TestContext.Out.WriteLine($"[{logLevel,-11}] {_category}: {formatter(state, exception)}");
                if (exception != null)
                    TestContext.Out.WriteLine(exception.ToString());
            }
            catch (Exception)
            {
                // TestContext may not be available outside of an active test.
            }
        }
    }
}
