using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Execution;

namespace Kalshi.Integration.Executor.Execution;

/// <summary>
/// Evaluates whether an execution request should be allowed or blocked.
/// </summary>
public interface IExecutionRiskGuard
{
    Task<ExecutionRiskDecision> EvaluateAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default);
}
