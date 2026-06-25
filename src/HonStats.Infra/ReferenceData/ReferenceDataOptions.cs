namespace HonStats.Infra.ReferenceData;

public sealed class ReferenceDataOptions
{
    public const string SectionName = "HonStats:ReferenceData";

    public int RefreshIntervalHours { get; set; } = 12;
}
