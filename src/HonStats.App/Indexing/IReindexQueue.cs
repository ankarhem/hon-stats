using HonStats.Domain.Indexing;

namespace HonStats.App.Indexing;

public interface IReindexQueue
{
    Task<ReindexResult> RequestReindexAsync(Guid accountId, CancellationToken ct = default);
}
