using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace PepSlotMachine
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.pep.spt.slotmachine";
        public const string PluginName = "Pep Slot Machine";
        public const string PluginVersion = "2.11.2";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> OpenHotkey;
        internal static ConfigEntry<string> ServerUrl;
        internal static ConfigEntry<bool> JackpotEnabled;
        internal static ConfigEntry<bool> SoloAutoDeal;

        private SlotMachineUI _ui;

        private void Awake()
        {
            Log = Logger;

            OpenHotkey = Config.Bind(
                "Slot Machine",
                "Open / Close Slot Machine",
                new KeyboardShortcut(KeyCode.F6),
                "Opens or closes the Pep's Casino.");

            ServerUrl = Config.Bind(
                "Slot Machine",
                "SPT Server URL",
                "https://127.0.0.1:6969",
                "SPT server base URL. Change this only if your SPT server uses a different address or port.");

            JackpotEnabled = Config.Bind(
                "Slot Machine",
                "Jackpot Enabled",
                true,
                "Enable the special five-7 jackpot payout.");

            SoloAutoDeal = Config.Bind(
                "Blackjack",
                "Solo Auto Deal",
                true,
                "When you are the only player at a Blackjack table, automatically deal shortly after a bet or rebet is accepted.");

            _ui = gameObject.AddComponent<SlotMachineUI>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void Update()
        {
            if (OpenHotkey != null &&
                _ui != null &&
                OpenHotkey.Value.IsDown())
            {
                _ui.Toggle();
            }
        }
    }
}
