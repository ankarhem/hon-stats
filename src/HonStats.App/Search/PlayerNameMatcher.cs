using HonStats.Domain.Players;

namespace HonStats.App.Search;

// Immutable, thread-safe fuzzy matcher over a player-name corpus. Built once from
// a snapshot of named players; the Infra layer swaps whole instances atomically to
// refresh, so this class performs NO internal locking — all fields are readonly.
//
// Candidate generation is the union of two indexes:
//   - PREFIX: a binary search over entry ids sorted by normalized key, recovering
//     the contiguous range whose key starts with the query; and
//   - TRIGRAM: a 3-char-substring inverted index (postings per trigram), which
//     catches names sharing character triples with the query even when neither is a
//     prefix of the other (the typical typo case).
// Every candidate is then scored by SearchMatcher.Score (exact > prefix > infix >
// token-fuzzy > whole-string fuzzy) and the top `limit` are returned, ordered by
// score descending with deterministic tiebreaks.
public sealed class PlayerNameMatcher
{
    private readonly PlayerCorpusEntry[] _entries;
    private readonly string[] _searchableNames;
    private readonly string[] _normalizedKeys;
    private readonly Dictionary<string, int[]> _trigrams;
    private readonly int[] _prefixOrder;

    /// <summary>
    /// Indexes <paramref name="corpus"/> for fast fuzzy name search. The matcher is
    /// immutable after construction. For each entry the searchable name is the first
    /// of (trimmed) Username / DisplayName that is non-empty; entries with no usable
    /// name are skipped. Entries are deduplicated by AccountId, keeping the FIRST
    /// occurrence in corpus order.
    /// </summary>
    public PlayerNameMatcher(IReadOnlyList<PlayerCorpusEntry> corpus)
    {
        var entries = new List<PlayerCorpusEntry>();
        var searchableNames = new List<string>();
        var normalizedKeys = new List<string>();
        var seen = new HashSet<Guid>();

        foreach (var entry in corpus)
        {
            var name = SearchableName(entry);
            if (name is null)
                continue;
            if (!seen.Add(entry.AccountId))
                continue; // keep first occurrence per AccountId

            entries.Add(entry);
            searchableNames.Add(name);
            normalizedKeys.Add(SearchMatcher.Normalize(name));
        }

        _entries = entries.ToArray();
        _searchableNames = searchableNames.ToArray();
        _normalizedKeys = normalizedKeys.ToArray();
        _trigrams = BuildTrigrams(_normalizedKeys);
        _prefixOrder = BuildPrefixOrder(_normalizedKeys);
    }

    /// <summary>Number of indexed (deduplicated, named) entries.</summary>
    public int Count => _entries.Length;

    /// <summary>
    /// Returns up to <paramref name="limit"/> results whose name matches
    /// <paramref name="query"/>, best score first. A null/blank query or
    /// <paramref name="limit"/> &lt;= 0 yields an empty list (the page handler hides
    /// the dropdown on blank input).
    /// </summary>
    public IReadOnlyList<PlayerNameSearchResult> Search(string? query, int limit)
    {
        if (limit <= 0 || string.IsNullOrWhiteSpace(query))
            return Array.Empty<PlayerNameSearchResult>();

        var q = SearchMatcher.Normalize(query);
        if (q.Length == 0)
            return Array.Empty<PlayerNameSearchResult>();

        var candidates = new HashSet<int>();
        AddPrefixCandidates(candidates, q);
        AddTrigramCandidates(candidates, q);

        if (candidates.Count == 0)
            return Array.Empty<PlayerNameSearchResult>();

        var matches = new List<(PlayerNameSearchResult Result, string NormKey)>(candidates.Count);
        foreach (var id in candidates)
        {
            var score = SearchMatcher.Score(query, _searchableNames[id]);
            if (score <= 0)
                continue;
            matches.Add(
                (
                    new PlayerNameSearchResult
                    {
                        AccountId = _entries[id].AccountId,
                        Username = _searchableNames[id],
                        DisplayName = _entries[id].DisplayName,
                        Score = score,
                    },
                    _normalizedKeys[id]
                )
            );
        }

        return matches
            .OrderByDescending(m => m.Result.Score)
            .ThenBy(m => m.NormKey, StringComparer.Ordinal)
            .ThenBy(m => m.Result.AccountId)
            .Take(limit)
            .Select(m => m.Result)
            .ToList();
    }

    // First non-empty (after trim) of Username, DisplayName; null if neither is
    // usable. Username wins so a player's canonical handle is the primary key.
    private static string? SearchableName(PlayerCorpusEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Username))
            return entry.Username.Trim();
        if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            return entry.DisplayName.Trim();
        return null;
    }

    // 3-char-substring inverted index: trigram -> ascending, duplicate-free entry
    // ids. Ids are appended in increasing order, so each postings array is already
    // sorted; the `list[^1] != id` guard drops repeats when a trigram occurs more
    // than once within a single key.
    private static Dictionary<string, int[]> BuildTrigrams(string[] normalizedKeys)
    {
        var postings = new Dictionary<string, List<int>>();
        for (var id = 0; id < normalizedKeys.Length; id++)
        {
            var key = normalizedKeys[id];
            for (var i = 0; i <= key.Length - 3; i++)
            {
                var tri = key.AsSpan(i, 3).ToString();
                if (!postings.TryGetValue(tri, out var list))
                {
                    list = new List<int>();
                    postings[tri] = list;
                }
                if (list.Count == 0 || list[^1] != id)
                    list.Add(id);
            }
        }

        var result = new Dictionary<string, int[]>(postings.Count);
        foreach (var (tri, list) in postings)
            result[tri] = list.ToArray();
        return result;
    }

    // Entry ids sorted by normalized key (ordinal), id as a deterministic secondary.
    // Used to binary-search the contiguous range of keys sharing a query prefix.
    private static int[] BuildPrefixOrder(string[] normalizedKeys)
    {
        var order = new int[normalizedKeys.Length];
        for (var i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(
            order,
            (a, b) =>
            {
                var c = string.CompareOrdinal(normalizedKeys[a], normalizedKeys[b]);
                return c != 0 ? c : a.CompareTo(b);
            }
        );
        return order;
    }

    // Binary-searches the first key >= q, then collects the contiguous run of keys
    // starting with q. Safe to break on the first non-prefix key: the array is
    // ordinal-sorted, so any later key is >= that key and cannot be a q-prefix.
    private void AddPrefixCandidates(HashSet<int> candidates, string q)
    {
        var lo = 0;
        var hi = _prefixOrder.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (string.CompareOrdinal(_normalizedKeys[_prefixOrder[mid]], q) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        for (var i = lo; i < _prefixOrder.Length; i++)
        {
            var id = _prefixOrder[i];
            if (!_normalizedKeys[id].StartsWith(q, StringComparison.Ordinal))
                break;
            candidates.Add(id);
        }
    }

    // Adds every entry id whose key contains any of q's trigrams. Queries shorter
    // than 3 chars produce no trigrams, so short queries rely on the prefix path.
    private void AddTrigramCandidates(HashSet<int> candidates, string q)
    {
        for (var i = 0; i <= q.Length - 3; i++)
        {
            var tri = q.AsSpan(i, 3).ToString();
            if (_trigrams.TryGetValue(tri, out var ids))
            {
                foreach (var id in ids)
                    candidates.Add(id);
            }
        }
    }
}
