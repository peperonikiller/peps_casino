using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace PepSlotMachine.Server;

[Injectable]
public class BlackjackRoomService(
    ISptLogger<BlackjackRoomService> logger,
    CasinoStatsService casinoStatsService,
    CasinoServerConfigService casinoServerConfigService)
{
    private static readonly object _sync = new();

    private static readonly Dictionary<string, BlackjackRoomState> _rooms =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, RecentKick> _recentKicks =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MaxPlayers = 5;
    private const int MaxHandsPerPlayer = 4;

    private static readonly TimeSpan TurnLimit =
        TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ResultDisplayTime =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan KickNoticeLifetime =
        TimeSpan.FromMinutes(2);

    private void Diag(
        string message)
    {
        if (casinoServerConfigService
            .Get()
            .BlackjackDiagnostics)
        {
            logger.Info(
                $"[PepCasino Blackjack] {message}");
        }
    }

    private static void MarkState(
        BlackjackRoomState room,
        string notice = "")
    {
        room.StateRevision++;

        if (!string.IsNullOrWhiteSpace(
                notice))
        {
            room.LastNotice =
                notice;
        }

        room.LastActivityUtc =
            DateTime.UtcNow;
    }

    public BlackjackLobbyState GetLobby()
    {
        lock (_sync)
        {
            CleanupExpired();

            foreach (BlackjackRoomState room in _rooms.Values.ToArray())
                ProcessTurnTimeout(room);

            return new BlackjackLobbyState
            {
                Rooms = _rooms.Values
                    .OrderByDescending(x => x.CreatedUtc)
                    .Select(CloneRoom)
                    .ToArray()
            };
        }
    }

    public BlackjackRoomState Host(
        string profileId,
        string displayName)
    {
        lock (_sync)
        {
            CleanupExpired();

            BlackjackRoomState? existing =
                _rooms.Values.FirstOrDefault(
                    x => x.Players.Any(
                        p => p.ProfileId == profileId));

            if (existing != null)
                return CloneRoom(existing);

            string id =
                Guid.NewGuid()
                    .ToString("N")[..8]
                    .ToUpperInvariant();

            BlackjackRoomState room =
                new()
                {
                    RoomId = id,
                    HostProfileId = profileId,
                    HostName = Name(displayName, profileId),
                    CreatedUtc = DateTime.UtcNow,
                    LastActivityUtc = DateTime.UtcNow,
                    Phase = "WAITING",
                    Message = "PLACE YOUR BETS",
                    StateRevision = 1,
                    LastNotice = "TABLE CREATED"
                };

            room.Players.Add(
                NewPlayer(
                    profileId,
                    displayName,
                    true,
                    0));

            _rooms[id] =
                room;

            Diag(
                $"HOST room={room.RoomId} host={room.HostName} profile={profileId}");

            return CloneRoom(room);
        }
    }

    public BlackjackRoomActionResult Join(
        string roomId,
        string profileId,
        string displayName)
    {
        lock (_sync)
        {
            CleanupExpired();

            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            ProcessTurnTimeout(room);

            BlackjackPlayerState? existing =
                room.Players.FirstOrDefault(
                    x => x.ProfileId == profileId);

            if (existing != null)
                return Ok("ALREADY JOINED", room);

            if (room.Players.Count >= MaxPlayers)
                return Fail("TABLE FULL");

            int seat =
                Enumerable.Range(0, MaxPlayers)
                    .First(
                        n => room.Players.All(
                            x => x.Seat != n));

            room.Players.Add(
                NewPlayer(
                    profileId,
                    displayName,
                    false,
                    seat));

            bool joinsNextHand =
                room.Phase is
                    "PLAYER" or
                    "INSURANCE" or
                    "DEALER";

            string joinedName =
                Name(
                    displayName,
                    profileId);

            MarkState(
                room,
                joinsNextHand
                    ? $"{joinedName} JOINED - NEXT HAND"
                    : $"{joinedName} JOINED TABLE");

            Diag(
                $"JOIN room={room.RoomId} player={joinedName} seat={seat} phase={room.Phase} nextHand={joinsNextHand}");

            return Ok(
                joinsNextHand
                    ? "JOINED - ENTERS NEXT HAND"
                    : "JOINED",
                room);
        }
    }

    public BlackjackRoomActionResult Leave(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            BlackjackPlayerState? player =
                room.Players.FirstOrDefault(
                    x => x.ProfileId == profileId);

            if (player == null)
                return new BlackjackRoomActionResult
                {
                    Success = true,
                    Message = "NOT IN ROOM"
                };

            string leavingName =
                player.DisplayName;

            bool leavingWasHost =
                player.IsHost;

            RemovePlayer(
                room,
                player,
                kicked: false,
                "LEFT TABLE");

            Diag(
                $"LEAVE room={roomId} player={leavingName} wasHost={leavingWasHost}");

            if (!_rooms.ContainsKey(roomId))
                return new BlackjackRoomActionResult
                {
                    Success = true,
                    Message = "ROOM CLOSED"
                };

            return Ok("LEFT", room);
        }
    }

    public BlackjackRoomState? GetRoom(
        string roomId)
    {
        lock (_sync)
        {
            CleanupExpired();

            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return null;

            ProcessTurnTimeout(room);

            if (!_rooms.ContainsKey(roomId))
                return null;

            if (room.Phase == "RESOLVED" &&
                room.ResolvedUtc.HasValue &&
                DateTime.UtcNow - room.ResolvedUtc.Value >= ResultDisplayTime)
            {
                PrepareNextHand(room);
            }

            return CloneRoom(room);
        }
    }

    public BlackjackRoomState? GetRoomForProfile(
        string roomId,
        string profileId,
        out string? kickMessage)
    {
        lock (_sync)
        {
            kickMessage =
                null;

            BlackjackRoomState? room =
                GetRoom(roomId);

            string key =
                KickKey(
                    roomId,
                    profileId);

            if (_recentKicks.TryGetValue(key, out RecentKick? kick))
            {
                if (DateTime.UtcNow <= kick.ExpiresUtc)
                {
                    kickMessage =
                        kick.Message;

                    _recentKicks.Remove(key);

                    return room;
                }

                _recentKicks.Remove(key);
            }

            return room;
        }
    }

    public BlackjackRoomActionResult SetBet(
        string roomId,
        string profileId,
        int bet,
        int currencyStackMax)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            ProcessTurnTimeout(room);

            if (room.Phase is not "WAITING" and not "RESOLVED")
                return Fail("HAND IN PROGRESS");

            BlackjackPlayerState? player =
                room.Players.FirstOrDefault(
                    x => x.ProfileId == profileId);

            if (player == null)
                return Fail("PLAYER NOT AT TABLE");

            CasinoServerConfig tableConfig =
                casinoServerConfigService.Get();

            if (bet <
                tableConfig.BlackjackMinBet)
            {
                return Fail(
                    $"MINIMUM BET IS ₽{tableConfig.BlackjackMinBet:N0}");
            }

            if (bet >
                tableConfig.BlackjackMaxBet)
            {
                return Fail(
                    $"MAXIMUM BET IS ₽{tableConfig.BlackjackMaxBet:N0}");
            }

            ResetPlayerRound(player);

            player.Wager =
                bet;

            player.LastBaseBet =
                bet;

            player.CurrencyStackMax =
                Math.Max(
                    1,
                    currencyStackMax);

            player.Ready =
                true;

            room.Phase =
                "WAITING";

            room.Message =
                $"BET ₽{bet:N0}";

            MarkState(
                room,
                $"{player.DisplayName} READY - ₽{bet:N0}");

            Diag(
                $"BET room={room.RoomId} player={player.DisplayName} wager={bet} revision={room.StateRevision}");

            return Ok(
                "BET ACCEPTED",
                room);
        }
    }

    public BlackjackRoomActionResult Deal(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            ProcessTurnTimeout(room);

            BlackjackPlayerState? actor =
                room.Players.FirstOrDefault(
                    x => x.ProfileId == profileId);

            if (actor == null)
                return Fail("PLAYER NOT AT TABLE");

            if (!actor.IsHost)
                return Fail("ONLY THE HOST CAN DEAL");

            if (room.Phase is not "WAITING" and not "RESOLVED")
                return Fail("HAND ALREADY IN PROGRESS");

            List<BlackjackPlayerState> active =
                room.Players
                    .Where(x => x.Ready && x.Wager > 0)
                    .OrderBy(x => x.Seat)
                    .ToList();

            if (active.Count == 0)
                return Fail("NO ACTIVE BETS");

            room.Shoe =
                BuildShoe();

            room.SettlementApplied =
                false;

            room.SettlementId =
                Guid.NewGuid()
                    .ToString("N");

            room.DealerCards.Clear();
            room.TauntEvents.Clear();
            room.ResolvedUtc = null;
            ClearTurnTimer(room);

            foreach (BlackjackPlayerState player in room.Players)
            {
                int originalWager =
                    player.Wager;

                ResetPlayerGameplay(player);

                if (player.Ready &&
                    originalWager > 0)
                {
                    player.Hands.Add(
                        new BlackjackHandState
                        {
                            HandIndex = 0,
                            Wager = originalWager
                        });

                    player.ActiveHandIndex =
                        0;
                }
            }

            for (int round = 0;
                 round < 2;
                 round++)
            {
                foreach (BlackjackPlayerState player in active)
                {
                    player.Hands[0].Cards.Add(
                        Draw(room));
                }

                room.DealerCards.Add(
                    Draw(room));
            }

            foreach (BlackjackPlayerState player in active)
            {
                BlackjackHandState hand =
                    player.Hands[0];

                hand.Blackjack =
                    hand.Cards.Count == 2 &&
                    Value(hand.Cards) == 21;

                if (hand.Blackjack)
                {
                    hand.Standing = true;
                    hand.TurnComplete = true;
                }

                SyncPlayerLegacy(player);
            }

            bool dealerBlackjack =
                room.DealerCards.Count == 2 &&
                Value(room.DealerCards) == 21;

            bool insuranceOffered =
                room.DealerCards.Count > 0 &&
                room.DealerCards[0].Rank == "A";

            if (insuranceOffered)
            {
                foreach (BlackjackPlayerState player in active)
                {
                    player.InsuranceDecisionMade = false;
                    player.InsuranceWager = 0;
                    player.InsurancePayout = 0;
                }

                int insuranceSeat =
                    NextInsuranceSeat(room,-1);

                if (insuranceSeat >= 0)
                    SetInsuranceSeat(room,insuranceSeat);
                else
                    FinishInsurancePhase(room);
            }
            else if (dealerBlackjack)
            {
                room.Phase = "DEALER";
                room.ActiveSeat = -1;
                room.Message = "DEALER BLACKJACK";
                ClearTurnTimer(room);
            }
            else
            {
                StartPlayerPhaseOrDealer(room);
            }

            MarkState(
                room,
                $"HAND STARTED - {active.Count} ACTIVE PLAYER{(active.Count == 1 ? "" : "S")}");

            Diag(
                $"DEAL room={room.RoomId} settlement={room.SettlementId} active={active.Count} phase={room.Phase} revision={room.StateRevision}");

            return Ok(
                "DEALT",
                room);
        }
    }

    public BlackjackRoomActionResult Hit(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!TryActiveHand(
                    roomId,
                    profileId,
                    out BlackjackRoomState? room,
                    out BlackjackPlayerState? player,
                    out BlackjackHandState? hand,
                    out BlackjackRoomActionResult? error))
            {
                return error!;
            }

            hand!.Cards.Add(
                Draw(room!));

            int value =
                Value(hand.Cards);

            if (value > 21)
            {
                hand.Busted = true;
                hand.Standing = true;
                hand.TurnComplete = true;
                hand.Result = "BUST";

                AdvanceAfterHand(
                    room!,
                    player!);
            }
            else if (value == 21)
            {
                hand.Standing = true;
                hand.TurnComplete = true;
                hand.Result = "21";

                AdvanceAfterHand(
                    room!,
                    player!);
            }
            else
            {
                room!.Message =
                    $"{player!.DisplayName} - HAND {player.ActiveHandIndex + 1}: {value}";

                ResetTurnTimer(room);
            }

            SyncPlayerLegacy(
                player!);

            room!.LastActivityUtc =
                DateTime.UtcNow;

            return Ok(
                hand.Busted
                    ? "BUST"
                    : "HIT",
                room);
        }
    }

    public BlackjackRoomActionResult Stand(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!TryActiveHand(
                    roomId,
                    profileId,
                    out BlackjackRoomState? room,
                    out BlackjackPlayerState? player,
                    out BlackjackHandState? hand,
                    out BlackjackRoomActionResult? error))
            {
                return error!;
            }

            hand!.Standing =
                true;

            hand.TurnComplete =
                true;

            AdvanceAfterHand(
                room!,
                player!);

            SyncPlayerLegacy(
                player!);

            room!.LastActivityUtc =
                DateTime.UtcNow;

            return Ok(
                "STAND",
                room);
        }
    }

    public BlackjackRoomActionResult Double(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!TryActiveHand(
                    roomId,
                    profileId,
                    out BlackjackRoomState? room,
                    out BlackjackPlayerState? player,
                    out BlackjackHandState? hand,
                    out BlackjackRoomActionResult? error))
            {
                return error!;
            }

            if (hand!.Cards.Count != 2)
                return Fail("DOUBLE ONLY ON FIRST TWO CARDS");

            if (hand.IsSplitAce)
                return Fail("CANNOT DOUBLE SPLIT ACES");

            hand.Wager *=
                2;

            hand.Cards.Add(
                Draw(room!));

            int value =
                Value(hand.Cards);

            hand.Busted =
                value > 21;

            hand.Standing =
                true;

            hand.TurnComplete =
                true;

            if (hand.Busted)
                hand.Result =
                    "BUST";

            AdvanceAfterHand(
                room!,
                player!);

            SyncPlayerLegacy(
                player!);

            room!.LastActivityUtc =
                DateTime.UtcNow;

            return Ok(
                "DOUBLED",
                room);
        }
    }

    public BlackjackRoomActionResult Split(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!TryActiveHand(
                    roomId,
                    profileId,
                    out BlackjackRoomState? room,
                    out BlackjackPlayerState? player,
                    out BlackjackHandState? hand,
                    out BlackjackRoomActionResult? error))
            {
                return error!;
            }

            if (!CanSplitHand(
                    player!,
                    hand!))
            {
                return Fail("HAND CANNOT BE SPLIT");
            }

            bool splitAces =
                hand!.Cards[0].Rank ==
                "A";

            BlackjackCard firstCard =
                hand.Cards[0];

            BlackjackCard secondCard =
                hand.Cards[1];

            int wager =
                hand.Wager;

            int activeIndex =
                player!.ActiveHandIndex;

            BlackjackHandState first =
                new()
                {
                    Wager = wager,
                    IsSplitHand = true,
                    IsSplitAce = splitAces
                };

            first.Cards.Add(
                firstCard);

            first.Cards.Add(
                Draw(room!));

            BlackjackHandState second =
                new()
                {
                    Wager = wager,
                    IsSplitHand = true,
                    IsSplitAce = splitAces
                };

            second.Cards.Add(
                secondCard);

            second.Cards.Add(
                Draw(room!));

            player.Hands[activeIndex] =
                first;

            player.Hands.Insert(
                activeIndex + 1,
                second);

            ReindexHands(
                player);

            if (splitAces)
            {
                first.Standing = true;
                first.TurnComplete = true;
                second.Standing = true;
                second.TurnComplete = true;

                AdvanceAfterHand(
                    room!,
                    player);
            }
            else
            {
                CompleteTwentyOneIfNeeded(
                    first);

                if (first.TurnComplete)
                {
                    player.ActiveHandIndex =
                        activeIndex + 1;

                    CompleteTwentyOneIfNeeded(
                        second);

                    if (second.TurnComplete)
                    {
                        AdvanceAfterHand(
                            room!,
                            player);
                    }
                    else
                    {
                        room!.Message =
                            $"{player.DisplayName} - HAND {player.ActiveHandIndex + 1}";

                        ResetTurnTimer(
                            room);
                    }
                }
                else
                {
                    room!.Message =
                        $"{player.DisplayName} - HAND {player.ActiveHandIndex + 1}";

                    ResetTurnTimer(
                        room);
                }
            }

            SyncPlayerLegacy(
                player);

            room!.LastActivityUtc =
                DateTime.UtcNow;

            return Ok(
                "SPLIT",
                room);
        }
    }

    public BlackjackRoomActionResult Insurance(
        string roomId,
        string profileId,
        bool takeInsurance)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomId,out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            ProcessTurnTimeout(room);

            if (room.Phase!="INSURANCE")
                return Fail("INSURANCE NOT AVAILABLE");

            BlackjackPlayerState? player=
                room.Players.FirstOrDefault(x=>x.ProfileId==profileId);

            if(player==null)return Fail("PLAYER NOT AT TABLE");
            if(player.Seat!=room.ActiveSeat)return Fail("NOT YOUR INSURANCE TURN");
            if(player.InsuranceDecisionMade)return Fail("INSURANCE ALREADY DECIDED");

            player.InsuranceDecisionMade=true;

            player.InsuranceWager=
                takeInsurance && player.Hands.Count>0
                    ? player.Hands[0].Wager/2
                    : 0;

            int next=NextInsuranceSeat(room,player.Seat);
            if(next>=0)SetInsuranceSeat(room,next);
            else FinishInsurancePhase(room);

            room.LastActivityUtc=DateTime.UtcNow;
            return Ok(takeInsurance?"INSURANCE TAKEN":"INSURANCE DECLINED",room);
        }
    }

    public BlackjackRoomActionResult Resolve(
        string roomId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            if (room.Phase != "DEALER")
                return Ok("WAITING", room);

            while (Value(room.DealerCards) < 17)
            {
                room.DealerCards.Add(
                    Draw(room));
            }

            int dealerValue =
                Value(room.DealerCards);

            bool dealerBust =
                dealerValue > 21;

            bool dealerBlackjack =
                dealerValue == 21 &&
                room.DealerCards.Count == 2;

            foreach (BlackjackPlayerState insured in
                     room.Players.Where(x=>x.Ready&&x.InsuranceWager>0))
            {
                insured.InsurancePayout=
                    dealerBlackjack
                        ? insured.InsuranceWager*3
                        : 0;

                casinoStatsService.RecordBlackjackInsurance(
                    insured.ProfileId,
                    insured.InsuranceWager,
                    insured.InsurancePayout);
            }

            foreach (BlackjackPlayerState player in
                     room.Players.Where(
                         x => x.Ready && x.Hands.Count > 0))
            {
                foreach (BlackjackHandState hand in player.Hands)
                {
                    ResolveHand(
                        hand,
                        dealerValue,
                        dealerBust,
                        dealerBlackjack);

                    casinoStatsService.RecordBlackjackHand(
                        player.ProfileId,
                        hand.Wager,
                        hand.Payout,
                        hand.Result);
                }

                SyncPlayerLegacy(
                    player);

                SetAggregatePlayerResult(
                    player);

                casinoStatsService.RecordBlackjackRound(
                    player.ProfileId,
                    player.Wager,
                    player.Payout,
                    player.Result,
                    player.Hands.Count);
            }

            room.TauntEvents.Clear();

            foreach (BlackjackPlayerState player in
                     room.Players.Where(
                         x => x.Ready && x.Hands.Count > 0))
            {
                int net =
                    player.Payout -
                    player.Wager;

                if (net < 0)
                {
                    room.TauntEvents.Add(
                        BuildVoiceEvent(
                            player,
                            "LOSS"));
                }
                else if (net > 0)
                {
                    room.TauntEvents.Add(
                        BuildVoiceEvent(
                            player,
                            "WIN"));
                }
            }

            room.Phase =
                "RESOLVED";

            room.ActiveSeat =
                -1;

            room.ResolvedUtc =
                DateTime.UtcNow;

            ClearTurnTimer(
                room);

            BlackjackPlayerState? solo =
                room.Players.FirstOrDefault(
                    x => x.Ready && x.Hands.Count > 0);

            if (room.Players.Count(
                    x => x.Ready && x.Hands.Count > 0) == 1 &&
                solo != null)
            {
                int net =
                    solo.Payout -
                    solo.Wager;

                if (solo.Hands.Count > 1)
                {
                    room.Message =
                        net > 0
                            ? $"SPLIT RESULT: +₽{net:N0}"
                            : net < 0
                                ? $"SPLIT RESULT: -₽{Math.Abs(net):N0}"
                                : "SPLIT RESULT: PUSH";
                }
                else
                {
                    room.Message =
                        solo.Result switch
                        {
                            "BLACKJACK" =>
                                $"BLACKJACK!  +₽{Math.Max(0, net):N0}",
                            "WIN" =>
                                $"YOU WIN  +₽{Math.Max(0, net):N0}",
                            "PUSH" =>
                                "PUSH - WAGER RETURNED",
                            _ =>
                                $"YOU LOSE  -₽{Math.Abs(Math.Min(0, net)):N0}"
                        };
                }
            }
            else
            {
                room.Message =
                    dealerBust
                        ? "DEALER BUST"
                        : dealerBlackjack
                            ? "DEALER BLACKJACK"
                            : $"DEALER {dealerValue}";
            }

            MarkState(
                room,
                $"HAND RESOLVED - SETTLEMENT {room.SettlementId[..Math.Min(8, room.SettlementId.Length)]}");

            Diag(
                $"RESOLVE room={room.RoomId} settlement={room.SettlementId} players={room.Players.Count} revision={room.StateRevision}");

            return Ok(
                "RESOLVED",
                room);
        }
    }

    public BlackjackRoomState? ClaimSettlement(
        string roomId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(
                    roomId,
                    out BlackjackRoomState? room))
            {
                return null;
            }

            if (room.Phase != "RESOLVED" ||
                room.SettlementApplied)
            {
                return null;
            }

            room.SettlementApplied =
                true;

            MarkState(
                room,
                "SETTLEMENT CLAIMED");

            Diag(
                $"SETTLEMENT room={room.RoomId} id={room.SettlementId} claimed=true revision={room.StateRevision}");

            return CloneRoom(
                room);
        }
    }

    public void ReleaseSettlementClaim(
        string roomId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(
                    roomId,
                    out BlackjackRoomState? room))
            {
                return;
            }

            if (room.Phase != "RESOLVED")
                return;

            room.SettlementApplied =
                false;

            MarkState(
                room,
                "SETTLEMENT RELEASED FOR RETRY");

            Diag(
                $"SETTLEMENT room={room.RoomId} id={room.SettlementId} released=true revision={room.StateRevision}");
        }
    }

    public BlackjackRoomActionResult NewHand(
        string roomId,
        string profileId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomId, out BlackjackRoomState? room))
                return Fail("ROOM NOT FOUND");

            BlackjackPlayerState? actor =
                room.Players.FirstOrDefault(
                    x => x.ProfileId == profileId);

            if (actor == null)
                return Fail("PLAYER NOT AT TABLE");

            if (!actor.IsHost)
                return Fail("ONLY THE HOST CAN START A NEW HAND");

            PrepareNextHand(
                room);

            return Ok(
                "READY",
                room);
        }
    }

    private bool TryActiveHand(
        string roomId,
        string profileId,
        out BlackjackRoomState? room,
        out BlackjackPlayerState? player,
        out BlackjackHandState? hand,
        out BlackjackRoomActionResult? error)
    {
        room =
            null;

        player =
            null;

        hand =
            null;

        error =
            null;

        if (!_rooms.TryGetValue(roomId, out room))
        {
            error =
                Fail("ROOM NOT FOUND");

            return false;
        }

        ProcessTurnTimeout(
            room);

        string key =
            KickKey(
                roomId,
                profileId);

        if (_recentKicks.TryGetValue(key, out RecentKick? kick) &&
            DateTime.UtcNow <= kick.ExpiresUtc)
        {
            error =
                Fail(kick.Message);

            return false;
        }

        if (room.Phase != "PLAYER")
        {
            error =
                Fail("NOT PLAYER TURN PHASE");

            return false;
        }

        player =
            room.Players.FirstOrDefault(
                x => x.ProfileId == profileId);

        if (player == null)
        {
            error =
                Fail("PLAYER NOT AT TABLE");

            return false;
        }

        if (player.Seat != room.ActiveSeat)
        {
            error =
                Fail("NOT YOUR TURN");

            return false;
        }

        if (player.ActiveHandIndex < 0 ||
            player.ActiveHandIndex >= player.Hands.Count)
        {
            error =
                Fail("NO ACTIVE HAND");

            return false;
        }

        hand =
            player.Hands[
                player.ActiveHandIndex];

        if (hand.TurnComplete)
        {
            error =
                Fail("HAND ALREADY COMPLETE");

            return false;
        }

        return true;
    }

    private void ProcessTurnTimeout(
        BlackjackRoomState room)
    {
        if ((room.Phase != "PLAYER" &&
             room.Phase != "INSURANCE") ||
            room.ActiveSeat < 0 ||
            !room.TurnDeadlineUtc.HasValue ||
            DateTime.UtcNow < room.TurnDeadlineUtc.Value)
        {
            return;
        }

        BlackjackPlayerState? player =
            room.Players.FirstOrDefault(
                x => x.Seat == room.ActiveSeat);

        if (player == null)
        {
            int next =
                NextPlayableSeat(
                    room,
                    room.ActiveSeat);

            if (next < 0)
            {
                room.Phase =
                    "DEALER";

                room.ActiveSeat =
                    -1;

                room.Message =
                    "DEALER TURN";

                ClearTurnTimer(
                    room);
            }
            else
            {
                SetActiveSeat(
                    room,
                    next);
            }

            return;
        }

        string roomId =
            room.RoomId;

        string profileId =
            player.ProfileId;

        string displayName =
            player.DisplayName;

        ForfeitCurrentRound(
            player);

        _recentKicks[
            KickKey(
                roomId,
                profileId)] =
            new RecentKick
            {
                Message =
                    "TURN TIMEOUT - KICKED FROM TABLE",
                ExpiresUtc =
                    DateTime.UtcNow +
                    KickNoticeLifetime
            };

        int oldSeat =
            player.Seat;

        bool wasHost =
            player.IsHost;

        room.Players.Remove(
            player);

        if (room.Players.Count == 0)
        {
            _rooms.Remove(
                roomId);

            return;
        }

        if (wasHost)
            PromoteHost(room);

        room.Message =
            $"{displayName} TIMED OUT - KICKED";

        MarkState(
            room,
            $"{displayName} TIMED OUT - REMOVED");

        Diag(
            $"TIMEOUT room={room.RoomId} player={displayName} seat={oldSeat} phase={room.Phase}");

        if (room.Phase == "INSURANCE")
        {
            int nextInsurance=NextInsuranceSeat(room,oldSeat);
            if(nextInsurance>=0)SetInsuranceSeat(room,nextInsurance);
            else FinishInsurancePhase(room);
        }
        else
        {
            int nextSeat=NextPlayableSeat(room,oldSeat);

            if(nextSeat<0)
            {
                room.Phase="DEALER";
                room.ActiveSeat=-1;
                ClearTurnTimer(room);
            }
            else
            {
                SetActiveSeat(room,nextSeat);
            }
        }

        room.LastActivityUtc =
            DateTime.UtcNow;
    }

    private void ForfeitCurrentRound(
        BlackjackPlayerState player)
    {
        if (!player.Ready ||
            player.Hands.Count == 0)
        {
            return;
        }

        foreach (BlackjackHandState hand in player.Hands)
        {
            hand.Result =
                "LOSE";

            hand.Payout =
                0;

            hand.Standing =
                true;

            hand.TurnComplete =
                true;

            casinoStatsService.RecordBlackjackHand(
                player.ProfileId,
                hand.Wager,
                0,
                "LOSE");
        }

        SyncPlayerLegacy(
            player);

        player.Result =
            "LOSE";
    }

    private void RemovePlayer(
        BlackjackRoomState room,
        BlackjackPlayerState player,
        bool kicked,
        string message)
    {
        int oldSeat =
            player.Seat;

        bool wasHost =
            player.IsHost;

        bool wasActive =
            room.Phase == "PLAYER" &&
            room.ActiveSeat == oldSeat;

        if ((room.Phase == "PLAYER" ||
             room.Phase == "DEALER") &&
            player.Ready &&
            player.Hands.Count > 0)
        {
            ForfeitCurrentRound(
                player);
        }

        room.Players.Remove(
            player);

        if (room.Players.Count == 0)
        {
            _rooms.Remove(
                room.RoomId);

            return;
        }

        if (wasHost)
        {
            PromoteHost(room);

            MarkState(
                room,
                $"HOST TRANSFERRED TO {room.HostName}");

            Diag(
                $"HOST_TRANSFER room={room.RoomId} old={player.DisplayName} new={room.HostName}");
        }
        else
        {
            MarkState(
                room,
                $"{player.DisplayName} LEFT TABLE");
        }

        if (wasActive)
        {
            int next =
                NextPlayableSeat(
                    room,
                    oldSeat);

            if (next < 0)
            {
                room.Phase =
                    "DEALER";

                room.ActiveSeat =
                    -1;

                room.Message =
                    "DEALER TURN";

                ClearTurnTimer(
                    room);
            }
            else
            {
                SetActiveSeat(
                    room,
                    next);
            }
        }

        room.LastActivityUtc =
            DateTime.UtcNow;
    }

    private static void PromoteHost(
        BlackjackRoomState room)
    {
        BlackjackPlayerState host =
            room.Players
                .OrderBy(x => x.Seat)
                .First();

        foreach (BlackjackPlayerState player in room.Players)
            player.IsHost =
                ReferenceEquals(
                    player,
                    host);

        room.HostProfileId =
            host.ProfileId;

        room.HostName =
            host.DisplayName;
    }

    private static void AdvanceAfterHand(
        BlackjackRoomState room,
        BlackjackPlayerState player)
    {
        for (int i =
                 player.ActiveHandIndex + 1;
             i < player.Hands.Count;
             i++)
        {
            if (!player.Hands[i].TurnComplete)
            {
                player.ActiveHandIndex =
                    i;

                room.Message =
                    $"{player.DisplayName} - HAND {i + 1}";

                ResetTurnTimer(
                    room);

                return;
            }
        }

        int next =
            NextPlayableSeat(
                room,
                player.Seat);

        if (next < 0)
        {
            room.Phase =
                "DEALER";

            room.ActiveSeat =
                -1;

            room.Message =
                "DEALER TURN";

            ClearTurnTimer(
                room);

            return;
        }

        SetActiveSeat(
            room,
            next);
    }

    private static int NextInsuranceSeat(
        BlackjackRoomState room,
        int currentSeat)
    {
        BlackjackPlayerState? next=
            room.Players
                .Where(x=>x.Ready&&x.Hands.Count>0&&!x.InsuranceDecisionMade&&x.Seat>currentSeat)
                .OrderBy(x=>x.Seat)
                .FirstOrDefault();

        return next?.Seat??-1;
    }

    private static void SetInsuranceSeat(
        BlackjackRoomState room,
        int seat)
    {
        BlackjackPlayerState player=
            room.Players.First(x=>x.Seat==seat);

        room.Phase="INSURANCE";
        room.ActiveSeat=seat;
        room.Message=$"{player.DisplayName}: INSURANCE?";
        room.StateRevision++;
        ResetTurnTimer(room);
    }

    private static void FinishInsurancePhase(
        BlackjackRoomState room)
    {
        bool dealerBlackjack=
            room.DealerCards.Count==2&&
            Value(room.DealerCards)==21;

        if(dealerBlackjack)
        {
            room.Phase="DEALER";
            room.ActiveSeat=-1;
            room.Message="DEALER BLACKJACK";
            ClearTurnTimer(room);
            return;
        }

        StartPlayerPhaseOrDealer(room);
    }

    private static void StartPlayerPhaseOrDealer(
        BlackjackRoomState room)
    {
        int seat=NextPlayableSeat(room,-1);

        if(seat<0)
        {
            room.Phase="DEALER";
            room.ActiveSeat=-1;
            room.Message="DEALER CHECK";
            ClearTurnTimer(room);
        }
        else
        {
            SetActiveSeat(room,seat);
        }
    }

    private static int NextPlayableSeat(
        BlackjackRoomState room,
        int currentSeat)
    {
        BlackjackPlayerState? next =
            room.Players
                .Where(
                    x =>
                        x.Ready &&
                        x.Hands.Any(
                            h => !h.TurnComplete) &&
                        x.Seat > currentSeat)
                .OrderBy(x => x.Seat)
                .FirstOrDefault();

        return next?.Seat
            ?? -1;
    }

    private static void SetActiveSeat(
        BlackjackRoomState room,
        int seat)
    {
        BlackjackPlayerState player =
            room.Players.First(
                x => x.Seat == seat);

        int handIndex =
            player.Hands.FindIndex(
                x => !x.TurnComplete);

        if (handIndex < 0)
        {
            int next =
                NextPlayableSeat(
                    room,
                    seat);

            if (next < 0)
            {
                room.Phase =
                    "DEALER";

                room.ActiveSeat =
                    -1;

                room.Message =
                    "DEALER TURN";

                ClearTurnTimer(
                    room);

                return;
            }

            SetActiveSeat(
                room,
                next);

            return;
        }

        player.ActiveHandIndex =
            handIndex;

        room.Phase =
            "PLAYER";

        room.ActiveSeat =
            seat;

        room.Message =
            player.Hands.Count > 1
                ? $"{player.DisplayName} - HAND {handIndex + 1}"
                : $"{player.DisplayName}'S TURN";

        room.StateRevision++;

        ResetTurnTimer(
            room);
    }

    private static void ResetTurnTimer(
        BlackjackRoomState room)
    {
        room.TurnStartedUtc =
            DateTime.UtcNow;

        room.TurnDeadlineUtc =
            room.TurnStartedUtc.Value +
            TurnLimit;
    }

    private static void ClearTurnTimer(
        BlackjackRoomState room)
    {
        room.TurnStartedUtc =
            null;

        room.TurnDeadlineUtc =
            null;
    }

    private static bool CanSplitHand(
        BlackjackPlayerState player,
        BlackjackHandState hand)
    {
        return player.Hands.Count <
                   MaxHandsPerPlayer &&
               !hand.TurnComplete &&
               !hand.IsSplitAce &&
               hand.Cards.Count == 2 &&
               hand.Cards[0].Rank ==
                   hand.Cards[1].Rank;
    }

    private static void CompleteTwentyOneIfNeeded(
        BlackjackHandState hand)
    {
        if (Value(hand.Cards) ==
            21)
        {
            hand.Standing =
                true;

            hand.TurnComplete =
                true;

            hand.Result =
                "21";
        }
    }

    private static void ResolveHand(
        BlackjackHandState hand,
        int dealerValue,
        bool dealerBust,
        bool dealerBlackjack)
    {
        int value =
            Value(hand.Cards);

        if (hand.Busted ||
            value > 21)
        {
            hand.Result =
                "LOSE";

            hand.Payout =
                0;
        }
        else if (hand.Blackjack &&
                 dealerBlackjack)
        {
            hand.Result =
                "PUSH";

            hand.Payout =
                hand.Wager;
        }
        else if (hand.Blackjack)
        {
            hand.Result =
                "BLACKJACK";

            hand.Payout =
                hand.Wager +
                (int)Math.Floor(
                    hand.Wager *
                    1.5m);
        }
        else if (dealerBlackjack)
        {
            hand.Result =
                "LOSE";

            hand.Payout =
                0;
        }
        else if (dealerBust ||
                 value > dealerValue)
        {
            hand.Result =
                "WIN";

            hand.Payout =
                hand.Wager * 2;
        }
        else if (value ==
                 dealerValue)
        {
            hand.Result =
                "PUSH";

            hand.Payout =
                hand.Wager;
        }
        else
        {
            hand.Result =
                "LOSE";

            hand.Payout =
                0;
        }

        hand.TurnComplete =
            true;
    }

    private static void SetAggregatePlayerResult(
        BlackjackPlayerState player)
    {
        if (player.Hands.Count == 0)
        {
            player.Result =
                string.Empty;

            return;
        }

        string first =
            player.Hands[0].Result;

        player.Result =
            player.Hands.All(
                x => x.Result == first)
                ? first
                : "MIXED";
    }

    private static void SyncPlayerLegacy(
        BlackjackPlayerState player)
    {
        if (player.Hands.Count == 0)
            return;

        player.Wager =
            player.Hands.Sum(
                x => x.Wager) +
            player.InsuranceWager;

        player.Payout =
            player.Hands.Sum(
                x => x.Payout) +
            player.InsurancePayout;

        player.Standing =
            player.Hands.All(
                x => x.Standing);

        player.Busted =
            player.Hands.All(
                x => x.Busted);

        player.Blackjack =
            player.Hands.Count == 1 &&
            player.Hands[0].Blackjack;

        player.TurnComplete =
            player.Hands.All(
                x => x.TurnComplete);

        BlackjackHandState display =
            player.ActiveHandIndex >= 0 &&
            player.ActiveHandIndex < player.Hands.Count
                ? player.Hands[player.ActiveHandIndex]
                : player.Hands[0];

        player.Cards =
            display.Cards
                .Select(CloneCard)
                .ToList();

        SetAggregatePlayerResult(
            player);
    }

    private static void ReindexHands(
        BlackjackPlayerState player)
    {
        for (int i = 0;
             i < player.Hands.Count;
             i++)
        {
            player.Hands[i].HandIndex =
                i;
        }
    }

    private static void ResetPlayerGameplay(
        BlackjackPlayerState player)
    {
        player.Payout =
            0;

        player.Standing =
            false;

        player.Busted =
            false;

        player.Blackjack =
            false;

        player.TurnComplete =
            false;

        player.Result =
            string.Empty;

        player.Cards.Clear();
        player.Hands.Clear();

        player.InsuranceWager = 0;
        player.InsurancePayout = 0;
        player.InsuranceDecisionMade = false;

        player.ActiveHandIndex =
            0;
    }

    private static void ResetPlayerRound(
        BlackjackPlayerState player)
    {
        player.Wager =
            0;

        player.Payout =
            0;

        player.Ready =
            false;

        ResetPlayerGameplay(
            player);
    }

    private static void PrepareNextHand(
        BlackjackRoomState room)
    {
        foreach (BlackjackPlayerState player in room.Players)
            ResetPlayerRound(player);

        room.DealerCards.Clear();
        room.Shoe.Clear();
        room.TauntEvents.Clear();

        room.Phase =
            "WAITING";

        room.ActiveSeat =
            -1;

        room.Message =
            "PLACE YOUR BETS";

        room.ResolvedUtc =
            null;

        room.SettlementId =
            string.Empty;

        room.SettlementApplied =
            false;

        ClearTurnTimer(
            room);

        MarkState(
            room,
            "BETTING ROUND OPEN");
    }

    private static BlackjackTauntEvent BuildVoiceEvent(
        BlackjackPlayerState player,
        string kind)
    {
        string[] phrases =
            kind == "WIN"
                ? new[]
                {
                    "GoodWork",
                    "OnGoodWork",
                    "Ready",
                    "Roger"
                }
                : new[]
                {
                    "Toxic",
                    "Provocation",
                    "BadWork",
                    "Negative",
                    "OnMutter"
                };

        string phrase =
            phrases[
                Random.Shared.Next(
                    phrases.Length)];

        string voiceName =
            (StableHash(player.ProfileId) & 1) == 0
                ? "Usec_1"
                : "Bear_1";

        return new BlackjackTauntEvent
        {
            EventId =
                Guid.NewGuid()
                    .ToString("N"),
            Kind =
                kind,
            LosingProfileId =
                player.ProfileId,
            LosingDisplayName =
                player.DisplayName,
            Phrase =
                phrase,
            VoiceName =
                voiceName,
            DelaySeconds =
                kind == "WIN"
                    ? Random.Shared.NextDouble() * 1.5
                    : Random.Shared.NextDouble() * 4.0,
            CreatedUtc =
                DateTime.UtcNow
        };
    }

    public static int Value(
        IEnumerable<BlackjackCard> cards)
    {
        int total =
            0;

        int aces =
            0;

        foreach (BlackjackCard card in cards)
        {
            if (card.Rank == "A")
            {
                total +=
                    11;

                aces++;
            }
            else if (card.Rank is "K" or "Q" or "J")
            {
                total +=
                    10;
            }
            else
            {
                total +=
                    int.Parse(
                        card.Rank);
            }
        }

        while (total > 21 &&
               aces-- > 0)
        {
            total -=
                10;
        }

        return total;
    }

    private static BlackjackPlayerState NewPlayer(
        string id,
        string name,
        bool host,
        int seat)
    {
        return new BlackjackPlayerState
        {
            ProfileId =
                id,
            DisplayName =
                Name(name, id),
            IsHost =
                host,
            Seat =
                seat
        };
    }

    private static string Name(
        string? name,
        string id)
    {
        return string.IsNullOrWhiteSpace(name)
            ? id
            : name;
    }

    private static BlackjackCard Draw(
        BlackjackRoomState room)
    {
        if (room.Shoe.Count == 0)
            room.Shoe =
                BuildShoe();

        BlackjackCard card =
            room.Shoe[^1];

        room.Shoe.RemoveAt(
            room.Shoe.Count - 1);

        return card;
    }

    private static List<BlackjackCard> BuildShoe()
    {
        string[] suits =
        {
            "S",
            "H",
            "D",
            "C"
        };

        string[] ranks =
        {
            "A",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "J",
            "Q",
            "K"
        };

        List<BlackjackCard> cards =
            new(312);

        for (int deck = 0;
             deck < 6;
             deck++)
        {
            foreach (string suit in suits)
            {
                foreach (string rank in ranks)
                {
                    cards.Add(
                        new BlackjackCard
                        {
                            Rank =
                                rank,
                            Suit =
                                suit
                        });
                }
            }
        }

        for (int i =
                 cards.Count - 1;
             i > 0;
             i--)
        {
            int j =
                Random.Shared.Next(
                    i + 1);

            (cards[i], cards[j]) =
                (cards[j], cards[i]);
        }

        return cards;
    }

    private void CleanupExpired()
    {
        DateTime now =
            DateTime.UtcNow;

        foreach (string id in
                 _rooms
                     .Where(
                         x =>
                         {
                             TimeSpan idle =
                                 now -
                                 x.Value.LastActivityUtc;

                             bool active =
                                 x.Value.Phase is
                                     "PLAYER" or
                                     "INSURANCE" or
                                     "DEALER";

                             return active
                                 ? idle > TimeSpan.FromMinutes(30)
                                 : idle > TimeSpan.FromMinutes(15);
                         })
                     .Select(x => x.Key)
                     .ToArray())
        {
            _rooms.Remove(
                id);
        }

        foreach (string key in
                 _recentKicks
                     .Where(x => x.Value.ExpiresUtc < now)
                     .Select(x => x.Key)
                     .ToArray())
        {
            _recentKicks.Remove(
                key);
        }
    }

    private static string KickKey(
        string roomId,
        string profileId)
    {
        return roomId +
            "|" +
            profileId;
    }

    private static int StableHash(
        string value)
    {
        unchecked
        {
            int hash =
                23;

            foreach (char c in value ?? string.Empty)
                hash =
                    hash * 31 +
                    c;

            return hash;
        }
    }

    private static BlackjackRoomActionResult Ok(
        string message,
        BlackjackRoomState room)
    {
        return new BlackjackRoomActionResult
        {
            Success =
                true,
            Message =
                message,
            Room =
                CloneRoom(room)
        };
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

    private static BlackjackRoomState CloneRoom(
        BlackjackRoomState room)
    {
        return new BlackjackRoomState
        {
            RoomId =
                room.RoomId,
            HostProfileId =
                room.HostProfileId,
            HostName =
                room.HostName,
            CreatedUtc =
                room.CreatedUtc,
            LastActivityUtc =
                room.LastActivityUtc,
            ResolvedUtc =
                room.ResolvedUtc,
            SettlementId =
                room.SettlementId,
            SettlementApplied =
                room.SettlementApplied,
            TurnStartedUtc =
                room.TurnStartedUtc,
            TurnDeadlineUtc =
                room.TurnDeadlineUtc,
            Phase =
                room.Phase,
            ActiveSeat =
                room.ActiveSeat,
            Message =
                room.Message,
            StateRevision =
                room.StateRevision,
            LastNotice =
                room.LastNotice,
            DealerCards =
                room.DealerCards
                    .Select(CloneCard)
                    .ToList(),
            TauntEvents =
                room.TauntEvents
                    .Select(
                        x =>
                            new BlackjackTauntEvent
                            {
                                EventId = x.EventId,
                                Kind = x.Kind,
                                LosingProfileId = x.LosingProfileId,
                                LosingDisplayName = x.LosingDisplayName,
                                Phrase = x.Phrase,
                                VoiceName = x.VoiceName,
                                DelaySeconds = x.DelaySeconds,
                                CreatedUtc = x.CreatedUtc
                            })
                    .ToList(),
            Players =
                room.Players
                    .OrderBy(x => x.Seat)
                    .Select(
                        x =>
                            new BlackjackPlayerState
                            {
                                ProfileId = x.ProfileId,
                                DisplayName = x.DisplayName,
                                IsHost = x.IsHost,
                                Seat = x.Seat,
                                Wager = x.Wager,
                                LastBaseBet = x.LastBaseBet,
                                Payout = x.Payout,
                                CurrencyStackMax = x.CurrencyStackMax,
                                Ready = x.Ready,
                                Standing = x.Standing,
                                Busted = x.Busted,
                                Blackjack = x.Blackjack,
                                TurnComplete = x.TurnComplete,
                                Result = x.Result,
                                ActiveHandIndex = x.ActiveHandIndex,
                                InsuranceWager = x.InsuranceWager,
                                InsurancePayout = x.InsurancePayout,
                                InsuranceDecisionMade = x.InsuranceDecisionMade,
                                Cards = x.Cards.Select(CloneCard).ToList(),
                                Hands = x.Hands.Select(CloneHand).ToList()
                            })
                    .ToList()
        };
    }

    private static BlackjackHandState CloneHand(
        BlackjackHandState hand)
    {
        return new BlackjackHandState
        {
            HandIndex =
                hand.HandIndex,
            Wager =
                hand.Wager,
            Payout =
                hand.Payout,
            Standing =
                hand.Standing,
            Busted =
                hand.Busted,
            Blackjack =
                hand.Blackjack,
            TurnComplete =
                hand.TurnComplete,
            IsSplitHand =
                hand.IsSplitHand,
            IsSplitAce =
                hand.IsSplitAce,
            Result =
                hand.Result,
            Cards =
                hand.Cards
                    .Select(CloneCard)
                    .ToList()
        };
    }

    private static BlackjackCard CloneCard(
        BlackjackCard card)
    {
        return new BlackjackCard
        {
            Rank =
                card.Rank,
            Suit =
                card.Suit
        };
    }

    private sealed class RecentKick
    {
        public string Message { get; set; } =
            string.Empty;

        public DateTime ExpiresUtc { get; set; }
    }
}

