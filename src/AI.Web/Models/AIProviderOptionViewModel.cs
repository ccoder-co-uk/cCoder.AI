namespace AI.Web.Models;

public class AIProviderOptionViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public bool SupportsModelListing { get; set; }
}
