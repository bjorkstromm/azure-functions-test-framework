using Microsoft.Extensions.Logging;
using Xunit;

namespace Sample.FunctionApp.CustomRoutePrefix.Tests;

/// <summary>
/// Routes framework logs to xUnit's <see cref="ITestOutputHelper"/> so that
/// infrastructure output (gRPC, worker startup, etc.) appears inline with the test that
/// produced it.
/// </summary>
public sealed class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XUnitLoggerProvider(ITestOutputHelper output) => _output = output;

    public ILogger CreateLogger(string categoryName) => new XUnitLogger(_output, categoryName);

    public void Dispose() { }

    private sealed class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _category;

        public XUnitLogger(ITestOutputHelper output, string category)
        {
            _output = output;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            try
            {
                _output.WriteLine($"[{logLevel,-11}] {_category}: {formatter(state, exception)}");
                if (exception != null)
                    _output.WriteLine(exception.ToString());
            }
            catch (InvalidOperationException)
            {
                // ITestOutputHelper throws when called outside of an active test.
            }
        }
    }
}
