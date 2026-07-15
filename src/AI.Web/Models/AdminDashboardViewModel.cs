namespace AI.Web.Models;

public class AdminDashboardViewModel
{
    public string DefaultProvider { get; set; } = string.Empty;
    public int ProviderCount { get; set; }
    public int RecentRunCount { get; set; }
    public AgentSettingsViewModel AgentSettings { get; set; } = new();
    public IReadOnlyList<ProviderDiagnosticsViewModel> Providers { get; set; } = Array.Empty<ProviderDiagnosticsViewModel>();
    public IReadOnlyList<RunHistoryItemViewModel> RecentRuns { get; set; } = Array.Empty<RunHistoryItemViewModel>();
}
