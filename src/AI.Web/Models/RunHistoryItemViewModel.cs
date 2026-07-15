namespace AI.Web.Models;

public class RunHistoryItemViewModel
{
    public string Source { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public int Iterations { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset RecordedOn { get; set; }
    public double DurationMilliseconds { get; set; }
}