public class BlackjackLobbyState
{
    public BlackjackRoomState[] Rooms { get; set; } =
        Array.Empty<BlackjackRoomState>();
}

public class BlackjackRoomState
{
    public string RoomId { get; set; } =
        string.Empty;

    public string HostProfileId { get; set; } =
        string.Empty;

    public string HostName { get; set; } =
        string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime LastActivityUtc { get; set; }

    public DateTime? ResolvedUtc { get; set; }

    public string SettlementId { get; set; } =
        string.Empty;

    public bool SettlementApplied { get; set; }

    public DateTime? TurnStartedUtc { get; set; }

    public DateTime? TurnDeadlineUtc { get; set; }

    public string Phase { get; set; } =
        "WAITING";

    public int ActiveSeat { get; set; } =
        -1;

    public string Message { get; set; } =
        string.Empty;

    public long StateRevision { get; set; }

    public string LastNotice { get; set; } =
        string.Empty;

    public List<BlackjackPlayerState> Players { get; set; } =
        new();

    public List<BlackjackCard> DealerCards { get; set; } =
        new();

    public List<BlackjackTauntEvent> TauntEvents { get; set; } =
        new();

    public List<BlackjackCard> Shoe { get; set; } =
        new();
}

public class BlackjackTauntEvent
{
    public string EventId { get; set; } =
        string.Empty;

