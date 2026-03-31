namespace Kalshi.Integration.Executor.Routing;
/// <summary>
/// Represents an error related to unsupported event.
/// </summary>


public sealed class UnsupportedEventException : InvalidOperationException
{
    public UnsupportedEventException(string eventName)
        : base($"Unsupported event '{eventName}'.")
    {
        EventName = eventName;
    }

    public string EventName { get; }
}