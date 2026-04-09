namespace AIStudio.Settings.DataModel;

public sealed class DataMandatoryInformation
{
    /// <summary>
    /// Persisted user acceptances for configured mandatory infos.
    /// </summary>
    public List<DataMandatoryInfoAcceptance> Acceptances { get; set; } = [];

    public DataMandatoryInfoAcceptance? FindAcceptance(string infoId)
    {
        return this.Acceptances.LastOrDefault(acceptance => string.Equals(acceptance.InfoId, infoId, StringComparison.OrdinalIgnoreCase));
    }

    public bool RemoveLeftOverAcceptances(IEnumerable<DataMandatoryInfo> mandatoryInfos)
    {
        var validInfoIds = mandatoryInfos
            .Select(info => info.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removedCount = this.Acceptances.RemoveAll(acceptance => !validInfoIds.Contains(acceptance.InfoId));
        return removedCount > 0;
    }
}