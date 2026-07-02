using HonStats.Domain.Insights;

namespace HonStats.App.Players;

public interface IMmrHistoryQuery
{
    Task<IReadOnlyList<MmrSnapshot>> GetAsync(
        Guid accountId,
        int days = 30,
        CancellationToken ct = default
    );
}
