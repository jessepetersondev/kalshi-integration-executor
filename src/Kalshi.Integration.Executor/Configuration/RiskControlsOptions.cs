using System.ComponentModel.DataAnnotations;

namespace Kalshi.Integration.Executor.Configuration;

/// <summary>
/// Represents configuration for risk controls.
/// </summary>


public sealed class RiskControlsOptions
{
    public const string SectionName = "RiskControls";

    public bool LiveExecutionEnabled { get; set; } = true;

    public bool KillSwitchEnabled { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxOrderQuantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxLimitPriceDollars { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxOrderNotionalDollars { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MaxDailyNotionalDollars { get; set; }

    public List<string> AllowedTickers { get; set; } = [];

    public List<string> AllowedTickerPrefixes { get; set; } = [];

    public List<string> DeniedTickers { get; set; } = [];
}
