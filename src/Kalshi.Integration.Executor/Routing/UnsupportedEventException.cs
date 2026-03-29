namespace Kalshi.Integration.Executor.Routing;

public sealed class UnsupportedEventException : InvalidOperationException
{
    public UnsupportedEventException(string eventName)
        : base($"Unsupported event '{eventName}'.")
    {
        EventName = eventName;
    }

    public string EventName { get; }
}