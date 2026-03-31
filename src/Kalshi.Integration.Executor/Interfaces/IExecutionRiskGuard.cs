using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Execution;

namespace Kalshi.Integration.Executor.Execution;
/// <summary>
/// Defines the contract for execution risk guard.
/// </summary>


public interface IExecutionRiskGuard
{
    Task<ExecutionRiskDecision> EvaluateAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default);
}
