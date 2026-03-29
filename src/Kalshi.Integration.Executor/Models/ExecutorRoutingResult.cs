using Kalshi.Integration.Executor.Messaging;


namespace Kalshi.Integration.Executor.Routing;

public sealed record ExecutorRoutingResult(ExecutorRoute Route, ApplicationEventEnvelope Envelope);