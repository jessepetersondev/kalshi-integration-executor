using System.ComponentModel.DataAnnotations;

namespace Kalshi.Integration.Executor.Configuration;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    [Required]
    public string ConnectionString { get; set; } = "Data Source=data/kalshi-integration-executor.db";
}
