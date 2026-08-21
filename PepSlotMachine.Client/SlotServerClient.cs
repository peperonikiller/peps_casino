using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace PepSlotMachine
{

    internal sealed class CasinoResultRequest
    {
        [JsonProperty("requestId")]
        public string RequestId { get; set; }
    }

    internal sealed class SlotSpinRequest
    {
        [JsonProperty("bet")]
        public int Bet { get; set; }

        [JsonProperty("profileId")]
        public string ProfileId { get; set; }

        [JsonProperty("testOdds")]
        public bool TestOdds { get; set; }

        [JsonProperty("jackpotEnabled")]
        public bool JackpotEnabled { get; set; }

        [JsonProperty("currencyStackMax")]
        public int CurrencyStackMax { get; set; }

        [JsonProperty("expectedPostWagerBalance")]
        public int ExpectedPostWagerBalance { get; set; }
    }

    internal sealed class SlotCell
    {
        [JsonProperty("reel")]
        public int Reel { get; set; }

        [JsonProperty("row")]
        public int Row { get; set; }
    }

    internal sealed class SlotLineWin
    {
        [JsonProperty("payline")]
        public int Payline { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("matches")]
        public int Matches { get; set; }
        [JsonProperty("win")]
        public int Win { get; set; }

        [JsonProperty("cells")]
        public SlotCell[] Cells { get; set; }

        [JsonProperty("jackpot")]
        public bool Jackpot { get; set; }
    }

    internal sealed class SlotSpinResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; }

        [JsonProperty("win")]
        public int Win { get; set; }

        [JsonProperty("winningPayline")]
        public int WinningPayline { get; set; }

        [JsonProperty("symbols")]
        public string[][] Symbols { get; set; }

        [JsonProperty("winningCells")]
        public SlotCell[] WinningCells { get; set; }

        [JsonProperty("lineWins")]
        public SlotLineWin[] LineWins { get; set; }

        [JsonProperty("jackpot")]
        public bool Jackpot { get; set; }

        [JsonProperty("oddsProfile")]
        public string OddsProfile { get; set; }

        [JsonProperty("jackpotAmount")]
        public int JackpotAmount { get; set; }

        [JsonProperty("jackpotPayout")]
        public int JackpotPayout { get; set; }
    }

    internal sealed class JackpotStateResponse
    {
        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("baseAmount")]
        public int BaseAmount { get; set; }

        [JsonProperty("lastWinner")]
        public string LastWinner { get; set; }

        [JsonProperty("lastWinAmount")]
        public int LastWinAmount { get; set; }
    }

    internal sealed class BlackjackLobbyState
    {
        [JsonProperty("rooms")]
        public BlackjackRoomState[] Rooms { get; set; }
    }

    internal sealed class BlackjackRoomState
    {
        [JsonProperty("roomId")]
        public string RoomId { get; set; }

        [JsonProperty("hostProfileId")]
        public string HostProfileId { get; set; }

        [JsonProperty("hostName")]
        public string HostName { get; set; }

        [JsonProperty("players")]
        public BlackjackPlayerState[] Players { get; set; }

        [JsonProperty("dealerCards")]
        public BlackjackCard[] DealerCards { get; set; }

        [JsonProperty("tauntEvents")]
        public BlackjackTauntEvent[] TauntEvents { get; set; }

        [JsonProperty("phase")]
        public string Phase { get; set; }

        [JsonProperty("resolvedUtc")]
        public DateTime? ResolvedUtc { get; set; }

        [JsonProperty("settlementId")]
        public string SettlementId { get; set; }

        [JsonProperty("settlementApplied")]
        public bool SettlementApplied { get; set; }

        [JsonProperty("turnStartedUtc")]
        public DateTime? TurnStartedUtc { get; set; }

        [JsonProperty("turnDeadlineUtc")]
        public DateTime? TurnDeadlineUtc { get; set; }

        [JsonProperty("activeSeat")]
        public int ActiveSeat { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("stateRevision")]
        public long StateRevision { get; set; }

        [JsonProperty("lastNotice")]
        public string LastNotice { get; set; }
    }

    internal sealed class BlackjackTauntEvent
    {
        [JsonProperty("eventId")]
        public string EventId { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("losingProfileId")]
        public string LosingProfileId { get; set; }

        [JsonProperty("losingDisplayName")]
        public string LosingDisplayName { get; set; }

        [JsonProperty("phrase")]
        public string Phrase { get; set; }

        [JsonProperty("voiceName")]
        public string VoiceName { get; set; }

        [JsonProperty("delaySeconds")]
        public double DelaySeconds { get; set; }

        [JsonProperty("createdUtc")]
        public DateTime CreatedUtc { get; set; }
    }

    internal sealed class BlackjackPlayerState
    {
        [JsonProperty("profileId")]
        public string ProfileId { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("isHost")]
        public bool IsHost { get; set; }

        [JsonProperty("seat")]
        public int Seat { get; set; }

        [JsonProperty("wager")]
        public int Wager { get; set; }

        [JsonProperty("lastBaseBet")]
        public int LastBaseBet { get; set; }

        [JsonProperty("payout")]
        public int Payout { get; set; }

        [JsonProperty("currencyStackMax")]
        public int CurrencyStackMax { get; set; }

        [JsonProperty("ready")]
        public bool Ready { get; set; }

        [JsonProperty("standing")]
        public bool Standing { get; set; }

        [JsonProperty("busted")]
        public bool Busted { get; set; }

        [JsonProperty("blackjack")]
        public bool Blackjack { get; set; }

        [JsonProperty("turnComplete")]
        public bool TurnComplete { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("cards")]
        public BlackjackCard[] Cards { get; set; }

        [JsonProperty("activeHandIndex")]
        public int ActiveHandIndex { get; set; }

        [JsonProperty("insuranceWager")]
        public int InsuranceWager { get; set; }

        [JsonProperty("insurancePayout")]
        public int InsurancePayout { get; set; }

        [JsonProperty("insuranceDecisionMade")]
        public bool InsuranceDecisionMade { get; set; }

        [JsonProperty("hands")]
        public BlackjackHandState[] Hands { get; set; }
    }

    internal sealed class BlackjackHandState
    {
        [JsonProperty("handIndex")]
        public int HandIndex { get; set; }

        [JsonProperty("wager")]
        public int Wager { get; set; }

        [JsonProperty("payout")]
        public int Payout { get; set; }

        [JsonProperty("standing")]
        public bool Standing { get; set; }

        [JsonProperty("busted")]
        public bool Busted { get; set; }

        [JsonProperty("blackjack")]
        public bool Blackjack { get; set; }

        [JsonProperty("turnComplete")]
        public bool TurnComplete { get; set; }

        [JsonProperty("isSplitHand")]
        public bool IsSplitHand { get; set; }

        [JsonProperty("isSplitAce")]
        public bool IsSplitAce { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("cards")]
        public BlackjackCard[] Cards { get; set; }
    }

    internal sealed class BlackjackCard
    {
        [JsonProperty("rank")]
        public string Rank { get; set; }
        [JsonProperty("suit")]
        public string Suit { get; set; }
    }

    internal sealed class BlackjackRoomActionResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("room")]
        public BlackjackRoomState Room { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; } = -1;
    }

    internal sealed class CasinoPlayerStats
    {
        [JsonProperty("profileId")] public string ProfileId { get; set; }
        [JsonProperty("slotSpins")] public long SlotSpins { get; set; }
        [JsonProperty("gpWagered")] public long GpWagered { get; set; }
        [JsonProperty("gpReturned")] public long GpReturned { get; set; }
        [JsonProperty("biggestSlotReturn")] public int BiggestSlotReturn { get; set; }
        [JsonProperty("jackpotsWon")] public int JackpotsWon { get; set; }
        [JsonProperty("biggestJackpot")] public int BiggestJackpot { get; set; }
        [JsonProperty("blackjackHands")] public long BlackjackHands { get; set; }
        [JsonProperty("blackjackWins")] public long BlackjackWins { get; set; }
        [JsonProperty("blackjackLosses")] public long BlackjackLosses { get; set; }
        [JsonProperty("blackjackPushes")] public long BlackjackPushes { get; set; }
        [JsonProperty("naturalBlackjacks")] public long NaturalBlackjacks { get; set; }
        [JsonProperty("insuranceBets")] public long InsuranceBets { get; set; }
        [JsonProperty("insuranceWins")] public long InsuranceWins { get; set; }
        [JsonProperty("roublesWagered")] public long RoublesWagered { get; set; }
        [JsonProperty("roublesReturned")] public long RoublesReturned { get; set; }
        [JsonProperty("biggestBlackjackProfit")] public int BiggestBlackjackProfit { get; set; }

        [JsonProperty("recentHistory")]
        public CasinoHistoryEntry[] RecentHistory { get; set; }

        [JsonProperty("gpNet")] public long GpNet { get; set; }
        [JsonProperty("roublesNet")] public long RoublesNet { get; set; }
    }

    internal sealed class CasinoHistoryEntry
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("wager")]
        public int Wager { get; set; }

        [JsonProperty("return")]
        public int Return { get; set; }

        [JsonProperty("net")]
        public int Net { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; }

        [JsonProperty("utc")]
        public DateTime Utc { get; set; }
    }

    internal sealed class CasinoConfigResponse
    {
        [JsonProperty("buyInCostRoubles")]
        public int BuyInCostRoubles { get; set; }

        [JsonProperty("blackjackMinBet")]
        public int BlackjackMinBet { get; set; }

        [JsonProperty("blackjackMaxBet")]
        public int BlackjackMaxBet { get; set; }

        [JsonProperty("blackjackDiagnostics")]
        public bool BlackjackDiagnostics { get; set; }

        [JsonProperty("shopItems")]
        public CasinoShopItem[] ShopItems { get; set; }
    }

    internal sealed class CasinoShopItem
    {
        [JsonProperty("templateId")]
        public string TemplateId { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("chipCost")]
        public int ChipCost { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }

    internal sealed class CasinoShopPurchaseResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("chipBalance")]
        public int ChipBalance { get; set; }
    }

    internal sealed class CasinoBuyInResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("gpBalance")]
        public int GpBalance { get; set; }

        [JsonProperty("roubleBalance")]
        public int RoubleBalance { get; set; }
}

    internal static class SlotServerClient
    {
        private sealed class LocalCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                // SPT uses a local self-signed certificate by default.
                return true;
            }
        }

        internal static IEnumerator GetCasinoConfig(
            string profileId,
            Action<CasinoConfigResponse> completed)
        {
            return PostJson(
                "/pep-casino/config",
                profileId,
                "{}",
                completed);
        }

        internal static IEnumerator BuyIn(
            string profileId,
            int gpStackMax,
            int roubleStackMax,
            Action<CasinoBuyInResponse> completed)
        {
            string baseUrl =
                Plugin.ServerUrl?.Value?.TrimEnd('/')
                ?? "https://127.0.0.1:6969";

            string url =
                baseUrl +
                "/pep-casino/buyin";

            string json =
                JsonConvert.SerializeObject(
                    new
                    {
                        profileId,
                        gpStackMax,
                        roubleStackMax
                    });

            using (UnityWebRequest request =
                new UnityWebRequest(
                    url,
                    UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(json));

                request.downloadHandler =
                    new DownloadHandlerBuffer();

                request.certificateHandler =
                    new LocalCertificateHandler();

                request.SetRequestHeader(
                    "Content-Type",
                    "application/json");

                request.SetRequestHeader(
                    "requestcompressed",
                    "0");

                request.SetRequestHeader(
                    "responsecompressed",
                    "0");

                if (!string.IsNullOrEmpty(profileId))
                {
                    request.SetRequestHeader(
                        "Cookie",
                        "PHPSESSID=" +
                        profileId);
                }

                request.timeout =
                    15;

                yield return
                    request.SendWebRequest();

                if (request.result !=
                    UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(
                        new CasinoBuyInResponse
                        {
                            Success = false,
                            Message = request.error
                        });

                    yield break;
                }

                try
                {
                    completed?.Invoke(
                        JsonConvert.DeserializeObject<
                            CasinoBuyInResponse>(
                                request.downloadHandler.text));
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning(
                        $"Buy-in response parse failed: {ex.Message}");

                    completed?.Invoke(
                        new CasinoBuyInResponse
                        {
                            Success = false,
                            Message = "INVALID BUY-IN RESPONSE"
                        });
                }
            }
        }

        internal static IEnumerator Spin(
            int bet,
            string profileId,
            int currencyStackMax,
            int expectedPostWagerBalance,
            Action<SlotSpinResponse> completed)
        {
            string baseUrl =
                Plugin.ServerUrl?.Value?.TrimEnd('/')
                ?? "https://127.0.0.1:6969";

            string url =
                baseUrl + "/pep-slots/spin";

            SlotSpinRequest payload =
                new SlotSpinRequest
                {
                    Bet = bet,
                    ProfileId = profileId,
                    TestOdds =
                        false,
                    JackpotEnabled =
                        Plugin.JackpotEnabled == null ||
                        Plugin.JackpotEnabled.Value,
                    CurrencyStackMax =
                        Math.Max(
                            1,
                            currencyStackMax),
                    ExpectedPostWagerBalance =
                        Math.Max(
                            0,
                            expectedPostWagerBalance)
                };

            string json =
                JsonConvert.SerializeObject(payload);

            using (UnityWebRequest request =
                new UnityWebRequest(
                    url,
                    UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(json));

                request.downloadHandler =
                    new DownloadHandlerBuffer();

                request.certificateHandler =
                    new LocalCertificateHandler();

                request.SetRequestHeader(
                    "Content-Type",
                    "application/json");

                request.SetRequestHeader(
                    "requestcompressed",
                    "0");

                request.SetRequestHeader(
                    "responsecompressed",
                    "0");

                // Static SPT routes normally identify the profile from the
                // PHPSESSID cookie. Sending it explicitly also keeps this
                // independent of UnityWebRequest's cookie jar.
                if (!string.IsNullOrEmpty(profileId))
                {
                    request.SetRequestHeader(
                        "Cookie",
                        "PHPSESSID=" + profileId);
                }

                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result !=
                    UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(
                        new SlotSpinResponse
                        {
                            Success = false,
                            Message =
                                $"SERVER REQUEST FAILED: {request.error}"
                        });

                    yield break;
                }

                try
                {
                    SlotSpinResponse response =
                        JsonConvert.DeserializeObject<SlotSpinResponse>(
                            request.downloadHandler.text);

                    completed?.Invoke(
                        response
                        ?? new SlotSpinResponse
                        {
                            Success = false,
                            Message = "EMPTY SERVER RESPONSE"
                        });
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError(
                        $"Failed parsing slot server response: {ex}");

                    completed?.Invoke(
                        new SlotSpinResponse
                        {
                            Success = false,
                            Message = "INVALID SERVER RESPONSE"
                        });
                }
            }
        }

        internal static IEnumerator GetSlotSpinResult(
            string profileId,
            string requestId,
            Action<SlotSpinResponse> completed)
        {
            string json =
                JsonConvert.SerializeObject(
                    new CasinoResultRequest
                    {
                        RequestId =
                            requestId
                    });

            return PostJson(
                "/pep-slots/result",
                profileId,
                json,
                completed);
        }

        internal static IEnumerator GetBuyInResult(
            string profileId,
            string requestId,
            Action<CasinoBuyInResponse> completed)
        {
            string json =
                JsonConvert.SerializeObject(
                    new CasinoResultRequest
                    {
                        RequestId =
                            requestId
                    });

            return PostJson(
                "/pep-casino/buyin/result",
                profileId,
                json,
                completed);
        }

        internal static IEnumerator GetShopPurchaseResult(
            string profileId,
            string requestId,
            Action<CasinoShopPurchaseResponse> completed)
        {
            string json =
                JsonConvert.SerializeObject(
                    new CasinoResultRequest
                    {
                        RequestId = requestId
                    });

            return PostJson(
                "/pep-casino/shop/result",
                profileId,
                json,
                completed);
        }

        internal static IEnumerator GetStats(
            string profileId,
            Action<CasinoPlayerStats> completed)
        {
            string baseUrl =
                Plugin.ServerUrl?.Value?.TrimEnd('/')
                ?? "https://127.0.0.1:6969";

            string url =
                baseUrl + "/pep-casino/stats";

            string json =
                JsonConvert.SerializeObject(
                    new { profileId });

            using (UnityWebRequest request =
                new UnityWebRequest(
                    url,
                    UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(json));

                request.downloadHandler =
                    new DownloadHandlerBuffer();

                request.certificateHandler =
                    new LocalCertificateHandler();

                request.SetRequestHeader(
                    "Content-Type",
                    "application/json");

                request.SetRequestHeader(
                    "requestcompressed",
                    "0");

                request.SetRequestHeader(
                    "responsecompressed",
                    "0");

                if (!string.IsNullOrEmpty(
                        profileId))
                {
                    request.SetRequestHeader(
                        "Cookie",
                        "PHPSESSID=" +
                        profileId);
                }

                request.timeout =
                    15;

                yield return
                    request.SendWebRequest();

                if (request.result !=
                    UnityWebRequest.Result.Success)
                {
                    Plugin.Log?.LogWarning(
                        $"Casino stats request failed: {request.error}");

                    completed?.Invoke(
                        null);

                    yield break;
                }

                try
                {
                    completed?.Invoke(
                        JsonConvert.DeserializeObject<
                            CasinoPlayerStats>(
                                request.downloadHandler.text));
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning(
                        $"Casino stats response parse failed: {ex.Message}");

                    completed?.Invoke(
                        null);
                }
            }
        }

        internal static IEnumerator GetJackpot(
            string profileId,
            Action<JackpotStateResponse> completed)
        {
            string baseUrl = Plugin.ServerUrl?.Value ?? "https://127.0.0.1:6969";
            string url = baseUrl.TrimEnd('/') + "/pep-casino/jackpot";

            using (UnityWebRequest request =
                   new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.certificateHandler = new LocalCertificateHandler();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("requestcompressed", "0");
                request.SetRequestHeader("responsecompressed", "0");

                if (!string.IsNullOrEmpty(profileId))
                    request.SetRequestHeader("Cookie", "PHPSESSID=" + profileId);

                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log?.LogWarning(
                        $"Jackpot state request failed: {request.error}");
                    yield break;
                }

                try
                {
                    JackpotStateResponse response =
                        JsonConvert.DeserializeObject<JackpotStateResponse>(
                            request.downloadHandler.text);

                    if (response != null)
                        completed?.Invoke(response);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning(
                        $"Could not parse jackpot state: {ex.Message}");
                }
            }
        }


        internal static IEnumerator GetBlackjackLobby(
            string profileId,
            Action<BlackjackLobbyState> completed)
        {
            return PostJson(
                "/pep-casino/blackjack/lobby",
                profileId,
                "{}",
                completed);
        }

        internal static IEnumerator HostBlackjack(
            string profileId,
            string displayName,
            Action<BlackjackRoomActionResult> completed)
        {
            string json =
                JsonConvert.SerializeObject(
                    new
                    {
                        profileId,
                        displayName
                    });

            return PostJson(
                "/pep-casino/blackjack/host",
                profileId,
                json,
                completed);
        }

        internal static IEnumerator JoinBlackjack(
            string roomId,
            string profileId,
            string displayName,
            Action<BlackjackRoomActionResult> completed)
        {
            string json =
                JsonConvert.SerializeObject(
                    new
                    {
                        roomId,
                        profileId,
                        displayName
                    });

            return PostJson(
                "/pep-casino/blackjack/join",
                profileId,
                json,
                completed);
        }

        internal static IEnumerator LeaveBlackjack(
            string roomId,
            string profileId,
            Action<BlackjackRoomActionResult> completed)
        {
            string json =
                JsonConvert.SerializeObject(
                    new
                    {
                        roomId,
                        profileId
                    });

            return PostJson(
                "/pep-casino/blackjack/leave",
                profileId,
                json,
                completed);
        }

        internal static IEnumerator GetBlackjackRoom(string roomId,string profileId,Action<BlackjackRoomActionResult> done)
        { return PostJson("/pep-casino/blackjack/room",profileId,JsonConvert.SerializeObject(new{roomId}),done); }
        internal static IEnumerator SetBlackjackBet(string roomId,string profileId,int bet,int stackMax,Action<BlackjackRoomActionResult> done)
        { return PostJson("/pep-casino/blackjack/bet",profileId,JsonConvert.SerializeObject(new{roomId,profileId,bet,currencyStackMax=stackMax}),done); }
        internal static IEnumerator BlackjackInsurance(string roomId,string profileId,bool take,Action<BlackjackRoomActionResult> done)
        { return PostJson("/pep-casino/blackjack/insurance",profileId,JsonConvert.SerializeObject(new{roomId,profileId,take}),done); }
        internal static IEnumerator BlackjackAction(string path,string roomId,string profileId,int stackMax,Action<BlackjackRoomActionResult> done)
        { return PostJson(path,profileId,JsonConvert.SerializeObject(new{roomId,profileId,currencyStackMax=stackMax}),done); }

        private static IEnumerator PostJson<T>(
            string path,
            string profileId,
            string json,
            Action<T> completed)
            where T : class
        {
            string baseUrl =
                Plugin.ServerUrl?.Value
                ?? "https://127.0.0.1:6969";

            string url =
                baseUrl.TrimEnd('/') +
                path;

            using (UnityWebRequest request =
                   new UnityWebRequest(
                       url,
                       UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(
                            json ?? "{}"));

                request.downloadHandler =
                    new DownloadHandlerBuffer();

                request.certificateHandler =
                    new LocalCertificateHandler();

                request.SetRequestHeader(
                    "Content-Type",
                    "application/json");

                request.SetRequestHeader(
                    "requestcompressed",
                    "0");

                request.SetRequestHeader(
                    "responsecompressed",
                    "0");

                if (!string.IsNullOrEmpty(
                        profileId))
                {
                    request.SetRequestHeader(
                        "Cookie",
                        "PHPSESSID=" +
                        profileId);
                }

                request.timeout = 15;

                yield return
                    request.SendWebRequest();

                if (request.result !=
                    UnityWebRequest.Result.Success)
                {
                    Plugin.Log?.LogWarning(
                        $"Casino request failed ({path}): {request.error}");

                    yield break;
                }

                try
                {
                    T response =
                        JsonConvert.DeserializeObject<T>(
                            request.downloadHandler.text);

                    if (response != null)
                    {
                        completed?.Invoke(
                            response);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning(
                        $"Casino response parse failed ({path}): {ex.Message}");
                }
            }
        }

    }
}
