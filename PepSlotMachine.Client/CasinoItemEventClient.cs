using System;
using System.Collections;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;

namespace PepSlotMachine
{
    internal sealed class CasinoItemEventResult
    {
        internal bool Success { get; set; }
        internal string Error { get; set; }
    }

    internal sealed class PepCasinoSpinCommand
    {
        [JsonProperty("Action")]
        public string Action { get; set; } =
            "PepCasinoSpin";

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("bet")]
        public int Bet { get; set; }

        [JsonProperty("jackpotEnabled")]
        public bool JackpotEnabled { get; set; }

        [JsonProperty("currencyStackMax")]
        public int CurrencyStackMax { get; set; }
    }

    internal sealed class PepCasinoBuyInCommand
    {
        [JsonProperty("Action")]
        public string Action { get; set; } =
            "PepCasinoBuyIn";

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("gpStackMax")]
        public int GpStackMax { get; set; }

        [JsonProperty("roubleStackMax")]
        public int RoubleStackMax { get; set; }
    }

    internal static class CasinoItemEventClient
    {
        internal static IEnumerator SendSpin(
            InventoryController controller,
            int bet,
            bool jackpotEnabled,
            int currencyStackMax,
            Action<string, CasinoItemEventResult> completed)
        {
            string requestId =
                Guid.NewGuid().ToString("N");

            PepCasinoSpinCommand command =
                new PepCasinoSpinCommand
                {
                    RequestId =
                        requestId,
                    Bet =
                        bet,
                    JackpotEnabled =
                        jackpotEnabled,
                    CurrencyStackMax =
                        Math.Max(
                            1,
                            currencyStackMax)
                };

            yield return
                SendImmediate(
                    controller,
                    command,
                    result =>
                    {
                        completed?.Invoke(
                            requestId,
                            result);
                    });
        }

        internal static IEnumerator SendBuyIn(
            InventoryController controller,
            int gpStackMax,
            int roubleStackMax,
            Action<string, CasinoItemEventResult> completed)
        {
            string requestId =
                Guid.NewGuid().ToString("N");

            PepCasinoBuyInCommand command =
                new PepCasinoBuyInCommand
                {
                    RequestId =
                        requestId,
                    GpStackMax =
                        Math.Max(
                            1,
                            gpStackMax),
                    RoubleStackMax =
                        Math.Max(
                            1,
                            roubleStackMax)
                };

            yield return
                SendImmediate(
                    controller,
                    command,
                    result =>
                    {
                        completed?.Invoke(
                            requestId,
                            result);
                    });
        }

        private static IEnumerator SendImmediate(
            InventoryController controller,
            object command,
            Action<CasinoItemEventResult> completed)
        {
            if (controller == null)
            {
                completed?.Invoke(
                    new CasinoItemEventResult
                    {
                        Success =
                            false,
                        Error =
                            "INVENTORY CONTROLLER UNAVAILABLE"
                    });

                yield break;
            }

            ClientBackendSession backendSession =
                ResolveBackendSession(
                    controller);

            if (backendSession == null)
            {
                completed?.Invoke(
                    new CasinoItemEventResult
                    {
                        Success =
                            false,
                        Error =
                            "BACKEND SESSION UNAVAILABLE"
                    });

                yield break;
            }

            bool finished =
                false;

            CasinoItemEventResult eventResult =
                null;

            try
            {
                backendSession.SendOperationRightNow(
                    command,
                    result =>
                    {
                        eventResult =
                            new CasinoItemEventResult
                            {
                                Success =
                                    result != null &&
                                    result.Succeed,
                                Error =
                                    result?.Error
                            };

                        finished =
                            true;
                    });
            }
            catch (Exception ex)
            {
                completed?.Invoke(
                    new CasinoItemEventResult
                    {
                        Success =
                            false,
                        Error =
                            ex.GetBaseException().Message
                    });

                yield break;
            }

            float deadline =
                UnityEngine.Time.unscaledTime +
                15f;

            while (!finished &&
                   UnityEngine.Time.unscaledTime <
                   deadline)
            {
                yield return null;
            }

            if (!finished)
            {
                completed?.Invoke(
                    new CasinoItemEventResult
                    {
                        Success =
                            false,
                        Error =
                            "CASINO ITEM EVENT TIMED OUT"
                    });

                yield break;
            }

            completed?.Invoke(
                eventResult
                ?? new CasinoItemEventResult
                {
                    Success =
                        false,
                    Error =
                        "CASINO ITEM EVENT FAILED"
                });
        }

        private static ClientBackendSession ResolveBackendSession(
            InventoryController controller)
        {
            Type type =
                controller.GetType();

            while (type != null)
            {
                FieldInfo field =
                    type.GetField(
                        "_backendSession",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                if (field?.GetValue(controller)
                    is ClientBackendSession session)
                {
                    return session;
                }

                type =
                    type.BaseType;
            }

            return null;
        }
    }
}
