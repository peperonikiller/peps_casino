using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace PepSlotMachine
{
    internal static class CurrencyService
    {
        internal sealed class CurrencyDefinition
        {
            internal string Key { get; }
            internal string DisplayName { get; }
            internal string TemplateId { get; }

            internal CurrencyDefinition(
                string key,
                string displayName,
                string templateId)
            {
                Key = key;
                DisplayName = displayName;
                TemplateId = templateId;
            }
        }

        internal static readonly CurrencyDefinition Gp =
            new CurrencyDefinition(
                "GP",
                "GP",
                "5d235b4d86f7742e017bc88a");

        internal static readonly CurrencyDefinition Roubles =
            new CurrencyDefinition(
                "RUB",
                "₽",
                "5449016a4bdc2d6f028b456f");

        private static readonly FieldInfo InventoryControllerField =
            typeof(InventoryScreen).GetField(
                "_inventoryController",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo OnProfileUpdateField =
            typeof(InventoryController).GetField(
                "OnProfileUpdate",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        private static InventoryScreen _cachedScreen;
        private static InventoryController _cachedController;

        internal static bool TryGetActiveCharacterInventoryController(
            out InventoryController controller)
        {
            controller = null;

            try
            {
                if (_cachedScreen != null &&
                    _cachedScreen.gameObject != null &&
                    _cachedScreen.gameObject.activeInHierarchy &&
                    _cachedController?.Inventory?.Stash != null)
                {
                    controller = _cachedController;
                    return true;
                }

                _cachedScreen = null;
                _cachedController = null;

                InventoryScreen[] screens =
                    Resources.FindObjectsOfTypeAll<InventoryScreen>();

                foreach (InventoryScreen screen in screens)
                {
                    if (screen == null ||
                        screen.gameObject == null ||
                        !screen.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    InventoryController found =
                        InventoryControllerField?.GetValue(screen)
                        as InventoryController;

                    if (found?.Inventory?.Stash == null)
                    {
                        continue;
                    }

                    _cachedScreen = screen;
                    _cachedController = found;
                    controller = found;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError(
                    $"Failed locating Character inventory: {ex}");
            }

            return false;
        }

        internal static bool IsCachedCharacterScreenActive()
        {
            return _cachedScreen != null &&
                   _cachedScreen.gameObject != null &&
                   _cachedScreen.gameObject.activeInHierarchy &&
                   _cachedController?.Inventory?.Stash != null;
        }

        internal static List<Item> GetStacks(
            InventoryController controller,
            CurrencyDefinition currency)
        {
            if (controller?.Inventory?.Stash == null)
            {
                return new List<Item>();
            }

            return controller.Inventory.Stash
                .GetAllItems()
                .Where(x =>
                    x != null &&
                    currency != null &&
                    x.TemplateId.ToString() == currency.TemplateId)
                .OrderByDescending(x => x.StackObjectsCount)
                .ToList();
        }

        internal static int GetBalance(
            InventoryController controller,
            CurrencyDefinition currency)
        {
            return GetStacks(
                    controller,
                    currency)
                .Sum(x => x.StackObjectsCount);
        }

        internal static int GetStackMax(
            InventoryController controller,
            CurrencyDefinition currency)
        {
            try
            {
                List<Item> stacks =
                    GetStacks(
                        controller,
                        currency);

                int liveMax =
                    stacks
                        .Where(x => x != null)
                        .Select(x => x.StackMaxSize)
                        .Where(x => x > 0)
                        .DefaultIfEmpty(0)
                        .Max();

                if (liveMax > 0)
                {
                    return liveMax;
                }

                EFT.ItemFactory factory =
                    Comfort.Common.Singleton<EFT.ItemFactory>.Instance;

                if (factory == null ||
                    controller == null ||
                    currency == null)
                {
                    return 1;
                }

                Item temp =
                    factory.CreateItem(
                        ((IDatabaseIdGenerator)controller).NextId,
                        currency.TemplateId,
                        null);

                int templateMax =
                    temp?.StackMaxSize ?? 1;

                return Math.Max(
                    1,
                    templateMax);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Could not resolve stack max for {currency?.Key}: {ex.Message}");

                return 1;
            }
        }

        internal static string GetProfileId(
            InventoryController controller)
        {
            if (controller == null)
            {
                return null;
            }

            try
            {
                Type type = controller.GetType();

                object profile =
                    type.GetProperty(
                        "Profile",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    ?.GetValue(controller);

                if (profile == null)
                {
                    FieldInfo field =
                        type.GetField(
                            "Profile",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                        ?? type.GetField(
                            "_profile",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);

                    profile = field?.GetValue(controller);
                }

                if (profile == null)
                {
                    return null;
                }

                Type profileType = profile.GetType();

                object id =
                    profileType.GetProperty("Id")?.GetValue(profile)
                    ?? profileType.GetProperty("ProfileId")?.GetValue(profile)
                    ?? profileType.GetField("Id")?.GetValue(profile)
                    ?? profileType.GetField("ProfileId")?.GetValue(profile);

                return id?.ToString();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError(
                    $"Unable to resolve profile id: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Mirrors the authoritative server currency balance into the live EFT item
        /// objects so the Character screen and slot UI update immediately.
        /// The server profile remains the source of truth.
        ///
        /// New and removed stacks are mirrored with EFT's native
        /// Begin -> Execute -> Succeed item-operation lifecycle. Stack limits
        /// are resolved from the live EFT item/template data so stack-limit
        /// mods remain compatible.
        /// </summary>
        internal static bool MirrorServerBalance(
            InventoryController controller,
            CurrencyDefinition currency,
            int targetBalance)
        {
            try
            {
                if (controller?.Inventory?.Stash?.Grid == null ||
                    targetBalance < 0)
                {
                    return false;
                }

                if (currency == null)
                {
                    return false;
                }

                int currentBalance =
                    GetBalance(
                        controller,
                        currency);

                if (currentBalance ==
                    targetBalance)
                {
                    return true;
                }

                int stackMax =
                    GetStackMax(
                        controller,
                        currency);

                List<Item> stacks =
                    GetStacks(
                        controller,
                        currency);

                int requiredStacks =
                    targetBalance <= 0
                        ? 0
                        : (targetBalance + stackMax - 1) / stackMax;

                // The SPT server is authoritative. If it had to create new GP
                // stacks, mirror those stacks locally without sending a second
                // backend operation. This is UI/profile mirroring only.
                while (stacks.Count < requiredStacks)
                {
                    EFT.ItemFactory factory =
                        Comfort.Common.Singleton<EFT.ItemFactory>.Instance;

                    if (factory == null)
                    {
                        Plugin.Log?.LogError(
                            "ItemFactory was unavailable while mirroring a new currency stack.");
                        return false;
                    }

                    Item gp = factory.CreateItem(
                        ((IDatabaseIdGenerator)controller).NextId,
                        currency.TemplateId,
                        null);

                    if (gp == null)
                    {
                        Plugin.Log?.LogError(
                            "Could not create local currency mirror item.");
                        return false;
                    }

                    gp.StackObjectsCount = 1;

                    GridItemAddress address =
                        controller.Inventory.Stash.Grid.FindLocationForItem(gp);

                    if (address == null)
                    {
                        Plugin.Log?.LogError(
                            "No local stash space available for mirrored currency stack.");
                        return false;
                    }

                    var add =
                        ItemManipulator.Add(
                            gp,
                            address,
                            controller,
                            simulate: true);

                    if (add.Failed)
                    {
                        Plugin.Log?.LogError(
                            $"Could not prepare local GP add operation: {add.Error}");
                        return false;
                    }

                    // GridView creates the new GridItemView on Begin.
                    add.Value.RaiseEvents(
                        controller,
                        CommandStatus.Begin);

                    var addExecute =
                        add.Value.Execute();

                    if (addExecute.Failed)
                    {
                        add.Value.RaiseEvents(
                            controller,
                            CommandStatus.Failed);

                        Plugin.Log?.LogError(
                            $"Local GP add Execute() failed: {addExecute.Error}");
                        return false;
                    }

                    add.Value.RaiseEvents(
                        controller,
                        CommandStatus.Succeed);

                    stacks.Add(gp);

                    gp.RaiseRefreshEvent(
                        refreshIcon: true);
                }

                // Remove excess local mirror stacks when the authoritative
                // balance shrinks enough that fewer stacks are needed.
                while (stacks.Count > requiredStacks)
                {
                    Item remove = stacks[stacks.Count - 1];

                    var removeResult =
                        ItemManipulator.Remove(
                            remove,
                            controller,
                            simulate: true);

                    if (removeResult.Failed)
                    {
                        Plugin.Log?.LogError(
                            $"Could not prepare local GP remove operation: {removeResult.Error}");
                        return false;
                    }

                    // Mirror EFT's Begin -> Execute -> Succeed lifecycle.
                    removeResult.Value.RaiseEvents(
                        controller,
                        CommandStatus.Begin);

                    var removeExecute =
                        removeResult.Value.Execute();

                    if (removeExecute.Failed)
                    {
                        removeResult.Value.RaiseEvents(
                            controller,
                            CommandStatus.Failed);

                        Plugin.Log?.LogError(
                            $"Local GP remove Execute() failed: {removeExecute.Error}");
                        return false;
                    }

                    removeResult.Value.RaiseEvents(
                        controller,
                        CommandStatus.Succeed);

                    stacks.RemoveAt(stacks.Count - 1);
                }

                int remaining = targetBalance;

                foreach (Item stack in stacks)
                {
                    int count =
                        Math.Min(
                            stackMax,
                            remaining);

                    if (stack.StackObjectsCount != count)
                    {
                        stack.StackObjectsCount = count;
                        stack.RaiseRefreshEvent(refreshIcon: false);
                    }

                    remaining -= count;
                }

                if (remaining != 0)
                {
                    return false;
                }

                try
                {
                    controller.Inventory.Stash.Grid.RevalidateSpaceBuffer();
                    controller.ReportProfileUpdate();
                }
                catch (Exception refreshEx)
                {
                    Plugin.Log?.LogWarning(
                        $"currency mirror succeeded but safe refresh notification failed: {refreshEx.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError(
                    $"Failed mirroring server currency balance locally: {ex}");
                return false;
            }
        }
    }
}
