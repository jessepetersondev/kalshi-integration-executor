namespace Kalshi.Integration.Executor.Execution;

public sealed record ExecutionRiskDecision(bool IsAllowed, string? Code = null, string? Reason = null)
{
    public static ExecutionRiskDecision Allow() => new(true);

    public static ExecutionRiskDecision Block(string code, string reason) => new(false, code, reason);
}
