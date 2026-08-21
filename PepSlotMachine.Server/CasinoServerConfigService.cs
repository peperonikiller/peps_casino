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
                    _cached =
                        CreateDefaultConfig();

                    Save(
                        _cached);

                    return Clone(
                        _cached);
                }

                DateTime writeUtc = File.GetLastWriteTimeUtc(ConfigPath);

                if (_cached == null || writeUtc != _lastWriteUtc)
                {
                    CasinoServerConfig? loaded =
                        JsonSerializer.Deserialize<CasinoServerConfig>(
                            File.ReadAllText(ConfigPath));

                    _cached =
                        Sanitize(
                            loaded ??
                            CreateDefaultConfig());

                    // Phase 22A accidentally generated ShopItems as an empty
                    // array because the missing-file path instantiated
                    // CasinoServerConfig directly instead of CreateDefaultConfig.
                    //
                    // This one-time migration repairs that config. The marker is
                    // persisted, so after migration a server owner may intentionally
                    // empty ShopItems and it will remain empty.
                    if (!_cached.ShopDefaultsInitialized)
                    {
                        if (_cached.ShopItems.Count == 0)
                        {
                            _cached.ShopItems =
                                CreateDefaultShopItems();
                        }

                        _cached.ShopDefaultsInitialized =
                            true;

                        Save(
                            _cached);
                    }
                    else
                    {
                        _lastWriteUtc =
                            writeUtc;
                    }
                }

                return Clone(_cached);
            }
            catch (Exception ex)
            {
                logger.Warning(
                    $"Could not load Pep's Casino server config: {ex}");

                _cached ??=
                    CreateDefaultConfig();

                return Clone(
                    _cached);
            }
        }
    }

    public bool IsForceNextSlotJackpotEnabled()
    {
        return Get()
            .ForceNextSlotJackpot;
    }

    public void ClearForceNextSlotJackpot()
    {
        lock (Sync)
        {
            // Get() refreshes the cache if the file was edited externally.
            CasinoServerConfig current =
                Get();

            if (!current.ForceNextSlotJackpot)
            {
                return;
            }

            if (_cached == null)
            {
                _cached =
                    Sanitize(
                        current);
            }

            _cached.ForceNextSlotJackpot =
                false;

            Save(
                _cached);

            logger.Info(
                "Pep's Casino debug jackpot consumed; ForceNextSlotJackpot reset to false.");
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

        config.ShopItems ??= [];

        config.ShopItems =
            config.ShopItems
                .Where(x =>
                    x != null &&
                    !string.IsNullOrWhiteSpace(x.TemplateId) &&
                    x.ChipCost > 0 &&
                    x.Quantity > 0)
                .Select(x => new CasinoShopConfigItem
                {
                    TemplateId = x.TemplateId.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(x.DisplayName)
                        ? x.TemplateId.Trim()
                        : x.DisplayName.Trim(),
                    ChipCost = Math.Max(1, x.ChipCost),
                    Quantity = Math.Max(1, x.Quantity)
                })
                .ToList();

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

    private static CasinoServerConfig CreateDefaultConfig()
    {
        return new CasinoServerConfig
        {
            ShopDefaultsInitialized =
                true,
            ShopItems =
                CreateDefaultShopItems()
        };
    }

    private static List<CasinoShopConfigItem> CreateDefaultShopItems()
    {
        return
        [
            new CasinoShopConfigItem
            {
                TemplateId = "5d235b4d86f7742e017bc88a",
                DisplayName = "GP Coin",
                ChipCost = 1,
                Quantity = 1
            },
            new CasinoShopConfigItem
            {
                TemplateId = "544fb45d4bdc2dee738b4568",
                DisplayName = "Salewa First Aid Kit",
                ChipCost = 2,
                Quantity = 1
            }
        ];
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
                config.BlackjackDiagnostics,
            ForceNextSlotJackpot =
                config.ForceNextSlotJackpot,
            ShopDefaultsInitialized =
                config.ShopDefaultsInitialized,
            ShopItems =
                config.ShopItems
                    .Select(x => new CasinoShopConfigItem
                    {
                        TemplateId = x.TemplateId,
                        DisplayName = x.DisplayName,
                        ChipCost = x.ChipCost,
                        Quantity = x.Quantity
                    })
                    .ToList()
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

    // Debug/testing switch. When true, the next valid slot spin is forced
    // to exactly three Gold Skull symbols on the center payline. The server
    // clears this flag only after that spin completes successfully.
    public bool ForceNextSlotJackpot { get; set; } =
        false;

    public bool ShopDefaultsInitialized { get; set; } =
        false;

    public List<CasinoShopConfigItem> ShopItems { get; set; } =
        [];
}

public class CasinoShopConfigItem
{
    public string TemplateId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ChipCost { get; set; } = 1;
    public int Quantity { get; set; } = 1;
}
