using Kalshi.Integration.Executor.Messaging;

namespace Kalshi.Integration.Executor.Routing;

/// <summary>
/// Carries the resolved route and parsed envelope for a single inbound event.
/// </summary>
public sealed record ExecutorRoutingResult(ExecutorRoute Route, ApplicationEventEnvelope Envelope);