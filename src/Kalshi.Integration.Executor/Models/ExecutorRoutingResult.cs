using Kalshi.Integration.Executor.Messaging;


namespace Kalshi.Integration.Executor.Routing;
/// <summary>
/// Represents the result of executor routing.
/// </summary>


public sealed record ExecutorRoutingResult(ExecutorRoute Route, ApplicationEventEnvelope Envelope);