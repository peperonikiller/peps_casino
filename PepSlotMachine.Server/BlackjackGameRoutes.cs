using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Spt.Inventory;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace PepSlotMachine.Server;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class BlackjackGameRouter(
    JsonUtil jsonUtil,
    BlackjackGameCallback callback)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<BjRoomRequest>(
                "/pep-casino/blackjack/room",
                async (u,i,s,o,c)=>await callback.Room(i,s)),
            new RouteAction<BjBetRequest>(
                "/pep-casino/blackjack/bet",
                async (u,i,s,o,c)=>await callback.Bet(i,s)),
            new RouteAction<BjInsuranceRequest>(
                "/pep-casino/blackjack/insurance",
                async (u,i,s,o,c)=>await callback.Insurance(i,s)),
            new RouteAction<BjActionRequest>(
                "/pep-casino/blackjack/deal",
                async (u,i,s,o,c)=>await callback.Deal(i,s)),
            new RouteAction<BjActionRequest>(
                "/pep-casino/blackjack/hit",
                async (u,i,s,o,c)=>await callback.Hit(i,s)),
            new RouteAction<BjActionRequest>(
                "/pep-casino/blackjack/stand",
                async (u,i,s,o,c)=>await callback.Stand(i,s)),
            new RouteAction<BjActionRequest>(
                "/pep-casino/blackjack/double",
                async (u,i,s,o,c)=>await callback.Double(i,s)),
            new RouteAction<BjActionRequest>(
                "/pep-casino/blackjack/split",
                async (u,i,s,o,c)=>await callback.Split(i,s)),
            new RouteAction<BjActionRequest>(
                "/pep-casino/blackjack/new-hand",
                async (u,i,s,o,c)=>await callback.NewHand(i,s))
        ])
{
}

