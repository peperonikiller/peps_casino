using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace PepSlotMachine.Server;

[Injectable]
public class JackpotService(ISptLogger<JackpotService> logger)
{
    private const int DefaultBaseJackpot = 500;
    private readonly object _sync = new();
    private readonly string _statePath = Path.Combine(
        AppContext.BaseDirectory, "user", "mods", "PepSlotMachine", "jackpot.json");
    private bool _loaded;
    private JackpotState _state = new()
    {
        Amount = DefaultBaseJackpot,
        BaseAmount = DefaultBaseJackpot,
        LastUpdatedUtc = DateTime.UtcNow
    };

    public JackpotState GetState()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return new JackpotState
            {
                Amount = _state.Amount,
                BaseAmount = _state.BaseAmount,
                LastWinner = _state.LastWinner,
                LastWinAmount = _state.LastWinAmount,
                LastUpdatedUtc = _state.LastUpdatedUtc
            };
        }
    }

    public void ApplySpin(
        int bet,
        bool isJackpot,
        string winnerName,
        out int jackpotPayout,
        out int jackpotAmount)
    {
        lock (_sync)
        {
            EnsureLoaded();

            int poolAfterBet =
                checked(
                    _state.Amount +
                    Math.Max(
                        0,
                        bet));

            jackpotPayout =
                isJackpot
                    ? poolAfterBet
                    : 0;

            _state.Amount =
                isJackpot
                    ? Math.Max(
                        0,
                        _state.BaseAmount)
                    : poolAfterBet;

            if (isJackpot)
            {
                _state.LastWinner =
                    string.IsNullOrWhiteSpace(
                        winnerName)
                        ? "Unknown"
                        : winnerName;

                _state.LastWinAmount =
                    jackpotPayout;
            }

            _state.LastUpdatedUtc =
                DateTime.UtcNow;

            Save();

            jackpotAmount =
                _state.Amount;
        }
    }

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

            JackpotState? loaded = JsonSerializer.Deserialize<JackpotState>(File.ReadAllText(_statePath));
            if (loaded == null) return;
            if (loaded.BaseAmount <= 0) loaded.BaseAmount = DefaultBaseJackpot;
            if (loaded.Amount < 0) loaded.Amount = loaded.BaseAmount;
            _state = loaded;
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not load jackpot state: {ex}");
            _state = new JackpotState
            {
                Amount = DefaultBaseJackpot,
                BaseAmount = DefaultBaseJackpot,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }
    }

    private void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(
                _state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.Warning($"Could not save jackpot state: {ex}");
        }
    }
}

public class JackpotState
{
    public int Amount { get; set; }
    public int BaseAmount { get; set; }
    public string? LastWinner { get; set; }
    public int LastWinAmount { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
