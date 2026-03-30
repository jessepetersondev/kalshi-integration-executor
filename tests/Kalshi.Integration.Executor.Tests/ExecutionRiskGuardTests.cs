using Kalshi.Integration.Executor.Configuration;
using Kalshi.Integration.Executor.Execution;
using Kalshi.Integration.Executor.KalshiApi;
using Kalshi.Integration.Executor.Persistence;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class ExecutionRiskGuardTests
{
    [Fact]
    public async Task EvaluateAsyncShouldBlockWhenKillSwitchIsEnabled()
    {
        var guard = CreateGuard(new RiskControlsOptions
        {
            KillSwitchEnabled = true,
        });

        var decision = await guard.EvaluateAsync(CreateRequest());

        Assert.False(decision.IsAllowed);
        Assert.Equal("kill_switch_enabled", decision.Code);
    }

    [Fact]
    public async Task EvaluateAsyncShouldBlockTickerNotInAllowlist()
    {
        var guard = CreateGuard(new RiskControlsOptions
        {
            AllowedTickers = ["OTHER-TICKER"],
        });

        var decision = await guard.EvaluateAsync(CreateRequest());

        Assert.False(decision.IsAllowed);
        Assert.Equal("ticker_not_allowed", decision.Code);
    }

    [Fact]
    public async Task EvaluateAsyncShouldBlockWhenMaxOrderQuantityExceeded()
    {
        var guard = CreateGuard(new RiskControlsOptions
        {
            MaxOrderQuantity = 1,
        });

        var decision = await guard.EvaluateAsync(CreateRequest(quantity: 2));

        Assert.False(decision.IsAllowed);
        Assert.Equal("max_order_quantity_exceeded", decision.Code);
    }

    [Fact]
    public async Task EvaluateAsyncShouldBlockWhenDailyNotionalWouldBeExceeded()
    {
        var store = new InMemoryExecutionRecordStore();
        await store.UpsertAsync(new ExecutionRecord(
            "ext-1",
            "client-1",
            "resource-1",
            "corr-1",
            "KXBTC-1",
            "yes",
            "buy",
            "filled",
            1,
            1.00m,
            4.50m,
            "{}",
            DateTimeOffset.UtcNow));

        var guard = new ExecutionRiskGuard(
            Options.Create(new RiskControlsOptions
            {
                LiveExecutionEnabled = true,
                MaxDailyNotionalDollars = 5.00m,
            }),
            store);

        var decision = await guard.EvaluateAsync(CreateRequest(quantity: 1, limitPrice: 0.75m));

        Assert.False(decision.IsAllowed);
        Assert.Equal("max_daily_notional_exceeded", decision.Code);
    }

    private static ExecutionRiskGuard CreateGuard(RiskControlsOptions options)
    {
        options.LiveExecutionEnabled = true;
        return new ExecutionRiskGuard(Options.Create(options), new InMemoryExecutionRecordStore());
    }

    private static KalshiOrderRequest CreateRequest(int quantity = 1, decimal limitPrice = 0.50m)
        => new("KXBTC-26MAR2915-B74950", "yes", quantity, limitPrice, "client-1");
}
