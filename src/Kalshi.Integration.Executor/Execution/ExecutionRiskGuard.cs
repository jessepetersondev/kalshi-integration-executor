using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Execution;
/// <summary>
/// Represents execution risk guard.
/// </summary>


public sealed class ExecutionRiskGuard : IExecutionRiskGuard
{
    private readonly RiskControlsOptions _options;
    private readonly IExecutionRecordStore _executionRecordStore;

    public ExecutionRiskGuard(IOptions<RiskControlsOptions> options, IExecutionRecordStore executionRecordStore)
    {
        _options = options.Value;
        _executionRecordStore = executionRecordStore;
    }

    public async Task<ExecutionRiskDecision> EvaluateAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.LiveExecutionEnabled)
        {
            return ExecutionRiskDecision.Block("live_execution_disabled", "Live execution is disabled.");
        }

        if (_options.KillSwitchEnabled)
        {
            return ExecutionRiskDecision.Block("kill_switch_enabled", "Kill switch is enabled.");
        }

        if (_options.DeniedTickers.Any(x => string.Equals(x, request.MarketTicker, StringComparison.OrdinalIgnoreCase)))
        {
            return ExecutionRiskDecision.Block("ticker_denied", $"Ticker '{request.MarketTicker}' is denied.");
        }

        var hasAllowedTickers = _options.AllowedTickers.Count > 0 || _options.AllowedTickerPrefixes.Count > 0;
        if (hasAllowedTickers)
        {
            var exactAllowed = _options.AllowedTickers.Any(x => string.Equals(x, request.MarketTicker, StringComparison.OrdinalIgnoreCase));
            var prefixAllowed = _options.AllowedTickerPrefixes.Any(x => request.MarketTicker.StartsWith(x, StringComparison.OrdinalIgnoreCase));
            if (!exactAllowed && !prefixAllowed)
            {
                return ExecutionRiskDecision.Block("ticker_not_allowed", $"Ticker '{request.MarketTicker}' is not allowlisted.");
            }
        }

        if (_options.MaxOrderQuantity > 0 && request.Quantity > _options.MaxOrderQuantity)
        {
            return ExecutionRiskDecision.Block("max_order_quantity_exceeded", $"Order quantity {request.Quantity} exceeds max {_options.MaxOrderQuantity}.");
        }

        if (_options.MaxLimitPriceDollars > 0m && request.LimitPrice > _options.MaxLimitPriceDollars)
        {
            return ExecutionRiskDecision.Block("max_limit_price_exceeded", $"Limit price {request.LimitPrice} exceeds max {_options.MaxLimitPriceDollars}.");
        }

        var orderNotional = request.LimitPrice * request.Quantity;
        if (_options.MaxOrderNotionalDollars > 0m && orderNotional > _options.MaxOrderNotionalDollars)
        {
            return ExecutionRiskDecision.Block("max_order_notional_exceeded", $"Order notional {orderNotional} exceeds max {_options.MaxOrderNotionalDollars}.");
        }

        if (_options.MaxDailyNotionalDollars > 0m)
        {
            var recent = await _executionRecordStore.ListRecentAsync(1000, cancellationToken);
            var todayUtc = DateTimeOffset.UtcNow.Date;
            var dailyNotional = recent
                .Where(x => x.RecordedAtUtc.UtcDateTime.Date == todayUtc)
                .Sum(x => x.NotionalDollars ?? 0m);
            if (dailyNotional + orderNotional > _options.MaxDailyNotionalDollars)
            {
                return ExecutionRiskDecision.Block(
                    "max_daily_notional_exceeded",
                    $"Daily notional {(dailyNotional + orderNotional)} would exceed max {_options.MaxDailyNotionalDollars}.");
            }
        }

        return ExecutionRiskDecision.Allow();
    }
}
