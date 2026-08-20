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

            // Validate the complete spend before committing the first stack.
            int validateLeft =
                amount;

            foreach (Item stack in
                     CurrencyService.GetStacks(
                             controller,
                             currency)
                         .OrderBy(
                             x => x.StackObjectsCount))
            {
                if (validateLeft <= 0)
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

                if (validateLeft >= count)
                {
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
                }
                else
                {
                    var validation =
                        ItemManipulator.SplitToNowhere(
                            stack,
                            validateLeft,
                            controller,
                            (IDatabaseIdGenerator)controller,
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
                }

                validateLeft -=
                    Math.Min(
                        validateLeft,
                        count);
            }

            if (validateLeft != 0)
            {
                Complete(
                    completed,
                    false,
                    "CURRENCY SPEND VALIDATION FAILED",
                    controller,
                    currency);

                yield break;
            }

            int left =
                amount;

            while (left > 0)
            {
                Item stack =
                    CurrencyService.GetStacks(
                            controller,
                            currency)
                        .OrderBy(
                            x => x.StackObjectsCount)
                        .FirstOrDefault(
                            x => x.StackObjectsCount > 0);

                if (stack == null)
                {
                    Complete(
                        completed,
                        false,
                        "CURRENCY STACK DISAPPEARED",
                        controller,
                        currency);

                    yield break;
                }

                int count =
                    stack.StackObjectsCount;

                if (left >= count)
                {
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

                    left -=
                        count;
                }
                else
                {
                    int splitCount =
                        left;

                    var operation =
                        ItemManipulator.SplitToNowhere(
                            stack,
                            splitCount,
                            controller,
                            (IDatabaseIdGenerator)controller,
                            simulate: true);

                    if (operation.Failed)
                    {
                        Complete(
                            completed,
                            false,
                            operation.Error?.ToString()
                                ?? "SPLIT OPERATION FAILED",
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
                                ?? "SPLIT TRANSACTION FAILED",
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
                                ?? "SPLIT TRANSACTION REJECTED",
                            controller,
                            currency);

                        yield break;
                    }

                    left =
                        0;
                }
            }

            int finalBalance =
                CurrencyService.GetBalance(
                    controller,
                    currency);

            if (finalBalance !=
                startingBalance - amount)
            {
                Complete(
                    completed,
                    false,
                    $"BALANCE VERIFY FAILED ({finalBalance} != {startingBalance - amount})",
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

                // Use EFT's native placement flow: create a simulated
                // QuickFindAppropriatePlace operation, then commit it through
                // TryRunNetworkTransaction. The previous direct AddResult path
                // was rejected by ConvertOperationResultToOperation().
                var operation =
                    ItemManipulator.QuickFindAppropriatePlace(
                        item,
                        controller,
                        controller.Inventory.Stash.ToEnumerable(),
                        ItemManipulator.EMoveItemOrder.UnloadAmmo,
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
