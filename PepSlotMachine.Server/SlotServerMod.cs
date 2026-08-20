using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Inventory;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace PepSlotMachine.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.pep.spt.slotmachine.server";
    public string Name { get; init; } = "Pep Slot Machine Server";
    public string Author { get; init; } = "Pep";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("2.9.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public class SlotStaticRouter(
    JsonUtil jsonUtil,
    SlotStaticRouterCallback callback)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<SlotSpinRequest>(
                "/pep-slots/spin",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleSpin(
                    url,
                    info,
                    sessionId)),
            new RouteAction<CasinoConfigRequest>(
                "/pep-casino/config",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleCasinoConfig(
                    info,
                    sessionId)),
            new RouteAction<CasinoBuyInRequest>(
                "/pep-casino/buyin",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleBuyIn(
                    info,
                    sessionId)),
            new RouteAction<JackpotStateRequest>(
                "/pep-casino/jackpot",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleJackpotState(
                    url,
                    info,
                    sessionId)),
            new RouteAction<CasinoStatsRequest>(
                "/pep-casino/stats",
                async (url, info, sessionId, output, cancellationToken)
                    => await callback.HandleCasinoStats(info, sessionId)),
            new RouteAction<BlackjackLobbyRequest>(
                "/pep-casino/blackjack/lobby",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleBlackjackLobby(
                    url,
                    info,
                    sessionId)),
            new RouteAction<BlackjackHostRequest>(
                "/pep-casino/blackjack/host",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleBlackjackHost(
                    url,
                    info,
                    sessionId)),
            new RouteAction<BlackjackJoinRequest>(
                "/pep-casino/blackjack/join",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleBlackjackJoin(
                    url,
                    info,
                    sessionId)),
            new RouteAction<BlackjackLeaveRequest>(
                "/pep-casino/blackjack/leave",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    cancellationToken
                ) => await callback.HandleBlackjackLeave(
                    url,
                    info,
                    sessionId))
        ])
{
}

