using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Execution;

namespace Kalshi.Integration.Executor.Execution;

public interface IExecutionRiskGuard
{
    Task<ExecutionRiskDecision> EvaluateAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default);
}
