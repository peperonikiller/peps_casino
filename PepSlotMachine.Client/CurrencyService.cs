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
                "CHIP",
                "Casino Chip",
                "565b8ae839a24633cd129ce1");

        internal static readonly CurrencyDefinition Roubles =
            new CurrencyDefinition(
                "RUB",
                "₽",
                "5449016a4bdc2d6f028b456f");

        private static readonly FieldInfo InventoryControllerField =
            typeof(InventoryScreen).GetField(
                "_inventoryController",
                BindingFlags.Instance | BindingFlags.NonPublic);

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
            if (controller?.Inventory?.Stash == null ||
                currency == null)
            {
                return new List<Item>();
            }

            return controller.Inventory.Stash
                .GetAllItems()
                .Where(
                    x =>
                        x != null &&
                        x.TemplateId.ToString() ==
                        currency.TemplateId)
                .OrderByDescending(
                    x => x.StackObjectsCount)
                .ToList();
        }

        internal static int GetBalance(
            InventoryController controller,
            CurrencyDefinition currency)
        {
            long total =
                GetStacks(
                        controller,
                        currency)
                    .Sum(
                        x => (long)Math.Max(
                            0,
                            x.StackObjectsCount));

            return total > int.MaxValue
                ? int.MaxValue
                : (int)total;
        }

        internal static int GetStackMax(
            InventoryController controller,
            CurrencyDefinition currency)
        {
            try
            {
                int liveMax =
                    GetStacks(
                            controller,
                            currency)
                        .Select(
                            x => x.StackMaxSize)
                        .Where(
                            x => x > 0)
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

                return Math.Max(
                    1,
                    temp?.StackMaxSize ?? 1);
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
                object profile =
                    controller.GetType()
                        .GetProperty(
                            "Profile",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                        ?.GetValue(controller);

                if (profile == null)
                {
                    FieldInfo field =
                        controller.GetType()
                            .GetField(
                                "Profile",
                                BindingFlags.Instance |
                                BindingFlags.Public |
                                BindingFlags.NonPublic)
                        ?? controller.GetType()
                            .GetField(
                                "_profile",
                                BindingFlags.Instance |
                                BindingFlags.Public |
                                BindingFlags.NonPublic);

                    profile =
                        field?.GetValue(
                            controller);
                }

                if (profile == null)
                {
                    return null;
                }

                Type profileType =
                    profile.GetType();

                object id =
                    profileType
                        .GetProperty("Id")
                        ?.GetValue(profile)
                    ?? profileType
                        .GetProperty("ProfileId")
                        ?.GetValue(profile)
                    ?? profileType
                        .GetField("Id")
                        ?.GetValue(profile)
                    ?? profileType
                        .GetField("ProfileId")
                        ?.GetValue(profile);

                return id?.ToString();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError(
                    $"Unable to resolve profile id: {ex}");

                return null;
            }
        }

        // Blackjack currently remains on its previously-tested
        // server-authoritative Rouble path. This mirror is retained only for
        // Blackjack/Roubles. Slots and Casino Chips never call it; their
        // add/remove operations use NativeCurrencyService and EFT's normal
        // TryRunNetworkTransaction pipeline.
        internal static bool MirrorServerBalance(
            InventoryController controller,
            CurrencyDefinition currency,
            int targetBalance)
        {
            try
            {
                if (controller?.Inventory?.Stash?.Grid == null ||
                    currency == null ||
                    targetBalance < 0)
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
                    Math.Max(
                        1,
                        GetStackMax(
                            controller,
                            currency));

                List<Item> stacks =
                    GetStacks(
                        controller,
                        currency);

                int requiredStacks =
                    targetBalance <= 0
                        ? 0
                        : (targetBalance + stackMax - 1) / stackMax;

                // IMPORTANT:
                //
                // The SPT server has already performed the authoritative
                // inventory transaction. The client is only reconciling the
                // Character-screen object tree/UI with that returned balance.
                //
                // Do NOT use ItemManipulator.Add/Remove here. Those methods
                // perform action/ownership validation and a custom item can be
                // classified as a trader-owned item, causing:
                //     "Unable to edit a traders item"
                //
                // Instead use EFT's built-in unrestricted container mutation
                // methods and explicitly raise the same Begin -> Succeed/Failed
                // ItemAddress events that GridView consumes.

                while (stacks.Count <
                       requiredStacks)
                {
                    EFT.ItemFactory factory =
                        Comfort.Common.Singleton<EFT.ItemFactory>.Instance;

                    if (factory == null)
                    {
                        Plugin.Log?.LogError(
                            "ItemFactory was unavailable while mirroring a new currency stack.");

                        return false;
                    }

                    Item item =
                        factory.CreateItem(
                            ((IDatabaseIdGenerator)controller).NextId,
                            currency.TemplateId,
                            null);

                    if (item == null)
                    {
                        Plugin.Log?.LogError(
                            $"Could not create local {currency.DisplayName} mirror item.");

                        return false;
                    }

                    item.StackObjectsCount =
                        1;

                    GridItemAddress address =
                        controller.Inventory.Stash.Grid.FindLocationForItem(
                            item);

                    if (address == null)
                    {
                        Plugin.Log?.LogError(
                            $"No local stash space available for mirrored {currency.DisplayName} stack.");

                        return false;
                    }

                    // GridView creates the visual ItemView on Begin.
                    address.RaiseAddEvent(
                        item,
                        CommandStatus.Begin,
                        controller);

                    var addResult =
                        controller.Inventory.Stash.Grid.AddItemWithoutRestrictions(
                            item,
                            address.LocationInGrid);

                    if (addResult.Failed)
                    {
                        address.RaiseAddEvent(
                            item,
                            CommandStatus.Failed,
                            controller);

                        Plugin.Log?.LogError(
                            $"Local unrestricted {currency.DisplayName} add failed: {addResult.Error}");

                        return false;
                    }

                    address.RaiseAddEvent(
                        item,
                        CommandStatus.Succeed,
                        controller);

                    stacks.Add(
                        item);

                    item.RaiseRefreshEvent(
                        refreshIcon: true);
                }

                // If the authoritative balance requires fewer stacks, remove
                // the excess local stack object completely. Never leave a
                // zero-count currency item in the stash.
                while (stacks.Count >
                       requiredStacks)
                {
                    Item remove =
                        stacks[stacks.Count - 1];

                    ItemAddress address =
                        remove.CurrentAddress;

                    if (address == null)
                    {
                        Plugin.Log?.LogError(
                            $"Could not resolve the current address for {currency.DisplayName} stack removal.");

                        return false;
                    }

                    address.RaiseRemoveEvent(
                        remove,
                        CommandStatus.Begin,
                        controller);

                    var removeResult =
                        address.RemoveWithoutRestrictions(
                            remove);

                    if (removeResult.Failed)
                    {
                        address.RaiseRemoveEvent(
                            remove,
                            CommandStatus.Failed,
                            controller);

                        Plugin.Log?.LogError(
                            $"Local unrestricted {currency.DisplayName} remove failed: {removeResult.Error}");

                        return false;
                    }

                    address.RaiseRemoveEvent(
                        remove,
                        CommandStatus.Succeed,
                        controller);

                    stacks.RemoveAt(
                        stacks.Count - 1);
                }

                int remaining =
                    targetBalance;

                // Fill the remaining local stacks to match the exact
                // authoritative server balance.
                foreach (Item stack in
                         stacks)
                {
                    int count =
                        Math.Min(
                            stackMax,
                            remaining);

                    if (count <= 0)
                    {
                        Plugin.Log?.LogError(
                            $"Currency mirror attempted to leave a zero-count {currency.DisplayName} stack.");

                        return false;
                    }

                    if (stack.StackObjectsCount !=
                        count)
                    {
                        stack.StackObjectsCount =
                            count;

                        stack.RaiseRefreshEvent(
                            refreshIcon: false);
                    }

                    remaining -=
                        count;
                }

                if (remaining != 0)
                {
                    Plugin.Log?.LogError(
                        $"{currency.DisplayName} mirror did not consume the full target balance. Remaining={remaining}");

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
                        $"{currency.DisplayName} mirror succeeded but refresh notification failed: {refreshEx.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError(
                    $"Failed mirroring server {currency.DisplayName} balance locally: {ex}");

                return false;
            }
        }
    }
}