[Injectable]
public class SlotStaticRouterCallback(
    ISptLogger<SlotStaticRouterCallback> logger,
    JsonUtil jsonUtil,
    ProfileHelper profileHelper,
    ServerCurrencyService currencyService,
    JackpotService jackpotService,
    CasinoStatsService casinoStatsService,
    CasinoServerConfigService casinoServerConfigService,
    BlackjackRoomService blackjackRoomService)
{
    private static readonly int[][] Paylines =
    [
        [1, 1, 1, 1, 1],
        [0, 0, 0, 0, 0],
        [2, 2, 2, 2, 2],
        [0, 1, 2, 1, 0],
        [2, 1, 0, 1, 2]
    ];

    public ValueTask<string> HandleSpin(
        string url,
        SlotSpinRequest info,
        MongoId sessionId)
    {
        try
        {
            if (!IsAllowedBet(
                    info.Bet))
            {
                return Response(
                    new SlotSpinResponse
                    {
                        Success =
                            false,
                        Message =
                            "INVALID BET"
                    });
            }

            var pmc =
                profileHelper.GetPmcProfile(
                    sessionId);

            if (pmc?.Inventory?.Items is null)
            {
                return Response(
                    new SlotSpinResponse
                    {
                        Success =
                            false,
                        Message =
                            "PMC PROFILE NOT AVAILABLE"
                    });
            }

            // The wager is now a normal EFT inventory transaction performed
            // before this route is called. Verify that the SPT profile reached
            // the exact post-wager balance reported by the live client.
            int balance =
                currencyService.GetBalance(
                    pmc,
                    CasinoCurrencies.Gp);

            if (balance !=
                info.ExpectedPostWagerBalance)
            {
                return Response(
                    new SlotSpinResponse
                    {
                        Success =
                            false,
                        Message =
                            $"CHIP BALANCE OUT OF SYNC ({balance} != {info.ExpectedPostWagerBalance})",
                        Balance =
                            balance,
                        JackpotAmount =
                            jackpotService
                                .GetState()
                                .Amount
                    });
            }

            string[][] symbols =
                GenerateSymbols(
                    false);

            WinResult win =
                Evaluate(
                    symbols,
                    info.Bet,
                    info.JackpotEnabled);

            string winnerName =
                pmc.Info?.Nickname
                ?? info.ProfileId
                ?? "Unknown";

            jackpotService.ApplySpin(
                info.Bet,
                win.Jackpot &&
                info.JackpotEnabled,
                winnerName,
                out int jackpotPayout,
                out int jackpotAmount);

            int payout =
                checked(
                    win.Amount +
                    jackpotPayout);

            int expectedFinalBalance =
                checked(
                    balance +
                    payout);

            casinoStatsService.RecordSlotSpin(
                sessionId.ToString(),
                info.Bet,
                win.Amount,
                jackpotPayout);

            return Response(
                new SlotSpinResponse
                {
                    Success =
                        true,
                    Message =
                        jackpotPayout > 0
                            ? $"JACKPOT {jackpotPayout} CHIPS"
                            : (win.Amount > 0
                                ? $"WIN {win.Amount} CHIPS"
                                : "NO WIN"),
                    Balance =
                        expectedFinalBalance,
                    Win =
                        win.Amount,
                    WinningPayline =
                        win.LineWins.Length > 0
                            ? win.LineWins[0].Payline
                            : -1,
                    Symbols =
                        symbols,
                    WinningCells =
                        win.Cells,
                    LineWins =
                        win.LineWins,
                    Jackpot =
                        win.Jackpot,
                    OddsProfile =
                        "RELEASE",
                    JackpotAmount =
                        jackpotAmount,
                    JackpotPayout =
                        jackpotPayout
                });
        }
        catch (Exception ex)
        {
            logger.Error(
                $"Slot spin route failed: {ex}");

            return Response(
                new SlotSpinResponse
                {
                    Success =
                        false,
                    Message =
                        "SERVER SLOT ERROR"
                });
        }
    }

    public ValueTask<string> HandleCasinoConfig(
        CasinoConfigRequest info,
        MongoId sessionId)
    {
        CasinoServerConfig config =
            casinoServerConfigService.Get();

        return new ValueTask<string>(
            jsonUtil.Serialize(
                new CasinoConfigResponse
                {
                    BuyInCostRoubles =
                        config.BuyInCostRoubles,
                    BlackjackMinBet =
                        config.BlackjackMinBet,
                    BlackjackMaxBet =
                        config.BlackjackMaxBet,
                    BlackjackDiagnostics =
                        config.BlackjackDiagnostics
                })
            ?? string.Empty);
    }

    public ValueTask<string> HandleBuyIn(
        CasinoBuyInRequest info,
        MongoId sessionId)
    {
        int costRoubles =
            casinoServerConfigService
                .Get()
                .BuyInCostRoubles;

        const int chipPurchase =
            5;

        try
        {
            var pmc =
                profileHelper.GetPmcProfile(
                    sessionId);

            if (pmc?.Inventory?.Items is null)
            {
                return new ValueTask<string>(
                    jsonUtil.Serialize(
                        new CasinoBuyInResponse
                        {
                            Success =
                                false,
                            Message =
                                "PMC PROFILE NOT AVAILABLE"
                        })
                    ?? string.Empty);
            }

            int rubBalance =
                currencyService.GetBalance(
                    pmc,
                    CasinoCurrencies.Roubles);

            int chipBalance =
                currencyService.GetBalance(
                    pmc,
                    CasinoCurrencies.Gp);

            if (chipBalance >=
                chipPurchase)
            {
                return new ValueTask<string>(
                    jsonUtil.Serialize(
                        new CasinoBuyInResponse
                        {
                            Success =
                                false,
                            Message =
                                "BUY-IN ONLY AVAILABLE BELOW 5 CHIPS",
                            GpBalance =
                                chipBalance,
                            RoubleBalance =
                                rubBalance
                        })
                    ?? string.Empty);
            }

            if (rubBalance <
                costRoubles)
            {
                return new ValueTask<string>(
                    jsonUtil.Serialize(
                        new CasinoBuyInResponse
                        {
                            Success =
                                false,
                            Message =
                                "NOT ENOUGH ROUBLES",
                            GpBalance =
                                chipBalance,
                            RoubleBalance =
                                rubBalance
                        })
                    ?? string.Empty);
            }

            // Authorization only. The live EFT client performs the actual
            // Rouble spend and Casino Chip add through
            // InventoryController.TryRunNetworkTransaction().
            return new ValueTask<string>(
                jsonUtil.Serialize(
                    new CasinoBuyInResponse
                    {
                        Success =
                            true,
                        Message =
                            $"BUY-IN APPROVED: 5 CHIPS FOR ₽{costRoubles:N0}",
                        GpBalance =
                            chipBalance + chipPurchase,
                        RoubleBalance =
                            rubBalance - costRoubles
                    })
                ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.Error(
                $"Casino buy-in validation failed: {ex}");

            return new ValueTask<string>(
                jsonUtil.Serialize(
                    new CasinoBuyInResponse
                    {
                        Success =
                            false,
                        Message =
                            "BUY-IN FAILED"
                    })
                ?? string.Empty);
        }
    }

    public ValueTask<string> HandleCasinoStats(
        CasinoStatsRequest info,
        MongoId sessionId)
    {
        string profileId =
            string.IsNullOrWhiteSpace(info.ProfileId)
                ? sessionId.ToString()
                : info.ProfileId;

        return new ValueTask<string>(
            jsonUtil.Serialize(casinoStatsService.Get(profileId))
            ?? string.Empty);
    }

    public ValueTask<string> HandleJackpotState(
        string url,
        JackpotStateRequest info,
        MongoId sessionId)
    {
        JackpotState state = jackpotService.GetState();

        return new ValueTask<string>(
            jsonUtil.Serialize(
                new JackpotStateResponse
                {
                    Amount = state.Amount,
                    BaseAmount = state.BaseAmount,
                    LastWinner = state.LastWinner,
                    LastWinAmount = state.LastWinAmount
                }) ?? string.Empty);
    }

    public ValueTask<string> HandleBlackjackLobby(
        string url,
        BlackjackLobbyRequest info,
        MongoId sessionId)
    {
        return new ValueTask<string>(
            jsonUtil.Serialize(
                blackjackRoomService.GetLobby())
            ?? string.Empty);
    }

    public ValueTask<string> HandleBlackjackHost(
        string url,
        BlackjackHostRequest info,
        MongoId sessionId)
    {
        string profileId =
            string.IsNullOrWhiteSpace(
                info.ProfileId)
                ? sessionId.ToString()
                : info.ProfileId;

        string displayName =
            string.IsNullOrWhiteSpace(
                info.DisplayName)
                ? profileId
                : info.DisplayName;

        BlackjackRoomState room =
            blackjackRoomService.Host(
                profileId,
                displayName);

        return new ValueTask<string>(
            jsonUtil.Serialize(
                new BlackjackRoomActionResult
                {
                    Success = true,
                    Message = "HOSTED",
                    Room = room
                })
            ?? string.Empty);
    }

    public ValueTask<string> HandleBlackjackJoin(
        string url,
        BlackjackJoinRequest info,
        MongoId sessionId)
    {
        BlackjackRoomActionResult result =
            blackjackRoomService.Join(
                info.RoomId ?? string.Empty,
                string.IsNullOrWhiteSpace(
                    info.ProfileId)
                    ? sessionId.ToString()
                    : info.ProfileId,
                info.DisplayName
                ?? string.Empty);

        return new ValueTask<string>(
            jsonUtil.Serialize(
                result)
            ?? string.Empty);
    }

    public ValueTask<string> HandleBlackjackLeave(
        string url,
        BlackjackLeaveRequest info,
        MongoId sessionId)
    {
        BlackjackRoomActionResult result =
            blackjackRoomService.Leave(
                info.RoomId ?? string.Empty,
                string.IsNullOrWhiteSpace(
                    info.ProfileId)
                    ? sessionId.ToString()
                    : info.ProfileId);

        return new ValueTask<string>(
            jsonUtil.Serialize(
                result)
            ?? string.Empty);
    }

    private ValueTask<string> Response(
        SlotSpinResponse response)
    {
        return new ValueTask<string>(
            jsonUtil.Serialize(response) ?? string.Empty);
    }

    private static bool IsAllowedBet(int bet)
    {
        return bet is 1 or 5 or 10 or 25 or 50;
    }


    private static string[][] GenerateSymbols(
        bool testOdds)
    {
        var result =
            new string[5][];

        for (int reel = 0;
             reel < 5;
             reel++)
        {
            result[reel] =
                new string[3];

            for (int row = 0;
                 row < 3;
                 row++)
            {
                result[reel][row] =
                    RandomSymbol(
                        testOdds);
            }
        }

        if (testOdds &&
            Random.Shared.NextDouble() < 0.45)
        {
            int lineIndex =
                Random.Shared.Next(
                    0,
                    Paylines.Length);

            int[] line =
                Paylines[lineIndex];

            string forcedSymbol =
                Random.Shared.NextDouble() < 0.70
                    ? "GP"
                    : "DOGTAG";

            int startReel =
                Random.Shared.Next(
                    0,
                    3);

            int length =
                Random.Shared.NextDouble() < 0.25
                    ? Math.Min(
                        4,
                        5 - startReel)
                    : 3;

            for (int reel = startReel;
                 reel < startReel + length;
                 reel++)
            {
                result[reel][line[reel]] =
                    forcedSymbol;
            }
        }

        return result;
    }

    private static string RandomSymbol(
        bool testOdds)
    {
        int roll =
            Random.Shared.Next(
                0,
                10000);

        if (testOdds)
        {
            if (roll < 4200) return "GP";
            if (roll < 6700) return "DOGTAG";
            if (roll < 8000) return "SKULL";
            if (roll < 8800) return "ROUBLES";
            if (roll < 9300) return "GOLDSTAR";
            if (roll < 9600) return "LABS";
            if (roll < 9800) return "LEDX";
            if (roll < 9900) return "BTC";
            return "RR";
        }

        // RELEASE WEIGHTS (10,000 total)
        // GP 18%, Dogtag 16%, Skull 14%, Roubles 13%, Golden Star 11%,
        // Labs 9%, LEDX 7%, BTC 5%, Red Rebel 4%, 7 Jackpot 3%.
        if (roll < 1800) return "GP";
        if (roll < 3400) return "DOGTAG";
        if (roll < 4800) return "SKULL";
        if (roll < 6100) return "ROUBLES";
        if (roll < 7200) return "GOLDSTAR";
        if (roll < 8100) return "LABS";
        if (roll < 8800) return "LEDX";
        if (roll < 9300) return "BTC";
        if (roll < 9700) return "RR";
        return "7";
    }

    private static WinResult Evaluate(
        string[][] symbols,
        int bet,
        bool jackpotEnabled)
    {
        int totalWin = 0;

        var allCells =
            new List<SlotCell>();

        var lineWins =
            new List<SlotLineWin>();

        for (int lineIndex = 0;
             lineIndex < Paylines.Length;
             lineIndex++)
        {
            int[] line =
                Paylines[lineIndex];

            int bestLineWin = 0;
            int bestStart = -1;
            int bestLength = 0;
            string? bestSymbol = null;
            bool bestJackpot = false;

            for (int startReel = 0;
                 startReel <= 2;
                 startReel++)
            {
                string symbol =
                    symbols[startReel]
                        [line[startReel]];

                int matches = 1;

                for (int reel =
                         startReel + 1;
                     reel < 5;
                     reel++)
                {
                    if (symbols[reel]
                            [line[reel]]
                        == symbol)
                    {
                        matches++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (matches < 3)
                {
                    continue;
                }

                bool isJackpot =
                    jackpotEnabled &&
                    symbol == "7" &&
                    matches >= 5;

                int win =
                    bet *
                    GetMultiplier(
                        symbol,
                        matches,
                        jackpotEnabled);

                if (win > bestLineWin)
                {
                    bestLineWin = win;
                    bestStart = startReel;
                    bestLength = matches;
                    bestSymbol = symbol;
                    bestJackpot = isJackpot;
                }
            }

            if (bestLineWin <= 0 ||
                bestStart < 0 ||
                bestLength < 3 ||
                string.IsNullOrEmpty(bestSymbol))
            {
                continue;
            }

            totalWin += bestLineWin;

            var cells =
                new List<SlotCell>();

            for (int reel = bestStart;
                 reel < bestStart + bestLength;
                 reel++)
            {
                var cell =
                    new SlotCell
                    {
                        Reel = reel,
                        Row = line[reel]
                    };

                cells.Add(cell);
                allCells.Add(cell);
            }

            lineWins.Add(
                new SlotLineWin
                {
                    Payline = lineIndex,
                    Symbol = bestSymbol,
                    Matches = bestLength,
                    Win = bestLineWin,
                    Cells = cells.ToArray(),
                    Jackpot = bestJackpot
                });
        }

        SlotCell[] uniqueCells =
            allCells
                .GroupBy(
                    x => $"{x.Reel}:{x.Row}")
                .Select(
                    x => x.First())
                .ToArray();

        return new WinResult(
            totalWin,
            uniqueCells,
            lineWins.ToArray(),
            lineWins.Any(x => x.Jackpot));
    }

    private static int GetMultiplier(
        string symbol,
        int matches,
        bool jackpotEnabled)
    {
        // Release payout table. Multipliers are applied to the selected bet.
        // With the release symbol weights and five paylines this models at
        // approximately 95-96% long-run RTP in offline simulation.
        int value =
            symbol switch
            {
                "GP" => 2,
                "DOGTAG" => 2,
                "SKULL" => 3,
                "ROUBLES" => 4,
                "GOLDSTAR" => 5,
                "LABS" => 7,
                "LEDX" => 10,
                "BTC" => 15,
                "RR" => 25,
                "7" => 45,
                _ => 0
            };

        if (symbol == "7" &&
            jackpotEnabled &&
            matches >= 5)
        {
            return 750;
        }

        if (matches == 4)
        {
            value *= 3;
        }
        else if (matches >= 5)
        {
            value *= 10;
        }

        return value;
    }

    private sealed record WinResult(
        int Amount,
        SlotCell[] Cells,
        SlotLineWin[] LineWins,
        bool Jackpot);
}

public record BlackjackLobbyRequest : IRequestData
{
}

public record BlackjackHostRequest : IRequestData
{
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}

public record BlackjackJoinRequest : IRequestData
{
    [JsonPropertyName("roomId")]
    public string? RoomId { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}

public record BlackjackLeaveRequest : IRequestData
{
    [JsonPropertyName("roomId")]
    public string? RoomId { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }
}

public record CasinoConfigRequest : IRequestData
{
}

public class CasinoConfigResponse
{
    public int BuyInCostRoubles { get; set; } =
        10000;

    public int BlackjackMinBet { get; set; } =
        1000;

    public int BlackjackMaxBet { get; set; } =
        50000;

    public bool BlackjackDiagnostics { get; set; }
}

public record CasinoBuyInRequest : IRequestData
{
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("gpStackMax")]
    public int GpStackMax { get; init; } =
        1;

    [JsonPropertyName("roubleStackMax")]
    public int RoubleStackMax { get; init; } =
        1;
}

public class CasinoBuyInResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } =
        string.Empty;

    public int GpBalance { get; set; }

    public int RoubleBalance { get; set; }
}

public record CasinoStatsRequest : IRequestData
{
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }
}

