using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace PepSlotMachine.Server;

[Injectable]
public sealed class CasinoShopService(
    ISptLogger<CasinoShopService> logger,
    InventoryHelper inventoryHelper,
    ItemHelper itemHelper)
{
    public bool TryAddReward(
        PmcData pmc,
        MongoId sessionId,
        ItemEventRouterResponse output,
        string templateIdText,
        int quantity,
        out string error)
    {
        error = string.Empty;

        if (pmc?.Inventory?.Items is null ||
            output is null ||
            string.IsNullOrWhiteSpace(templateIdText) ||
            quantity <= 0)
        {
            error = "INVALID SHOP REWARD";
            return false;
        }

        try
        {
            MongoId templateId = new(templateIdText);
            var template = itemHelper.GetItem(templateId).Value;

            if (template is null)
            {
                error = $"SHOP ITEM TEMPLATE NOT FOUND: {templateIdText}";
                return false;
            }

            Item root = new()
            {
                Id = new MongoId(),
                Template = templateId,
                Upd = new Upd
                {
                    StackObjectsCount = quantity
                }
            };

            List<List<Item>> rewards =
                itemHelper.SplitStackIntoSeparateItems(root);

            if (rewards.Count == 0)
            {
                rewards = [[root]];
            }

            int warningsBefore = output.Warnings?.Count ?? 0;

            inventoryHelper.AddItemsToStash(
                sessionId,
                new AddItemsDirectRequest
                {
                    ItemsWithModsToAdd = rewards,
                    FoundInRaid = false,
                    Callback = null,
                    UseSortingTable = false
                },
                pmc,
                output);

            if ((output.Warnings?.Count ?? 0) > warningsBefore)
            {
                error = "NO STASH SPACE FOR SHOP ITEM";
                return false;
            }

            HashSet<MongoId> ids =
                rewards
                    .Where(x => x.Count > 0)
                    .Select(x => x[0].Id)
                    .ToHashSet();

            int present =
                pmc.Inventory.Items
                    .Where(x => x != null)
                    .Select(x => x.Id)
                    .Intersect(ids)
                    .Count();

            if (present != ids.Count)
            {
                error = "SHOP REWARD COULD NOT BE ADDED";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Casino shop reward failed: {ex}");
            error = "SERVER SHOP INVENTORY ERROR";
            return false;
        }
    }
}
