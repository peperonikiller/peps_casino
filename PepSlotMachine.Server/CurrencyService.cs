using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace PepSlotMachine.Server;

public sealed record CasinoCurrency(
    string Key,
    string DisplayName,
    string TemplateId);

public sealed class CasinoCurrencyStackState
{
    public string Id { get; set; } =
        string.Empty;

    public int Count { get; set; }
}

public static class CasinoCurrencies
{
    public static readonly CasinoCurrency Gp =
        new(
            "CHIP",
            "Casino Chip",
            "565b8ae839a24633cd129ce1");

    public static readonly CasinoCurrency Roubles =
        new(
            "RUB",
            "₽",
            "5449016a4bdc2d6f028b456f");
}

/// <summary>
/// Server-authoritative casino currency/item handling for SPT 4.1.3.
///
/// Important design rules:
/// 1. Only currency physically inside the player's stash tree is counted.
///    This matches the Character-screen client view used by Pep's Casino.
/// 2. SPT's native InventoryHelper/ItemHelper APIs are used directly.
///    There is no reflection-based AddItemToStash discovery.
/// 3. Existing stacks are modified first and changed items are added to the
///    ItemEventRouterResponse exactly like SPT's PaymentService.
/// 4. New stacks are created as clean Item/Upd records, split with
///    ItemHelper.SplitStackIntoSeparateItems(), then added with
///    InventoryHelper.AddItemsToStash().
/// 5. Stack limits come from the live SPT item template. The client-supplied
///    stack maximum is only a compatibility fallback if the server template
///    cannot provide a usable value.
/// 6. Stack counts are normalized to whole numbers before casino arithmetic.
/// </summary>
[Injectable]
public class ServerCurrencyService(
    ISptLogger<ServerCurrencyService> logger,
    InventoryHelper inventoryHelper,
    ItemHelper itemHelper)
{
    public List<Item> GetStacks(
        PmcData pmc,
        CasinoCurrency currency)
    {
        if (pmc?.Inventory?.Items is null ||
            currency is null)
        {
            return [];
        }

        MongoId templateId =
            new(
                currency.TemplateId);

        return pmc.Inventory.Items
            .Where(
                item =>
                    item != null &&
                    item.Template == templateId &&
                    pmc.IsItemInStash(item))
            .ToList();
    }

    public List<CasinoCurrencyStackState> GetStackState(
        PmcData pmc,
        CasinoCurrency currency)
    {
        return GetStacks(
                pmc,
                currency)
            .Select(
                stack =>
                    new CasinoCurrencyStackState
                    {
                        Id =
                            stack.Id.ToString(),
                        Count =
                            ReadWholeStackCount(
                                stack)
                    })
            .Where(x => x.Count > 0)
            .OrderBy(x => x.Id)
            .ToList();
    }

    public int GetBalance(
        PmcData pmc,
        CasinoCurrency currency)
    {
        long total =
            0;

        foreach (Item stack in
                 GetStacks(
                     pmc,
                     currency))
        {
            total +=
                ReadWholeStackCount(
                    stack);
        }

        if (total >
            int.MaxValue)
        {
            logger.Warning(
                $"{currency.Key} balance exceeded Int32 range; clamping to {int.MaxValue}.");

            return int.MaxValue;
        }

        return (int)total;
    }

    public bool TryAdjustBalance(
        PmcData pmc,
        MongoId sessionId,
        ItemEventRouterResponse output,
        CasinoCurrency currency,
        int delta,
        int clientStackMaxHint,
        out int newBalance,
        out string error)
    {
        int currentBalance =
            GetBalance(
                pmc,
                currency);

        long targetLong =
            (long)currentBalance +
            delta;

        if (targetLong < 0 ||
            targetLong > int.MaxValue)
        {
            newBalance =
                currentBalance;

            error =
                delta < 0
                    ? $"NOT ENOUGH {currency.Key}"
                    : $"{currency.Key} BALANCE TOO LARGE";

            return false;
        }

        return TrySetBalance(
            pmc,
            sessionId,
            output,
            currency,
            (int)targetLong,
            clientStackMaxHint,
            out newBalance,
            out error);
    }

    public bool TrySetBalance(
        PmcData pmc,
        MongoId sessionId,
        ItemEventRouterResponse output,
        CasinoCurrency currency,
        int targetBalance,
        int clientStackMaxHint,
        out int newBalance,
        out string error)
    {
        error =
            string.Empty;

        newBalance =
            GetBalance(
                pmc,
                currency);

        if (pmc?.Inventory?.Items is null ||
            currency is null ||
            output is null)
        {
            error =
                "INVALID CURRENCY TRANSACTION";

            return false;
        }

        if (targetBalance < 0)
        {
            error =
                $"INVALID {currency.Key} BALANCE";

            return false;
        }

        try
        {
            List<Item> stacks =
                GetStacks(
                    pmc,
                    currency);

            NormalizeStacks(
                stacks,
                sessionId,
                output);

            int currentBalance =
                SumStacks(
                    stacks);

            newBalance =
                currentBalance;

            if (targetBalance ==
                currentBalance)
            {
                return true;
            }

            int stackMax =
                ResolveStackMax(
                    currency,
                    stacks,
                    clientStackMaxHint);

            if (targetBalance <
                currentBalance)
            {
                if (!DecreaseBalance(
                        pmc,
                        sessionId,
                        output,
                        currency,
                        stacks,
                        currentBalance -
                        targetBalance,
                        out error))
                {
                    return false;
                }
            }
            else
            {
                if (!IncreaseBalance(
                        pmc,
                        sessionId,
                        output,
                        currency,
                        stacks,
                        targetBalance -
                        currentBalance,
                        stackMax,
                        out error))
                {
                    return false;
                }
            }

            int verified =
                GetBalance(
                    pmc,
                    currency);

            if (verified !=
                targetBalance)
            {
                error =
                    $"{currency.Key} BALANCE VERIFY FAILED ({verified} != {targetBalance})";

                logger.Error(
                    error);

                newBalance =
                    verified;

                return false;
            }

            newBalance =
                verified;

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(
                $"Currency update failed for {currency.Key}: {ex}");

            error =
                $"SERVER {currency.Key} INVENTORY ERROR";

            newBalance =
                GetBalance(
                    pmc,
                    currency);

            return false;
        }
    }

    private bool DecreaseBalance(
        PmcData pmc,
        MongoId sessionId,
        ItemEventRouterResponse output,
        CasinoCurrency currency,
        List<Item> stacks,
        int amount,
        out string error)
    {
        error =
            string.Empty;

        int left =
            amount;

        // Match SPT's general money behavior by consuming smaller stash
        // stacks first. This reduces stack fragmentation.
        foreach (Item stack in
                 stacks
                     .OrderBy(
                         ReadWholeStackCount)
                     .ToArray())
        {
            if (left <= 0)
                break;

            int count =
                ReadWholeStackCount(
                    stack);

            if (count <= 0)
                continue;

            if (left >=
                count)
            {
                left -=
                    count;

                inventoryHelper.RemoveItem(
                    pmc,
                    stack.Id,
                    sessionId,
                    output);

                continue;
            }

            EnsureUpd(
                stack);

            stack.Upd!.StackObjectsCount =
                count -
                left;

            left =
                0;

            MarkChanged(
                sessionId,
                output,
                stack);
        }

        if (left != 0)
        {
            error =
                $"SERVER {currency.Key} DEDUCTION FAILED";

            return false;
        }

        return true;
    }

    private bool IncreaseBalance(
        PmcData pmc,
        MongoId sessionId,
        ItemEventRouterResponse output,
        CasinoCurrency currency,
        List<Item> stacks,
        int amount,
        int stackMax,
        out string error)
    {
        error =
            string.Empty;

        int left =
            amount;

        // Fill the fullest legal stacks first so the operation touches as few
        // profile items as possible. Existing oversized stacks are preserved.
        foreach (Item stack in
                 stacks
                     .OrderByDescending(
                         ReadWholeStackCount))
        {
            if (left <= 0)
                break;

            int count =
                ReadWholeStackCount(
                    stack);

            int effectiveMax =
                Math.Max(
                    stackMax,
                    count);

            int room =
                effectiveMax -
                count;

            if (room <= 0)
                continue;

            int add =
                Math.Min(
                    room,
                    left);

            EnsureUpd(
                stack);

            stack.Upd!.StackObjectsCount =
                count +
                add;

            left -=
                add;

            MarkChanged(
                sessionId,
                output,
                stack);
        }

        if (left <= 0)
            return true;

        MongoId templateId =
            new(
                currency.TemplateId);

        Item rootCurrencyReward =
            new()
            {
                Id =
                    new MongoId(),
                Template =
                    templateId,
                Upd =
                    new Upd
                    {
                        StackObjectsCount =
                            Math.Round(
                                (double)left)
                    }
            };

        // This is the same native stack-splitting path SPT uses when
        // GiveProfileMoney() has to create new currency stacks.
        List<List<Item>> rewards =
            itemHelper
                .SplitStackIntoSeparateItems(
                    rootCurrencyReward);

        // If a stack-limit mod changed the live server template, ItemHelper
        // should already split correctly. The fallback below only guards a
        // malformed/missing template result.
        if (rewards.Count == 0)
        {
            rewards =
                SplitFallback(
                    rootCurrencyReward,
                    left,
                    stackMax);
        }

        HashSet<MongoId> rewardIds =
            rewards
                .Where(x => x.Count > 0)
                .Select(x => x[0].Id)
                .ToHashSet();

        int warningsBefore =
            output.Warnings?.Count
            ?? 0;

        AddItemsDirectRequest request =
            new()
            {
                ItemsWithModsToAdd =
                    rewards,
                FoundInRaid =
                    false,
                Callback =
                    null,

                // Pep's Casino mirrors the Character stash, not the sorting
                // table. If the stash cannot accept a new stack, fail the
                // transaction instead of silently moving casino money there.
                UseSortingTable =
                    false
            };

        inventoryHelper.AddItemsToStash(
            sessionId,
            request,
            pmc,
            output);

        int warningsAfter =
            output.Warnings?.Count
            ?? 0;

        if (warningsAfter >
            warningsBefore)
        {
            error =
                $"NO STASH SPACE FOR {currency.Key}";

            return false;
        }

        var inventoryItems =
            pmc.Inventory?.Items;

        if (inventoryItems is null)
        {
            error =
                $"SERVER COULD NOT VERIFY NEW {currency.Key} STACK";

            return false;
        }

        bool allPresent =
            inventoryItems
                .Where(x => x != null)
                .Select(x => x.Id)
                .Intersect(
                    rewardIds)
                .Count() ==
            rewardIds.Count;

        if (!allPresent)
        {
            error =
                $"SERVER COULD NOT ADD NEW {currency.Key} STACK";

            return false;
        }

        return true;
    }

    private int ResolveStackMax(
        CasinoCurrency currency,
        List<Item> existingStacks,
        int clientStackMaxHint)
    {
        int templateMax =
            0;

        try
        {
            MongoId templateId =
                new(
                    currency.TemplateId);

            var itemDetails =
                itemHelper
                    .GetItem(
                        templateId)
                    .Value;

            if (itemDetails?.Properties is not null)
            {
                templateMax =
                    (int)Math.Round(
                        itemDetails
                            .Properties
                            .StackMaxSize
                        ?? 0d);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(
                $"Unable to read server stack max for {currency.Key}: {ex.Message}");
        }

        int largestExisting =
            existingStacks
                .Select(
                    ReadWholeStackCount)
                .DefaultIfEmpty(0)
                .Max();

        // Existing oversized stacks are valid for compatibility with
        // stack-limit mods/profile migrations. For NEW stacks, prefer the
        // server template. Use the client value only as a last-resort hint.
        int resolved =
            templateMax > 0
                ? templateMax
                : Math.Max(
                    1,
                    clientStackMaxHint);

        return Math.Max(
            resolved,
            largestExisting);
    }

    private static List<List<Item>> SplitFallback(
        Item root,
        int total,
        int stackMax)
    {
        List<List<Item>> result =
            [];

        int left =
            total;

        while (left > 0)
        {
            int count =
                Math.Min(
                    Math.Max(1, stackMax),
                    left);

            result.Add(
                [
                    new Item
                    {
                        Id =
                            new MongoId(),
                        Template =
                            root.Template,
                        Upd =
                            new Upd
                            {
                                StackObjectsCount =
                                    count
                            }
                    }
                ]);

            left -=
                count;
        }

        return result;
    }

    private static void NormalizeStacks(
        IEnumerable<Item> stacks,
        MongoId sessionId,
        ItemEventRouterResponse output)
    {
        foreach (Item stack in stacks)
        {
            int normalized =
                ReadWholeStackCount(
                    stack);

            EnsureUpd(
                stack);

            double current =
                stack.Upd!
                    .StackObjectsCount
                ?? 0d;

            if (Math.Abs(
                    current -
                    normalized) <
                0.000001d)
            {
                continue;
            }

            stack.Upd.StackObjectsCount =
                normalized;

            MarkChanged(
                sessionId,
                output,
                stack);
        }
    }

    private static int SumStacks(
        IEnumerable<Item> stacks)
    {
        long total =
            0;

        foreach (Item stack in stacks)
        {
            total +=
                ReadWholeStackCount(
                    stack);
        }

        return total >
            int.MaxValue
                ? int.MaxValue
                : (int)total;
    }

    private static int ReadWholeStackCount(
        Item stack)
    {
        double raw =
            stack?.Upd?
                .StackObjectsCount
            ?? 1d;

        if (double.IsNaN(raw) ||
            double.IsInfinity(raw))
        {
            return 0;
        }

        double rounded =
            Math.Round(
                raw,
                MidpointRounding.AwayFromZero);

        if (rounded <= 0d)
            return 0;

        if (rounded >=
            int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)rounded;
    }

    private static void EnsureUpd(
        Item item)
    {
        item.Upd ??=
            new Upd
            {
                StackObjectsCount =
                    1
            };

        item.Upd.StackObjectsCount ??=
            1;
    }

    private static void MarkChanged(
        MongoId sessionId,
        ItemEventRouterResponse output,
        Item item)
    {
        output
            .ProfileChanges?[sessionId]
            .Items?
            .ChangedItems?
            .Add(item);
    }
}
