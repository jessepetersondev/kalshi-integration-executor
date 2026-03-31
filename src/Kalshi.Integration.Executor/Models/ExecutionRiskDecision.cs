namespace Kalshi.Integration.Executor.Execution;

/// <summary>
/// Captures whether an order request was allowed and, if not, why it was blocked.
/// </summary>
public sealed record ExecutionRiskDecision(bool IsAllowed, string? Code = null, string? Reason = null)
{
    public static ExecutionRiskDecision Allow() => new(true);

    public static ExecutionRiskDecision Block(string code, string reason) => new(false, code, reason);
}