public record JackpotStateRequest : IRequestData
{
}

public record JackpotStateResponse
{
    [JsonPropertyName("amount")]
    public int Amount { get; init; }

    [JsonPropertyName("baseAmount")]
    public int BaseAmount { get; init; }

    [JsonPropertyName("lastWinner")]
    public string? LastWinner { get; init; }

    [JsonPropertyName("lastWinAmount")]
    public int LastWinAmount { get; init; }
}

public record SlotSpinRequest : IRequestData
{
    [JsonPropertyName("bet")]
    public int Bet { get; init; }

    // The server route uses the normal SPT sessionId supplied by the router.
    // This field is retained for diagnostics/client compatibility.
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("testOdds")]
    public bool TestOdds { get; init; }

    [JsonPropertyName("jackpotEnabled")]
    public bool JackpotEnabled { get; init; } = true;

    [JsonPropertyName("currencyStackMax")]
    public int CurrencyStackMax { get; init; } = 1;

    [JsonPropertyName("expectedPostWagerBalance")]
    public int ExpectedPostWagerBalance { get; init; }
}


public record SlotSpinResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("balance")]
    public int Balance { get; init; }
[JsonPropertyName("win")]
    public int Win { get; init; }

    [JsonPropertyName("winningPayline")]
    public int WinningPayline { get; init; } = -1;

    [JsonPropertyName("symbols")]
    public string[][]? Symbols { get; init; }

    [JsonPropertyName("winningCells")]
    public SlotCell[]? WinningCells { get; init; }

    [JsonPropertyName("lineWins")]
    public SlotLineWin[]? LineWins { get; init; }

    [JsonPropertyName("jackpot")]
    public bool Jackpot { get; init; }

    [JsonPropertyName("oddsProfile")]
    public string? OddsProfile { get; init; }

    [JsonPropertyName("jackpotAmount")]
    public int JackpotAmount { get; init; }

    [JsonPropertyName("jackpotPayout")]
    public int JackpotPayout { get; init; }
}

public record SlotLineWin
{
    [JsonPropertyName("payline")]
    public int Payline { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("matches")]
    public int Matches { get; init; }

    [JsonPropertyName("win")]
    public int Win { get; init; }

    [JsonPropertyName("cells")]
    public SlotCell[]? Cells { get; init; }

    [JsonPropertyName("jackpot")]
    public bool Jackpot { get; init; }
}

public record SlotCell
{
    [JsonPropertyName("reel")]
    public int Reel { get; init; }

    [JsonPropertyName("row")]
    public int Row { get; init; }
}
