using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace PepSlotMachine.Server;

[Injectable]
public class CasinoServerConfigService(
    ISptLogger<CasinoServerConfigService> logger)
{
    private static readonly object Sync = new();

    private static readonly string ConfigPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "user",
            "mods",
            "PepSlotMachine",
            "casino_config.json");

    private static CasinoServerConfig? _cached;
    private static DateTime _lastWriteUtc = DateTime.MinValue;

    public CasinoServerConfig Get()
    {
        lock (Sync)
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigPath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (!File.Exists(ConfigPath))
                {
                    _cached = new CasinoServerConfig();
                    Save(_cached);
                    return Clone(_cached);
                }

                DateTime writeUtc = File.GetLastWriteTimeUtc(ConfigPath);

                if (_cached == null || writeUtc != _lastWriteUtc)
                {
                    CasinoServerConfig? loaded =
                        JsonSerializer.Deserialize<CasinoServerConfig>(
                            File.ReadAllText(ConfigPath));

                    _cached = Sanitize(
                        loaded ?? new CasinoServerConfig());

                    _lastWriteUtc = writeUtc;
                }

                return Clone(_cached);
            }
            catch (Exception ex)
            {
                logger.Warning(
                    $"Could not load Pep's Casino server config: {ex}");

                _cached ??= new CasinoServerConfig();

                return Clone(_cached);
            }
        }
    }

    private static CasinoServerConfig Sanitize(
        CasinoServerConfig config)
    {
        config.BuyInCostRoubles =
            Math.Max(
                0,
                config.BuyInCostRoubles);

        config.BlackjackMinBet =
            Math.Max(
                1,
                config.BlackjackMinBet);

        config.BlackjackMaxBet =
            Math.Max(
                config.BlackjackMinBet,
                config.BlackjackMaxBet);

        return config;
    }

    private static void Save(CasinoServerConfig config)
    {
        File.WriteAllText(
            ConfigPath,
            JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions { WriteIndented = true }));

        _lastWriteUtc = File.GetLastWriteTimeUtc(ConfigPath);
    }

    private static CasinoServerConfig Clone(
        CasinoServerConfig config)
    {
        return new CasinoServerConfig
        {
            BuyInCostRoubles =
                config.BuyInCostRoubles,
            BlackjackMinBet =
                config.BlackjackMinBet,
            BlackjackMaxBet =
                config.BlackjackMaxBet,
            BlackjackDiagnostics =
                config.BlackjackDiagnostics
        };
    }
}

public class CasinoServerConfig
{
    public int BuyInCostRoubles { get; set; } =
        10000;

    public int BlackjackMinBet { get; set; } =
        1000;

    public int BlackjackMaxBet { get; set; } =
        50000;

    public bool BlackjackDiagnostics { get; set; } =
        false;
}
