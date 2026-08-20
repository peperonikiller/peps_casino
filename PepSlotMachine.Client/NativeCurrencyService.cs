using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace PepSlotMachine
{
    internal sealed class NativeCurrencyResult
    {
        internal bool Success { get; set; }
        internal string Error { get; set; }
        internal int Balance { get; set; }
    }

    internal static class NativeCurrencyService
    {
        internal static IEnumerator Spend(
            InventoryController controller,
            CurrencyService.CurrencyDefinition currency,
            int amount,
            Action<NativeCurrencyResult> completed)
        {
            if (controller?.Inventory?.Stash == null ||
                currency == null ||
                amount < 0)
            {
                Complete(
                    completed,
                    false,
                    "INVALID INVENTORY TRANSACTION",
                    controller,
                    currency);

                yield break;
            }

            if (amount == 0)
            {
                Complete(
                    completed,
                    true,
                    null,
                    controller,
                    currency);

                yield break;
            }

            int startingBalance =
                CurrencyService.GetBalance(
                    controller,
                    currency);

            if (startingBalance < amount)
            {
                Complete(
                    completed,
                    false,
                    $"NOT ENOUGH {currency.DisplayName.ToUpperInvariant()}",
                    controller,
                    currency);

                yield break;
            }

            // SplitToNowhereResult is NOT a supported top-level network
            // transaction result in this EFT build. ItemController's converter
            // accepts RemoveResult/DiscardResult/SplitResult/etc., but not
            // SplitToNowhereResult.
            //
            // Therefore spending is implemented entirely with supported native
            // RemoveResult transactions:
            //
            //   remove complete stack(s) until removed >= amount
            //   then refund any overpayment through the native Add() path
            //
            // Example: stack=10, spend=5 -> remove 10, add 5 back.
            //
            // This avoids zero-count stacks and avoids unsupported partial-
            // destruction transaction types while still keeping all profile
            // changes inside EFT/SPT's normal network inventory pipeline.

            List<Item> selected =
                new List<Item>();

            int selectedTotal =
                0;

            foreach (Item stack in
                     CurrencyService.GetStacks(
                             controller,
                             currency)
                         .OrderBy(
                             x => x.StackObjectsCount))
            {
                if (selectedTotal >= amount)
                {
                    break;
                }

                int count =
                    Math.Max(
                        0,
                        stack.StackObjectsCount);

                if (count <= 0)
                {
                    continue;
                }

                var validation =
                    ItemManipulator.Remove(
                        stack,
                        controller,
                        simulate: true);

                if (validation.Failed)
                {
                    Complete(
                        completed,
                        false,
                        validation.Error?.ToString()
                            ?? "CURRENCY SPEND VALIDATION FAILED",
                        controller,
                        currency);

                    yield break;
                }

                selected.Add(
                    stack);

                selectedTotal +=
                    count;
            }

            if (selectedTotal < amount)
            {
                Complete(
                    completed,
                    false,
                    "CURRENCY SPEND VALIDATION FAILED",
                    controller,
                    currency);

                yield break;
            }

            int removedTotal =
                0;

            foreach (Item selectedStack in
                     selected)
            {
                // Resolve the current live object by ID before each operation.
                // A previous native transaction may have refreshed/replaced
                // objects in the local inventory graph.
                string selectedId =
                    selectedStack.Id.ToString();

                Item stack =
                    CurrencyService.GetStacks(
                            controller,
                            currency)
                        .FirstOrDefault(
                            x =>
                                string.Equals(
                                    x.Id.ToString(),
                                    selectedId,
                                    StringComparison.OrdinalIgnoreCase));

                if (stack == null)
                {
                    Complete(
                        completed,
                        false,
                        $"CURRENCY STACK {selectedId} DISAPPEARED",
                        controller,
                        currency);

                    yield break;
                }

                int count =
                    Math.Max(
                        0,
                        stack.StackObjectsCount);

                var operation =
                    ItemManipulator.Remove(
                        stack,
                        controller,
                        simulate: true);

                if (operation.Failed)
                {
                    Complete(
                        completed,
                        false,
                        operation.Error?.ToString()
                            ?? "REMOVE OPERATION FAILED",
                        controller,
                        currency);

                    yield break;
                }

                var task =
                    controller.TryRunNetworkTransaction(
                        operation);

                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.IsFaulted ||
                    task.IsCanceled)
                {
                    Complete(
                        completed,
                        false,
                        task.Exception?.GetBaseException().Message
                            ?? "REMOVE TRANSACTION FAILED",
                        controller,
                        currency);

                    yield break;
                }

                var result =
                    task.Result;

                if (result == null ||
                    result.Failed)
                {
                    Complete(
                        completed,
                        false,
                        result?.Error
                            ?? "REMOVE TRANSACTION REJECTED",
                        controller,
                        currency);

                    yield break;
                }

                removedTotal +=
                    count;
            }

            int refundAmount =
                removedTotal -
                amount;

            if (refundAmount < 0)
            {
                Complete(
                    completed,
                    false,
                    "CURRENCY SPEND UNDERFLOW",
                    controller,
                    currency);

                yield break;
            }

            if (refundAmount > 0)
            {
                NativeCurrencyResult refund =
                    null;

                yield return
                    Add(
                        controller,
                        currency,
                        refundAmount,
                        result =>
                        {
                            refund =
                                result;
                        });

                if (refund == null ||
                    !refund.Success)
                {
                    Complete(
                        completed,
                        false,
                        refund?.Error
                            ?? "CURRENCY CHANGE REFUND FAILED",
                        controller,
                        currency);

                    yield break;
                }
            }

            int finalBalance =
                CurrencyService.GetBalance(
                    controller,
                    currency);

            int expectedBalance =
                startingBalance -
                amount;

            if (finalBalance !=
                expectedBalance)
            {
                Complete(
                    completed,
                    false,
                    $"BALANCE VERIFY FAILED ({finalBalance} != {expectedBalance})",
                    controller,
                    currency);

                yield break;
            }

            Complete(
                completed,
                true,
                null,
                controller,
                currency);
        }


        internal static IEnumerator Add(
            InventoryController controller,
            CurrencyService.CurrencyDefinition currency,
            int amount,
            Action<NativeCurrencyResult> completed)
        {
            if (controller?.Inventory?.Stash == null ||
                currency == null ||
                amount < 0)
            {
                Complete(
                    completed,
                    false,
                    "INVALID INVENTORY TRANSACTION",
                    controller,
                    currency);

                yield break;
            }

            if (amount == 0)
            {
                Complete(
                    completed,
                    true,
                    null,
                    controller,
                    currency);

                yield break;
            }

            ItemFactory factory =
                Singleton<ItemFactory>.Instance;

            if (factory == null)
            {
                Complete(
                    completed,
                    false,
                    "ITEM FACTORY UNAVAILABLE",
                    controller,
                    currency);

                yield break;
            }

            int startingBalance =
                CurrencyService.GetBalance(
                    controller,
                    currency);

            int stackMax =
                Math.Max(
                    1,
                    CurrencyService.GetStackMax(
                        controller,
                        currency));

            int left =
                amount;

            while (left > 0)
            {
                int chunk =
                    Math.Min(
                        stackMax,
                        left);

                Item item;

                try
                {
                    item =
                        factory.CreateItem(
                            ((IDatabaseIdGenerator)controller).NextId,
                            currency.TemplateId,
                            null);
                }
                catch (Exception ex)
                {
                    Complete(
                        completed,
                        false,
                        $"COULD NOT CREATE {currency.DisplayName}: {ex.Message}",
                        controller,
                        currency);

                    yield break;
                }

                if (item == null)
                {
                    Complete(
                        completed,
                        false,
                        $"COULD NOT CREATE {currency.DisplayName}",
                        controller,
                        currency);

                    yield break;
                }

                item.StackObjectsCount =
                    chunk;

                // Use EFT's native pickup placement flow. Factory-created
                // reward/refund items are parentless. PickUp includes the
                // IgnoreItemParent flag, preventing QuickFindAppropriatePlace
                // from calling GetNotMergedParent() on an unattached item.
                //
                // The resulting Move/Merge/Transfer operation is then committed
                // through TryRunNetworkTransaction. The previous direct
                // AddResult path is not a supported top-level network result.
                var operation =
                    ItemManipulator.QuickFindAppropriatePlace(
                        item,
                        controller,
                        new CompoundItem[]
                        {
                            controller.Inventory.Stash
                        },
                        ItemManipulator.EMoveItemOrder.PickUp,
                        simulate: true);

                if (operation.Failed)
                {
                    Complete(
                        completed,
                        false,
                        operation.Error?.ToString()
                            ?? $"NO VALID STASH LOCATION FOR {currency.DisplayName}",
                        controller,
                        currency);

                    yield break;
                }

                var task =
                    controller.TryRunNetworkTransaction(
                        operation);

                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.IsFaulted ||
                    task.IsCanceled)
                {
                    Complete(
                        completed,
                        false,
                        task.Exception?.GetBaseException().Message
                            ?? $"ADD {currency.DisplayName} TRANSACTION FAILED",
                        controller,
                        currency);

                    yield break;
                }

                var result =
                    task.Result;

                if (result == null ||
                    result.Failed)
                {
                    Complete(
                        completed,
                        false,
                        result?.Error
                            ?? $"ADD {currency.DisplayName} TRANSACTION REJECTED",
                        controller,
                        currency);

                    yield break;
                }

                left -=
                    chunk;
            }

            int finalBalance =
                CurrencyService.GetBalance(
                    controller,
                    currency);

            if (finalBalance !=
                startingBalance + amount)
            {
                Complete(
                    completed,
                    false,
                    $"BALANCE VERIFY FAILED ({finalBalance} != {startingBalance + amount})",
                    controller,
                    currency);

                yield break;
            }

            Complete(
                completed,
                true,
                null,
                controller,
                currency);
        }

        private static void Complete(
            Action<NativeCurrencyResult> completed,
            bool success,
            string error,
            InventoryController controller,
            CurrencyService.CurrencyDefinition currency)
        {
            completed?.Invoke(
                new NativeCurrencyResult
                {
                    Success =
                        success,
                    Error =
                        error,
                    Balance =
                        CurrencyService.GetBalance(
                            controller,
                            currency)
                });
        }
    }
}
