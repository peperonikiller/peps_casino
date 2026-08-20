using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace PepSlotMachine.Server;

[Injectable]
public class CasinoStatsService(ISptLogger<CasinoStatsService> logger)
{
    // Shared across DI instances so Blackjack writes and the stats route
    // always see the same in-memory state.
    private static readonly object _sync = new();
    private static readonly string _statePath = Path.Combine(
        AppContext.BaseDirectory, "user", "mods", "PepSlotMachine", "casino_stats.json");
    private static bool _loaded;
    private static Dictionary<string, CasinoPlayerStats> _stats = new(StringComparer.OrdinalIgnoreCase);

    public CasinoPlayerStats Get(string profileId)
    {
        lock (_sync)
        {
            EnsureLoaded();
            if (!_stats.TryGetValue(profileId, out var stats))
            {
                stats = NewStats(profileId);
                _stats[profileId] = stats;
                Save();
            }
            return Clone(stats);
        }
    }

    public void RecordSlotSpin(string profileId, int bet, int baseWin, int jackpotPayout)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var s = GetOrCreate(profileId);
            int returned = Math.Max(0, baseWin) + Math.Max(0, jackpotPayout);
            s.SlotSpins++;
            s.GpWagered += Math.Max(0, bet);
            s.GpReturned += returned;
            s.BiggestSlotReturn = Math.Max(s.BiggestSlotReturn, returned);
            if (jackpotPayout > 0)
            {
                s.JackpotsWon++;
                s.BiggestJackpot = Math.Max(s.BiggestJackpot, jackpotPayout);
            }
            s.LastUpdatedUtc = DateTime.UtcNow;

            AddHistory(
                s,
                new CasinoHistoryEntry
                {
                    Type = "SLOT",
                    Result =
                        jackpotPayout > 0
                            ? "JACKPOT"
                            : returned > bet
                                ? "WIN"
                                : returned == bet
                                    ? "PUSH"
                                    : "LOSS",
                    Wager = Math.Max(0, bet),
                    Return = returned,
                    Net = returned - Math.Max(0, bet),
                    Detail =
                        jackpotPayout > 0
                            ? $"JACKPOT +{jackpotPayout:N0} GP"
                            : returned > 0
                                ? $"+{returned:N0} GP"
                                : "NO WIN",
                    Utc = DateTime.UtcNow
                });