[Injectable]
public class BlackjackGameCallback(
    JsonUtil jsonUtil,
    ProfileHelper profileHelper,
    ServerCurrencyService currencyService,
    BlackjackRoomService rooms,
    EventOutputHolder outputs)
{
    public ValueTask<string> Room(
        BjRoomRequest input,
        MongoId session)
    {
        string profileId =
            Pid(
                input.ProfileId,
                session);

        BlackjackRoomState? room =
            rooms.GetRoomForProfile(
                input.RoomId ?? string.Empty,
                profileId,
                out string? kickMessage);

        if (!string.IsNullOrEmpty(kickMessage))
        {
            return Resp(
                WithBalance(
                    new BlackjackRoomActionResult
                    {
                        Success = false,
                        Message = kickMessage
                    },
                    session));
        }

        if (room?.Phase == "DEALER")
        {
            return FinalizeIfDealer(
                input.RoomId ?? string.Empty,
                session);
        }

        return Resp(
            WithBalance(
                new BlackjackRoomActionResult
                {
                    Success = room != null,
                    Message = room == null
                        ? "ROOM NOT FOUND"
                        : "OK",
                    Room = room
                },
                session));
    }

    public ValueTask<string> Bet(
        BjBetRequest input,
        MongoId session)
    {
        return Resp(
            WithBalance(
                rooms.SetBet(
                    input.RoomId ?? string.Empty,
                    Pid(input.ProfileId, session),
                    input.Bet,
                    input.CurrencyStackMax),
                session));
    }

    public ValueTask<string> Insurance(
        BjInsuranceRequest input,
        MongoId session)
    {
        string profileId=Pid(input.ProfileId,session);
        BlackjackRoomState? room=rooms.GetRoom(input.RoomId??string.Empty);
        BlackjackPlayerState? player=room?.Players.FirstOrDefault(x=>x.ProfileId==profileId);

        if(room==null||player==null)
            return Resp(WithBalance(Fail("PLAYER NOT AT TABLE"),session));

        if(room.Phase!="INSURANCE"||room.ActiveSeat!=player.Seat)
            return Resp(WithBalance(Fail("INSURANCE NOT AVAILABLE"),session));

        if(player.InsuranceDecisionMade)
            return Resp(WithBalance(Fail("INSURANCE ALREADY DECIDED"),session));

        int cost=player.Hands.Count>0?player.Hands[0].Wager/2:0;
        int stackMax=Math.Max(1,player.CurrencyStackMax);

        if(input.Take&&cost>0)
        {
            if(GetBalance(session)<cost)
                return Resp(WithBalance(Fail("NOT ENOUGH ROUBLES FOR INSURANCE"),session));

            if(!ChangeMoney(session,-cost,stackMax,out string err))
                return Resp(WithBalance(Fail(err),session));

            player.InsuranceWager=cost;
        }

        BlackjackRoomActionResult result=
            rooms.Insurance(input.RoomId??string.Empty,profileId,input.Take);

        if(!result.Success&&input.Take&&cost>0)
            ChangeMoney(session,cost,stackMax,out _);

        return ResolveIfNeeded(
            new BjActionRequest
            {
                RoomId=input.RoomId,
                ProfileId=input.ProfileId,
                CurrencyStackMax=stackMax
            },
            session,
            result);
    }

    public ValueTask<string> Deal(
        BjActionRequest input,
        MongoId session)
    {
        string profileId =
            Pid(
                input.ProfileId,
                session);

        BlackjackRoomState? room =
            rooms.GetRoom(
                input.RoomId ?? string.Empty);

        BlackjackPlayerState? actor =
            room?.Players.FirstOrDefault(
                x => x.ProfileId == profileId);

        if (actor == null)
            return Resp(Fail("PLAYER NOT AT TABLE"));

        if (!actor.IsHost)
            return Resp(Fail("ONLY THE HOST CAN DEAL"));

        if (room!.Phase is not "WAITING" and not "RESOLVED")
            return Resp(Fail("HAND ALREADY IN PROGRESS"));

        BlackjackPlayerState[] active =
            room.Players
                .Where(x => x.Ready && x.Wager > 0)
                .ToArray();

        if (active.Length == 0)
            return Resp(Fail("NO ACTIVE BETS"));

        foreach (BlackjackPlayerState player in active)
        {
            if (!TrySession(
                    player.ProfileId,
                    out MongoId playerSession))
            {
                return Resp(
                    Fail(
                        $"INVALID PROFILE: {player.DisplayName}"));
            }

            if (GetBalance(playerSession) <
                player.Wager)
            {
                return Resp(
                    Fail(
                        $"{player.DisplayName} NEEDS MORE ROUBLES"));
            }
        }

        List<(MongoId Session, int Amount, int StackMax)> charged =
            [];

        foreach (BlackjackPlayerState player in active)
        {
            TrySession(
                player.ProfileId,
                out MongoId playerSession);

            int stackMax =
                Math.Max(
                    1,
                    player.CurrencyStackMax);

            if (!ChangeMoney(
                    playerSession,
                    -player.Wager,
                    stackMax,
                    out string error))
            {
                // Do not leave earlier players charged if a later player's
                // inventory transaction fails before the hand is dealt.
                foreach (var charge in
                         charged.AsEnumerable().Reverse())
                {
                    ChangeMoney(
                        charge.Session,
                        charge.Amount,
                        charge.StackMax,
                        out _);
                }

                return Resp(
                    Fail(error));
            }

            charged.Add(
                (
                    playerSession,
                    player.Wager,
                    stackMax
                ));
        }

        return ResolveIfNeeded(
            input,
            session,
            rooms.Deal(
                input.RoomId ?? string.Empty,
                profileId));
    }

    public ValueTask<string> Hit(
        BjActionRequest input,
        MongoId session)
    {
        return ResolveIfNeeded(
            input,
            session,
            rooms.Hit(
                input.RoomId ?? string.Empty,
                Pid(input.ProfileId, session)));
    }

    public ValueTask<string> Stand(
        BjActionRequest input,
        MongoId session)
    {
        return ResolveIfNeeded(
            input,
            session,
            rooms.Stand(
                input.RoomId ?? string.Empty,
                Pid(input.ProfileId, session)));
    }

    public ValueTask<string> Double(
        BjActionRequest input,
        MongoId session)
    {
        string profileId =
            Pid(
                input.ProfileId,
                session);

        if (!TryGetActiveHand(
                input.RoomId ?? string.Empty,
                profileId,
                out BlackjackPlayerState? player,
                out BlackjackHandState? hand,
                out string error))
        {
            return Resp(
                WithBalance(
                    Fail(error),
                    session));
        }

        if (hand!.Cards.Count != 2)
            return Resp(WithBalance(Fail("DOUBLE ONLY ON FIRST TWO CARDS"), session));

        if (hand.IsSplitAce)
            return Resp(WithBalance(Fail("CANNOT DOUBLE SPLIT ACES"), session));

        int cost =
            hand.Wager;

        if (GetBalance(session) < cost)
            return Resp(WithBalance(Fail("NOT ENOUGH ROUBLES TO DOUBLE"), session));

        int stackMax =
            Math.Max(
                1,
                player!.CurrencyStackMax);

        if (!ChangeMoney(
                session,
                -cost,
                stackMax,
                out string currencyError))
        {
            return Resp(
                WithBalance(
                    Fail(currencyError),
                    session));
        }

        BlackjackRoomActionResult result =
            rooms.Double(
                input.RoomId ?? string.Empty,
                profileId);

        if (!result.Success)
        {
            ChangeMoney(
                session,
                cost,
                stackMax,
                out _);

            return Resp(
                WithBalance(
                    result,
                    session));
        }

        return ResolveIfNeeded(
            input,
            session,
            result);
    }

    public ValueTask<string> Split(
        BjActionRequest input,
        MongoId session)
    {
        string profileId =
            Pid(
                input.ProfileId,
                session);

        if (!TryGetActiveHand(
                input.RoomId ?? string.Empty,
                profileId,
                out BlackjackPlayerState? player,
                out BlackjackHandState? hand,
                out string error))
        {
            return Resp(
                WithBalance(
                    Fail(error),
                    session));
        }

        if (player!.Hands.Count >= 4 ||
            hand!.Cards.Count != 2 ||
            hand.TurnComplete ||
            hand.IsSplitAce ||
            hand.Cards[0].Rank != hand.Cards[1].Rank)
        {
            return Resp(
                WithBalance(
                    Fail("HAND CANNOT BE SPLIT"),
                    session));
        }

        int cost =
            hand.Wager;

        if (GetBalance(session) < cost)
        {
            return Resp(
                WithBalance(
                    Fail("NOT ENOUGH ROUBLES TO SPLIT"),
                    session));
        }

        int stackMax =
            Math.Max(
                1,
                player.CurrencyStackMax);

        if (!ChangeMoney(
                session,
                -cost,
                stackMax,
                out string currencyError))
        {
            return Resp(
                WithBalance(
                    Fail(currencyError),
                    session));
        }

        BlackjackRoomActionResult result =
            rooms.Split(
                input.RoomId ?? string.Empty,
                profileId);

        if (!result.Success)
        {
            ChangeMoney(
                session,
                cost,
                stackMax,
                out _);

            return Resp(
                WithBalance(
                    result,
                    session));
        }

        return ResolveIfNeeded(
            input,
            session,
            result);
    }

    public ValueTask<string> NewHand(
        BjActionRequest input,
        MongoId session)
    {
        return Resp(
            WithBalance(
                rooms.NewHand(
                    input.RoomId ?? string.Empty,
                    Pid(input.ProfileId, session)),
                session));
    }

    private ValueTask<string> ResolveIfNeeded(
        BjActionRequest input,
        MongoId session,
        BlackjackRoomActionResult result)
    {
        if (!result.Success ||
            result.Room?.Phase != "DEALER")
        {
            return Resp(
                WithBalance(
                    result,
                    session));
        }

        return FinalizeIfDealer(
            input.RoomId ?? string.Empty,
            session);
    }

    private ValueTask<string> FinalizeIfDealer(
        string roomId,
        MongoId callerSession)
    {
        BlackjackRoomActionResult resolved =
            rooms.Resolve(
                roomId);

        // ClaimSettlement() is atomic inside BlackjackRoomService. Only the
        // first request that sees a newly RESOLVED hand receives a settlement
        // snapshot. Retries/polls cannot apply the same payout twice.
        BlackjackRoomState? settlement =
            rooms.ClaimSettlement(
                roomId);

        if (settlement != null)
        {
            List<(MongoId Session, int Amount, int StackMax)> paid =
                [];

            string settlementError =
                string.Empty;

            foreach (BlackjackPlayerState player in
                     settlement.Players.Where(
                         x => x.Ready && x.Payout > 0))
            {
                if (!TrySession(
                        player.ProfileId,
                        out MongoId playerSession))
                {
                    settlementError =
                        $"INVALID PAYOUT PROFILE: {player.DisplayName}";

                    break;
                }

                int stackMax =
                    Math.Max(
                        1,
                        player.CurrencyStackMax);

                if (!ChangeMoney(
                        playerSession,
                        player.Payout,
                        stackMax,
                        out string payoutError))
                {
                    settlementError =
                        string.IsNullOrWhiteSpace(
                            payoutError)
                            ? $"PAYOUT FAILED: {player.DisplayName}"
                            : payoutError;

                    break;
                }

                paid.Add(
                    (
                        playerSession,
                        player.Payout,
                        stackMax
                    ));
            }

            if (!string.IsNullOrEmpty(
                    settlementError))
            {
                bool rollbackOk =
                    true;

                foreach (var payment in
                         paid.AsEnumerable().Reverse())
                {
                    if (!ChangeMoney(
                            payment.Session,
                            -payment.Amount,
                            payment.StackMax,
                            out _))
                    {
                        rollbackOk =
                            false;
                    }
                }

                if (rollbackOk)
                {
                    rooms.ReleaseSettlementClaim(
                        roomId);
                }

                resolved.Success =
                    false;

                resolved.Message =
                    rollbackOk
                        ? $"SETTLEMENT FAILED - SAFE TO RETRY: {settlementError}"
                        : $"SETTLEMENT FAILED - MANUAL REVIEW REQUIRED: {settlementError}";
            }

            resolved.Room =
                settlement;
        }

        return Resp(
            WithBalance(
                resolved,
                callerSession));
    }

    private bool TryGetActiveHand(
        string roomId,
        string profileId,
        out BlackjackPlayerState? player,
        out BlackjackHandState? hand,
        out string error)
    {
        player =
            null;

        hand =
            null;

        error =
            string.Empty;

        BlackjackRoomState? room =
            rooms.GetRoom(
                roomId);

        if (room == null)
        {
            error =
                "ROOM NOT FOUND";

            return false;
        }

        if (room.Phase != "PLAYER")
        {
            error =
                "NOT PLAYER TURN PHASE";

            return false;
        }

        player =
            room.Players.FirstOrDefault(
                x => x.ProfileId == profileId);

        if (player == null)
        {
            error =
                "PLAYER NOT AT TABLE";

            return false;
        }

        if (room.ActiveSeat != player.Seat)
        {
            error =
                "NOT YOUR TURN";

            return false;
        }

        if (player.ActiveHandIndex < 0 ||
            player.ActiveHandIndex >= player.Hands.Count)
        {
            error =
                "NO ACTIVE HAND";

            return false;
        }

        hand =
            player.Hands[
                player.ActiveHandIndex];

        if (hand.TurnComplete)
        {
            error =
                "HAND ALREADY COMPLETE";

            return false;
        }

        return true;
    }

    private BlackjackRoomActionResult WithBalance(
        BlackjackRoomActionResult result,
        MongoId session)
    {
        result.Balance =
            GetBalance(
                session);

        return result;
    }

    private int GetBalance(
        MongoId session)
    {
        var pmc =
            profileHelper.GetPmcProfile(
                session);

        if (pmc?.Inventory?.Items is null)
            return 0;

        return currencyService.GetBalance(
            pmc,
            CasinoCurrencies.Roubles);
    }

    private static bool TrySession(
        string profileId,
        out MongoId session)
    {
        try
        {
            session =
                new MongoId(
                    profileId);

            return true;
        }
        catch
        {
            session =
                default;

            return false;
        }
    }

    private bool ChangeMoney(
        MongoId session,
        int delta,
        int stackMax,
        out string error)
    {
        error =
            string.Empty;

        var pmc =
            profileHelper.GetPmcProfile(
                session);

        if (pmc?.Inventory?.Items is null)
        {
            error =
                "PROFILE UNAVAILABLE";

            return false;
        }

        ItemEventRouterResponse output =
            outputs.GetOutput(
                session);

        return currencyService.TryAdjustBalance(
            pmc,
            session,
            output,
            CasinoCurrencies.Roubles,
            delta,
            Math.Max(1, stackMax),
            out _,
            out error);
    }

    private static string Pid(
        string? profileId,
        MongoId session)
    {
        return string.IsNullOrWhiteSpace(profileId)
            ? session.ToString()
            : profileId;
    }

    private static BlackjackRoomActionResult Fail(
        string message)
    {
        return new BlackjackRoomActionResult
        {
            Success =
                false,
            Message =
                message
        };
    }

    private ValueTask<string> Resp(
        BlackjackRoomActionResult result)
    {
        return new ValueTask<string>(
            jsonUtil.Serialize(result)
            ?? string.Empty);
    }
}

public record BjRoomRequest : IRequestData
{
    [JsonPropertyName("roomId")]
    public string? RoomId { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }
}

public record BjBetRequest : IRequestData
{
    [JsonPropertyName("roomId")]
    public string? RoomId { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("bet")]
    public int Bet { get; init; }

    [JsonPropertyName("currencyStackMax")]
    public int CurrencyStackMax { get; init; } =
        1;
}

public record BjInsuranceRequest : IRequestData
{
    [JsonPropertyName("roomId")]
    public string? RoomId { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("take")]
    public bool Take { get; init; }
}

public record BjActionRequest : IRequestData
{
    [JsonPropertyName("roomId")]
    public string? RoomId { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }

    [JsonPropertyName("currencyStackMax")]
    public int CurrencyStackMax { get; init; } =
        1;
}