    public string Kind { get; set; } =
        "LOSS";

    public string LosingProfileId { get; set; } =
        string.Empty;

    public string LosingDisplayName { get; set; } =
        string.Empty;

    public string Phrase { get; set; } =
        string.Empty;

    public string VoiceName { get; set; } =
        string.Empty;

    public double DelaySeconds { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public class BlackjackPlayerState
{
    public string ProfileId { get; set; } =
        string.Empty;

    public string DisplayName { get; set; } =
        string.Empty;

    public bool IsHost { get; set; }

    public int Seat { get; set; }

    public int Wager { get; set; }

    public int LastBaseBet { get; set; }

    public int Payout { get; set; }

    public int CurrencyStackMax { get; set; } =
        1;

    public bool Ready { get; set; }

    public bool Standing { get; set; }

    public bool Busted { get; set; }

    public bool Blackjack { get; set; }

    public bool TurnComplete { get; set; }

    public string Result { get; set; } =
        string.Empty;

    public int ActiveHandIndex { get; set; }

    public int InsuranceWager { get; set; }

    public int InsurancePayout { get; set; }

    public bool InsuranceDecisionMade { get; set; }

    public List<BlackjackCard> Cards { get; set; } =
        new();

    public List<BlackjackHandState> Hands { get; set; } =
        new();
}

public class BlackjackHandState
{
    public int HandIndex { get; set; }

    public int Wager { get; set; }

    public int Payout { get; set; }

    public bool Standing { get; set; }

    public bool Busted { get; set; }

    public bool Blackjack { get; set; }

    public bool TurnComplete { get; set; }

    public bool IsSplitHand { get; set; }

    public bool IsSplitAce { get; set; }

    public string Result { get; set; } =
        string.Empty;

    public List<BlackjackCard> Cards { get; set; } =
        new();
}

public class BlackjackCard
{
    public string Rank { get; set; } =
        string.Empty;

    public string Suit { get; set; } =
        string.Empty;
}

public class BlackjackRoomActionResult
{
    public bool Success { get; set; }

    public string Message { get; set; } =
        string.Empty;

    public BlackjackRoomState? Room { get; set; }

    public int Balance { get; set; } =
        -1;
}