            Save();
        }
    }

    public void RecordBlackjackHand(string profileId, int wager, int payout, string result)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var s = GetOrCreate(profileId);
            int w = Math.Max(0, wager);
            int p = Math.Max(0, payout);
            s.BlackjackHands++;
            s.RoublesWagered += w;
            s.RoublesReturned += p;
            s.BiggestBlackjackProfit = Math.Max(s.BiggestBlackjackProfit, p - w);

            switch (result?.ToUpperInvariant())
            {
                case "BLACKJACK":
                    s.BlackjackWins++;
                    s.NaturalBlackjacks++;
                    break;
                case "WIN":
                    s.BlackjackWins++;
                    break;
                case "PUSH":
                    s.BlackjackPushes++;
                    break;
                default:
                    s.BlackjackLosses++;
                    break;
            }

            s.LastUpdatedUtc = DateTime.UtcNow;
            Save();
        }
    }

    public void RecordBlackjackRound(
        string profileId,
        int wager,
        int payout,
        string result,
        int handCount)
    {
        lock (_sync)
        {
            EnsureLoaded();

            var stats =
                GetOrCreate(
                    profileId);

            int safeWager =
                Math.Max(
                    0,
                    wager);

            int safePayout =
                Math.Max(
                    0,
                    payout);

            int net =
                safePayout -
                safeWager;

            AddHistory(
                stats,
                new CasinoHistoryEntry
                {
                    Type = "BLACKJACK",
                    Result =
                        string.IsNullOrWhiteSpace(result)
                            ? "MIXED"
                            : result,
                    Wager = safeWager,
                    Return = safePayout,
                    Net = net,
                    Detail =
                        handCount > 1
                            ? $"{handCount} HANDS"
                            : "1 HAND",
                    Utc = DateTime.UtcNow
                });

            stats.LastUpdatedUtc =
                DateTime.UtcNow;

            Save();
        }
    }

    public void RecordBlackjackInsurance(string profileId,int wager,int payout)
    {
        lock (_sync)
        {
            EnsureLoaded();
            var stats=GetOrCreate(profileId);
            int w=Math.Max(0,wager);
            int p=Math.Max(0,payout);
            stats.InsuranceBets++;
            if(p>w)stats.InsuranceWins++;
            stats.RoublesWagered+=w;
            stats.RoublesReturned+=p;
            stats.LastUpdatedUtc=DateTime.UtcNow;
            Save();
        }
    }

    private static void AddHistory(
        CasinoPlayerStats stats,
        CasinoHistoryEntry entry)
    {
        stats.RecentHistory ??=
            new List<CasinoHistoryEntry>();

        stats.RecentHistory.Insert(
            0,
            entry);

        const int maxEntries =
            20;

        if (stats.RecentHistory.Count >
            maxEntries)
        {
            stats.RecentHistory.RemoveRange(
                maxEntries,
                stats.RecentHistory.Count -
                maxEntries);
        }
    }

    private CasinoPlayerStats GetOrCreate(string profileId)
    {
        if (!_stats.TryGetValue(profileId, out var stats))
        {
            stats = NewStats(profileId);
            _stats[profileId] = stats;
        }
        return stats;
    }

    private static CasinoPlayerStats NewStats(string profileId) => new()
    {
        ProfileId = profileId,
        LastUpdatedUtc = DateTime.UtcNow
    };

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            string? dir = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(_statePath))
            {
                Save();
                return;
            }

            var loaded = JsonSerializer.Deserialize<Dictionary<string, CasinoPlayerStats>>(
                File.ReadAllText(_statePath));

            if (loaded != null)
                _stats = new Dictionary<string, CasinoPlayerStats>(
                    loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not load casino stats: {ex}");
            _stats = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(
                _stats, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not save casino stats: {ex}");
        }
    }

    private static CasinoPlayerStats Clone(CasinoPlayerStats x) => new()
    {
        ProfileId = x.ProfileId,
        SlotSpins = x.SlotSpins,
        GpWagered = x.GpWagered,
        GpReturned = x.GpReturned,
        BiggestSlotReturn = x.BiggestSlotReturn,
        JackpotsWon = x.JackpotsWon,
        BiggestJackpot = x.BiggestJackpot,
        BlackjackHands = x.BlackjackHands,
        BlackjackWins = x.BlackjackWins,
        BlackjackLosses = x.BlackjackLosses,
        BlackjackPushes = x.BlackjackPushes,
        NaturalBlackjacks = x.NaturalBlackjacks,
        InsuranceBets = x.InsuranceBets,
        InsuranceWins = x.InsuranceWins,
        RoublesWagered = x.RoublesWagered,
        RoublesReturned = x.RoublesReturned,
        BiggestBlackjackProfit = x.BiggestBlackjackProfit,
        RecentHistory =
            (x.RecentHistory ?? new List<CasinoHistoryEntry>())
                .Select(
                    h =>
                        new CasinoHistoryEntry
                        {
                            Type = h.Type,
                            Result = h.Result,
                            Wager = h.Wager,
                            Return = h.Return,
                            Net = h.Net,
                            Detail = h.Detail,
                            Utc = h.Utc
                        })
                .ToList(),
        LastUpdatedUtc = x.LastUpdatedUtc
    };
}

public class CasinoPlayerStats
{
    public string ProfileId { get; set; } = string.Empty;
    public long SlotSpins { get; set; }
    public long GpWagered { get; set; }
    public long GpReturned { get; set; }
    public int BiggestSlotReturn { get; set; }
    public int JackpotsWon { get; set; }
    public int BiggestJackpot { get; set; }
    public long BlackjackHands { get; set; }
    public long BlackjackWins { get; set; }
    public long BlackjackLosses { get; set; }
    public long BlackjackPushes { get; set; }
    public long NaturalBlackjacks { get; set; }
    public long InsuranceBets { get; set; }
    public long InsuranceWins { get; set; }
    public long RoublesWagered { get; set; }
    public long RoublesReturned { get; set; }
    public int BiggestBlackjackProfit { get; set; }

    public List<CasinoHistoryEntry> RecentHistory { get; set; } =
        new();

    public DateTime LastUpdatedUtc { get; set; }
    public long GpNet => GpReturned - GpWagered;
    public long RoublesNet => RoublesReturned - RoublesWagered;
}


public class CasinoHistoryEntry
{
    public string Type { get; set; } =
        string.Empty;

    public string Result { get; set; } =
        string.Empty;

    public int Wager { get; set; }

    public int Return { get; set; }

    public int Net { get; set; }

    public string Detail { get; set; } =
        string.Empty;

    public DateTime Utc { get; set; }
}
