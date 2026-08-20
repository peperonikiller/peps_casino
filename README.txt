Pep Slot Machine - Phase 6 - SPT 4.1.2
===========================================

IMPORTANT
---------
BACK UP YOUR SPT PROFILE BEFORE TESTING.

Phase 5 changes the architecture:
- Client no longer tries to send AddResult / SplitToNowhereResult as unsupported backend operations.
- The SPT SERVER is authoritative for the spin, wager, outcome, payout, and final GP balance.
- Client receives the predetermined reel result and animates to it.
- Client mirrors the server's final GP count into the live Character inventory objects for immediate display.

Solution projects
-----------------
1. PepSlotMachine.Client
   BepInEx plugin / netstandard2.1

2. PepSlotMachine.Server
   SPT 4.1.2 C# server mod / net10.0
   NuGet:
     SPTarkov.Common 4.1.2
     SPTarkov.DI 4.1.2
     SPTarkov.Server.Core 4.1.2

Prerequisites
-------------
- Visual Studio 2026
- .NET 10 SDK
- SPT 4.1.2 at C:\SPT (change SptPath in both csproj files if needed)
- Internet access the first time the SERVER project restores its NuGet packages.

Build
-----
Open:
    PepSlotMachine.sln

Build Solution.

Client DLL post-build target:
    C:\SPT\BepInEx\plugins\PepSlotMachine\PepSlotMachine.dll

Server mod post-build target:
    C:\SPT\user\mods\PepSlotMachine.Server\

If your SPT 4.1.2 installation uses a different server mod directory,
copy the contents of:
    PepSlotMachine.Server\bin\Debug\PepSlotMachine.Server\
or Release equivalent
into your SPT server mods directory.

TEST ORDER
----------
1. BACK UP profile.
2. Build both projects.
3. Confirm server mod is in SPT server mods directory.
4. Start SPT server FIRST.
5. Server log should show the Pep Slot Machine Server mod loading.
6. Start launcher/game.
7. Go to Character -> Gear.
8. Press F6.
9. Start with BET = 1 GP.
10. Press SPIN.

Expected:
- Client briefly shows REQUESTING SERVER SPIN...
- Server log shows:
    Slot spin requested...
    Slot spin complete...
- Reels animate.
- GP balance changes exactly once.
- A win is already included in the new server balance; there is NO second payout transaction.

F12:
- Open / Close Slot Machine hotkey
- SPT Server URL (default https://127.0.0.1:6969)

PHASE 5 LIMITATION
------------------
This first server-authoritative test does not create brand-new GP stack Item records.
It rebalances the GP stacks already present in the PMC profile, with GP stack max
treated as 100. If a possible winning result would require more GP capacity than
the existing number of GP stacks can hold, the server rejects that spin BEFORE
taking the wager.

If you have 285 GP represented by 3 stacks, Phase 5 can safely represent balances
up to 300. For testing, 1 GP bets are recommended.

This limitation is intentional for the first server integration test. Once the
route/client handshake is proven, the next pass can add proper new-stack creation
through the server InventoryHelper.

If build fails:
- Paste the COMPLETE Visual Studio build output.
If the server starts but a spin fails:
- Paste Pep Slot Machine lines from both the BepInEx log and server log.


PHASE 5.1 FIXES
---------------
- Adds SPT HTTP headers:
    requestcompressed: 0
    responsecompressed: 0
  so the custom POST route uses plain JSON in both directions.

- Server project now builds to:
    PepSlotMachine.Server\bin\Debug\
  and copies directly to:
    C:\SPT\user\mods\PepSlotMachine.Server\

- Expected deployed DLL:
    C:\SPT\user\mods\PepSlotMachine.Server\PepSlotMachine.Server.dll

Before testing:
1. Delete any old PepSlotMachine.Server copy from the wrong folder.
2. Rebuild the full solution.
3. Fully restart SPT.Server.exe.
4. Launch EFT and test a 1 GP spin.


PHASE 5.2 PATH UPDATE
---------------------
Server mod deployment path updated for the newer SPT layout:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\

Expected deployed DLL after build:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\PepSlotMachine.Server.dll

The client DLL path remains:
    C:\SPT\BepInEx\plugins\PepSlotMachine\PepSlotMachine.dll


PHASE 5.3 FIXES
----------------
1. Winning cells are no longer highlighted before the reels stop.
   The server result is stored as pending state and is only revealed after the
   final reel has stopped. LAST WIN is also delayed until the stop completes.

2. GP now visibly changes during a spin:
   - When the server accepts the spin, the client immediately mirrors BALANCE-BET
     into the live GP stacks so the wager visibly disappears.
   - After the reels stop, the client mirrors the server's authoritative final
     balance, so winnings visibly appear only after the result is shown.

3. After changing live GP stack counts, the client invokes InventoryController's
   OnProfileUpdate backing event (when available) to force the Character inventory
   views to refresh.

Server deploy path remains:
   C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\

Client deploy path remains:
   C:\SPT\BepInEx\plugins\PepSlotMachine\


PHASE 5.4 LIVE GEAR REFRESH
---------------------------
The GP values were changing correctly, but EFT's already-visible GridItemView caches
the displayed stack count. InventoryController.OnProfileUpdate was not enough to
make that specific item view redraw.

After each GP StackObjectsCount change, the client now calls:
    stack.RaiseRefreshEvent(refreshIcon: false);

EFT itself uses Item.RaiseRefreshEvent when an item's visible state changes. This
should update the GP stack number immediately while the Gear tab stays open, without
tabbing away and back.

Server deploy path:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\
Client deploy path:
    C:\SPT\BepInEx\plugins\PepSlotMachine\


PHASE 6 - NATIVE SERVER GP STACK HANDLING
-----------------------------------------
The existing-GP-stack capacity limit has been removed.

Server side:
- Uses SPT InventoryHelper.AddItemToStash() when a payout needs another GP stack.
- Uses InventoryHelper.RemoveItem() when the authoritative balance needs fewer stacks.
- Existing stacks are updated and added to the ItemEventRouterResponse changed-items list.
- New GP item IDs use SPT's MongoId generator.
- New stacks are placed by SPT's stash placement helper rather than manually assigning X/Y slots.

Client side:
- Continues to use the server as the source of truth.
- Can now create/remove LOCAL mirror GP stacks without sending a second network
  transaction, so a newly-created payout stack can appear live while Gear remains open.
- RaiseRefreshEvent continues to update visible stack counts immediately.

Server deployment path:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\
Client deployment path:
    C:\SPT\BepInEx\plugins\PepSlotMachine\

TEST:
1. Back up profile.
2. Rebuild entire solution.
3. Fully restart SPT.Server.exe.
4. Test normal 1 GP spins.
5. Important capacity test: use a GP stack near 100 and get a win that pushes the
   balance above 100. A second GP stack should be created automatically.


TEST ODDS BUILD
---------------
This build is intentionally NOT balanced for normal play.

Testing changes:
- Common symbols are much more frequent.
- 45% of spins force at least one 3-symbol winning run.
- Forced wins are usually GP or DOGTAG.
- Some forced wins extend to 4 symbols.

Purpose:
- Quickly test server payout handling.
- Quickly test automatic creation of additional GP stacks.
- Quickly test live Gear-tab refresh after wins.

Do not use these odds as the final release balance.


PHASE 6.1 COMPILE FIX / NATIVE STACK TEST
-----------------------------------------
- Client ItemFactory is now explicitly EFT.ItemFactory.
- Server no longer references guessed Upd or AddItemDirectRequest type names.
- New GP stacks clone the exact existing GP profile-item model.
- InventoryHelper.AddItemToStash is resolved against the installed SPT 4.1.2
  server assembly at runtime, avoiding hard-coded request model names.
- If SPT exposes an incompatible stash-add signature, the server log prints all
  AddItemToStash overloads instead of crashing.
- High test odds remain enabled.

Server path:
  C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\


PHASE 6.2 - NEW STACK LIVE REFRESH
----------------------------------
Existing GP stack count changes already refresh live with Item.RaiseRefreshEvent.

A brand-new GP stack has no existing GridItemView for EFT to refresh. Phase 6.2
detects when the number of GP stacks changes and programmatically rebuilds the Gear
tab using InventoryScreen.ShowWithTab(EInventoryTab.Gear, false), which is equivalent
to the manual tab-away/tab-back workaround.

High testing odds remain enabled.

Server:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server\

Client:
    C:\SPT\BepInEx\plugins\PepSlotMachine\


PHASE 6.3 - REMOVE UNSAFE GEAR REOPEN
-------------------------------------
Phase 6.2 attempted to refresh a newly-created stack by calling InventoryScreen.ShowWithTab()
while the Character screen was already open. That re-entered the screen initialization path and
could leave EFT on the empty/incorrect inventory view shown in testing.

6.3 removes that completely. It NEVER changes tabs or reopens InventoryScreen automatically.
It keeps:
- live RaiseRefreshEvent updates for existing GP stack counts,
- local creation/removal of GP mirror stacks,
- server-authoritative GP profile updates,
- high testing odds.

After a new stack is created it now only performs safe inventory notifications:
- stash Grid.RevalidateSpaceBuffer()
- InventoryController.ReportProfileUpdate()

This build prioritizes not disrupting the Character screen. If the brand-new stack still does not
materialize visually until a tab change, the next step is to hook the Gear grid's own item-added UI
notification rather than reopening the entire InventoryScreen.


PHASE 6.4 - LIVE NEW-STACK UI EVENT TEST
----------------------------------------
No automatic tab switching is used.

The issue is now isolated to the UI event path:
- Existing GP stacks refresh correctly with Item.RaiseRefreshEvent().
- New GP stacks exist in the live inventory but the open stash does not create a
  GridItemView until EFT receives its normal item-added notification.

After a local mirror GP stack is added, Phase 6.4:
1. Calls RaiseRefreshEvent on the new GP item.
2. Locates InventoryController/ItemController's AddItemEvent backing delegate.
3. Dynamically matches its installed SPT 4.1.2 signature.
4. Emits a success/completed item-added notification to current subscribers.
5. Does not reopen or change the Character tab.

If the new stack still fails to appear, send the Pep Slot Machine BepInEx lines
around "AddItemEvent" so the exact installed event behavior can be targeted.


PHASE 7 - VISUAL / AUDIO POLISH
-------------------------------
Known limitation retained for now:
- A brand-new GP stack may require a manual stash refresh before its new GridItemView appears.
  Server/profile state remains correct.

Phase 7 changes:
- Reel symbols now physically scroll through the window instead of snapping once per tick.
- Compact Tarkov-inspired item cards replace plain centered text labels.
- Each reel uses cubic ease-out deceleration and a staggered mechanical stop.
- Winning highlights still remain hidden until every reel stops.
- Procedural spin tick, reel-stop, and win sounds are generated in code; no audio assets required.
- High testing odds remain enabled.


PHASE 7.1 BUILD FIX
-------------------
Fixes CS0104 in SlotMachineUI.cs:
    Random.Range(...)
is now:
    UnityEngine.Random.Range(...)

This removes the ambiguity introduced by using both System and UnityEngine.
The server nullable warning is unchanged and does not prevent the server build.


PHASE 7.3 - NATIVE EFT UI AUDIO
-------------------------------
Build fix: Phase 7.3 was regenerated from the known-good Phase 7.1 source so the
SlotMachineUI class braces/method boundaries remain intact.

Custom AudioSource/procedural clips are removed.

Native EFT UI sound mappings:
- Open: MenuInspectorWindowOpen
- Close: MenuInspectorWindowClose
- Spin press: ButtonClick
- Reel tick: ButtonOver
- Reel stop: MenuInstallMag
- Win: TradeOperationComplete

GUISounds is resolved once and cached. Audio now goes through:
    GUISounds.PlayUISound(EUISoundType)
which routes through EFT's own UI mixer.


PHASE 7.4 BUILD FIX
-------------------
GUISounds and EUISoundType are both in the EFT.UI namespace.

Added:
    using EFT.UI;

to SlotMachineUI.cs.

No other audio logic changed.


PHASE 8 - MULTI-PAYLINE WINS
----------------------------
Server:
- Evaluates every payline independently.
- The best 3+ adjacent run on each line pays.
- All winning lines are added into one total payout.
- Server returns lineWins[] with line number, symbol, match length, payout, and cells.

Client:
- Shows payout breakdown after the reels stop.
- Winning symbols pulse.
- LAST WIN counts upward.
- GP BALANCE counts upward from the post-bet amount to the final server balance.
- Winning highlights remain hidden until all reels have stopped.
- Native EFT TradeOperationComplete sound remains the win sound.

High testing odds remain enabled.
The deferred new-stack live stash refresh issue is unchanged.


PHASE 9 - REAL EFT ITEM ICONS
-----------------------------
Uses EFT.UI.DragAndDrop.ItemViewFactory.GetItemSpriteAsync() to preload and cache
vanilla inventory sprites for reel symbols.

Icon-backed symbols:
- GP Coin
- Roubles
- LEDX
- Physical Bitcoin
- Golden Star balm
- Labs access keycard
- Red Rebel
- Dogtag

The custom jackpot 7 and skull remain stylized text symbols.

Icons are loaded only the first time the slot machine opens. No icon generation is
performed per reel tick. If a template or icon fails to load, that symbol falls back
to the existing Phase 8 text/glyph card.

Phase 8 multi-payline wins, payout animation, native EFT sounds, high test odds, and
the deferred new-GP-stack live stash refresh limitation remain unchanged.


PHASE 9.1 BUILD FIX
-------------------
Fixes CS0165 in SlotMachineUI.cs by initializing the local Sprite reference before
it is passed through Dictionary.TryGetValue():

    Sprite sprite = null;

No Phase 9 icon behavior changed.


PHASE 10 / 1.0.0 - RELEASE ODDS + JACKPOT
------------------------------------------
The slot machine now defaults to a release odds profile instead of boosted testing odds.

F12:
- Test Odds = false by default.
  When enabled, restores the boosted development odds and 45% forced-win test path.
- Jackpot Enabled = true by default.

Release reel weights:
  GP          18%
  DOGTAG      16%
  SKULL       14%
  ROUBLES     13%
  GOLDSTAR    11%
  LABS         9%
  LEDX         7%
  BTC          5%
  RR           4%
  7            3%

Release 3-match payout multipliers:
  GP          2x
  DOGTAG      2x
  SKULL       3x
  ROUBLES     4x
  GOLDSTAR    5x
  LABS        7x
  LEDX       10x
  BTC        15x
  RR         25x
  7          45x

Four matches use 3x the listed multiplier.
Five matches use 10x the listed multiplier, except:
  7 x5 + Jackpot Enabled = 750x bet.

RTP:
An offline Monte Carlo simulation of the release symbol weights, five paylines, current
multi-line evaluator, and standard release payout table produced approximately 95.6%
return-to-player over 200,000 simulated spins. This is an estimate rather than a formal
closed-form proof, but is useful as the release balancing target.

Jackpot presentation:
- 5x 7 jackpot is identified separately by the server.
- Payout panel displays JACKPOT.
- Uses EFT's native QuestCompleted UI sound for the jackpot.
- Ordinary wins continue to use TradeOperationComplete.

Existing behavior retained:
- Server-authoritative wager/result/payout.
- Real EFT item icons.
- Animated reels.
- Multi-payline wins.
- Animated balance/win counter.
- Native EFT UI audio.
- Known newly-created-GP-stack live stash refresh issue remains deferred.


PHASE 10.2 / 1.0.2 - STACK REGRESSION FIX
------------------------------------------
This build intentionally rolls back the Phase 10.1 manual AddItemEvent replay.

Why:
ItemManipulator.Add/Remove already perform real inventory mutations. Replaying the
controller's private AddItemEvent after the add caused the open inventory UI to end up
with duplicate/stale item-view state. That could make old GP stacks appear to remain
after later wagers/removals.

10.2 behavior:
- Uses the known-stable Phase 10 inventory mirror as the base.
- New local GP stack: ItemManipulator.Add only.
- Removed local GP stack: ItemManipulator.Remove only.
- Existing stack count changes: Item.RaiseRefreshEvent(false).
- No private AddItemEvent delegate is invoked manually.
- Adds logging when a local GP stack is added or removed.
- Odds profile is now shown in the slot UI, removing the unused-field warning.

Known UI limitation restored:
A brand-new GP stack may not become visible in the already-open Gear stash until the
stash UI is refreshed manually. This is preferable to corrupting/staling the visible
stack list. The server/profile remains authoritative.

The next proper fix for live new-stack display requires the exact Gear GridView
item-added subscription/handler, not replaying ItemController.AddItemEvent.


PHASE 10.3 / 1.0.3 - NATIVE ADD/REMOVE RESULT EVENTS
-----------------------------------------------------
This fixes the missing lifecycle step for GP stack topology changes.

SPT 4.1.2 ItemManipulator.Add/Remove mutate the container immediately and return
AddResult / RemoveResult objects. Those result objects expose:

    RaiseEvents(IItemOwner itemController, CommandStatus status)

Phase 10.3 now calls:

    add.Value.RaiseEvents(controller, CommandStatus.Succeed);

after a successful local mirror add, and:

    removeResult.Value.RaiseEvents(controller, CommandStatus.Succeed);

after a successful local mirror removal.

This is different from the failed 10.1 experiment:
- 10.1 manually invoked ItemController._addItemEvent with fabricated event args.
- 10.3 lets EFT's own AddResult/RemoveResult raise their complete native event chain,
  including the underlying ContainerAddResult/ContainerRemoveResult events.

Expected behavior:
- Existing GP stack count changes continue to refresh live.
- Crossing upward into a new GP stack should add its GridItemView live.
- Crossing downward and deleting a GP stack should remove its GridItemView live.
- No tab switching/manual stash refresh should be required.


PHASE 10.4 / 1.0.4 - REAL EFT GRIDVIEW LIFECYCLE
-------------------------------------------------
SPT 4.1.2 GridView creates a new item view during CommandStatus.Begin. Succeed only
finalizes the view that Begin already created.

New GP stack:
    ItemManipulator.Add(..., simulate:true)
    AddResult.RaiseEvents(controller, Begin)
    AddResult.Execute()
    AddResult.RaiseEvents(controller, Succeed)

Removed GP stack:
    ItemManipulator.Remove(..., simulate:true)
    RemoveResult.RaiseEvents(controller, Begin)
    RemoveResult.Execute()
    RemoveResult.RaiseEvents(controller, Succeed)

Failed is emitted if Execute() fails.

Existing GP stack count refreshes are unchanged.


PHASE 11A / 1.1.0 - CASINO FOUNDATION
---------------------------------------
This is the first architecture pass for Fika/shared casino support.

Currency handling:
- GpInventoryService.cs has been replaced by generic CurrencyService.cs.
- Currency definitions now exist for GP and Roubles.
- Slot machine uses CurrencyService.Gp.
- Blackjack can use CurrencyService.Roubles without duplicating inventory logic.
- Client currency stack size is read dynamically from Item.StackMaxSize.
- If the player has no existing stack, a temporary ItemFactory item is created only to
  read its current StackMaxSize. It is never inserted into inventory.
- The client sends the observed stack max to the server with the wager request.
- ServerCurrencyService.cs owns server-side add/remove/rebalance logic for any currency.
- No GP=100 assumption remains in the currency service.

Why this matters:
Players using stack-size mods are no longer forced into vanilla GP/Rouble stack sizes.
The same generic add/remove lifecycle used by Phase 10.4 remains intact for live UI
topology changes.

Fika architecture selected:
The casino will be server-authoritative and shared through SPT HTTP routes rather than
raid-local Fika packets. This keeps the casino usable out of raid and gives every client
connected to the same SPT/Fika server one shared jackpot and blackjack room registry.

Next phases:
11B - persistent global progressive GP jackpot + live counter
12A - Casino window tabs (Slots / Blackjack)
12B - Blackjack authoritative room system, solo host, join-anytime, max 5 players
12C - multiplayer turn/state polling, reconnect/leave handling, Rouble wagers


PHASE 11A FIX 1
---------------
Server CurrencyService.cs now imports:

    SPTarkov.Server.Core.Helpers.Profile

InventoryHelper in SPT 4.1.2 is resolved through the helper namespaces used by the
previous known-good SlotServerMod build. No currency behavior changed.


PHASE 11B / 1.1.1 - SHARED JACKPOT + CASINO VISUAL PASS
--------------------------------------------------------
- UI label is simply JACKPOT.
- Shared server-owned jackpot starts at 500 GP.
- Every successful slot wager contributes 100% of its GP bet.
- 5x7 pays the shared pool in addition to the normal line payout, then resets to 500 GP.
- Shared state is locked server-side and persisted at:
    <SPT runtime>/user/mods/PepSlotMachine/jackpot.json
- If the GP inventory update fails, the jackpot state is not committed/reset.
- Clients poll the shared state every 2 seconds so Fika players see other wagers.
- Casino visual pass adds a dark red/black cabinet, gold trim, Pep's Casino marquee,
  and a large centered JACKPOT counter.

Next: SLOTS / BLACKJACK tabs and server-authoritative Rouble blackjack rooms.


PHASE 12A / 1.2.0 - CASINO TABS + BLACKJACK LOBBY FOUNDATION
-------------------------------------------------------------
UI:
- Pep's Casino now has two tabs:
    SLOTS
    BLACKJACK
- Slots keep the shared JACKPOT and existing slot machine behavior.
- Blackjack shows live Rouble balance through the generic CurrencyService.
- Blackjack lobby supports:
    Host Table
    Browse hosted tables
    Join a table
    Leave a table
    Up to 5 players
- Hosted tables show host name, room ID and player count.

Server:
- New server-authoritative BlackjackRoomService.
- Shared room registry lives on the SPT server, making it suitable for Fika clients.
- Host creates a room and occupies seat 1.
- Players can join at any time while fewer than 5 players are present.
- Host migration occurs automatically if the host leaves.
- Empty rooms close automatically.
- Inactive rooms are cleaned after 30 minutes.
- Routes added:
    /pep-casino/blackjack/lobby
    /pep-casino/blackjack/host
    /pep-casino/blackjack/join
    /pep-casino/blackjack/leave

This phase intentionally does NOT deal cards yet.
Phase 12B will add the real server-authoritative blackjack game:
- Rouble wagers
- Solo play vs AI dealer
- Dealer shoe/deck
- Deal / hit / stand / double
- Blackjack / bust / push / dealer resolution
- Server-side payouts through CurrencyService


PHASE 12A FIX 1 / 1.2.1
------------------------
Build fixes:
- Added `using System.Linq;` to SlotMachineUI.cs for Take() and OrderBy().
- Replaced the nonexistent `_panelStyle` blackjack lobby box style with the existing
  `_buttonStyle` style.
- No blackjack lobby/server behavior changed.


PHASE 12A FIX 2 / 1.2.2
------------------------
Fixed the Visual Studio server deployment path.

Previous incorrect output folder:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server

Correct output folder:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine

The SPT server mod loader was discovering the PepSlotMachine folder while the compiled
DLL was being copied into PepSlotMachine.Server, resulting in:

    No Assemblies found in path: ...\user\mods\PepSlotMachine

Before testing this build:
1. Delete any stale folders:
       C:\SPT\SPT_Runtime\user\mods\PepSlotMachine
       C:\SPT\SPT_Runtime\user\mods\PepSlotMachine.Server
2. Rebuild the Server project.
3. Confirm this file exists:
       C:\SPT\SPT_Runtime\user\mods\PepSlotMachine\PepSlotMachine.Server.dll

The client DLL continues to deploy to:
    C:\SPT\BepInEx\plugins\PepSlotMachine\PepSlotMachine.dll


PHASE 12A FIX 3 / 1.2.3
------------------------
Fixed HTTP 404 responses for the Blackjack lobby routes.

Root cause:
BlackjackRoomService.cs and the client blackjack HTTP methods were present in the
Phase 12A project, but SlotServerMod.cs in the packaged project never actually
registered the blackjack routes with SlotStaticRouter.

The server now explicitly registers:
    /pep-casino/blackjack/lobby
    /pep-casino/blackjack/host
    /pep-casino/blackjack/join
    /pep-casino/blackjack/leave

SlotStaticRouterCallback now also injects BlackjackRoomService and includes the
corresponding lobby/host/join/leave handlers.

Server metadata/project version bumped to 1.2.3.
Server build output remains:
    C:\SPT\SPT_Runtime\user\mods\PepSlotMachine


PHASE 12A FIX 4 / 1.2.4
------------------------
Fixed the server compile failure:

    SlotStaticRouterCallback does not contain a definition for HandleBlackjackLobby
    HandleBlackjackHost
    HandleBlackjackJoin
    HandleBlackjackLeave

Root cause:
Fix 3 correctly registered the four Blackjack routes and injected
BlackjackRoomService, but its generation check saw "HandleBlackjackLobby" in the
router call and mistakenly assumed the callback method itself already existed.

Fix 4 explicitly adds all four callback methods to SlotStaticRouterCallback.

The warning:
    Parameter 'blackjackRoomService' is unread
will also disappear because the callback now actually uses that service.

No lobby behavior or routes changed.


PHASE 12A FIX 5 / 1.2.5
------------------------
Fixed the client compile error involving UnityEngine.Networking.CertificateHandler.

PepSlotMachine.Client.csproj now permanently references:
    UnityEngine.UnityWebRequestModule.dll
    UnityEngine.UnityWebRequestAudioModule.dll
    UnityEngine.AudioModule.dll

The project now defines:
    SptPath        = C:\SPT
    EftManagedPath = $(SptPath)\EscapeFromTarkov_Data\Managed
    BepInExPath    = $(SptPath)\BepInEx

References use HintPath entries with Private=false so Visual Studio no longer needs
the Reference Manager checkboxes each time and EFT/Unity DLLs are not copied into the
plugin output.


PHASE 12B / 1.3.0 - PLAYABLE SOLO BLACKJACK
---------------------------------------------
- Server-authoritative six-deck shoe and card order.
- Rouble wagers use generic CurrencyService and dynamic stack limits.
- Dealer hits below 17 and stands on 17.
- Blackjack pays 3:2; normal win 1:1; push returns wager.
- HIT, STAND, DOUBLE, DEAL and NEW HAND are playable.
- Dealer hole card is hidden until resolution.
- This phase targets solo host-vs-dealer validation first.
- Phase 12C will harden up-to-five-player Fika turn synchronization and add native EFT voice taunts on losing hands.


PHASE 12B FIX 1 / 1.3.1
------------------------
ROOM NOT FOUND:
The Blackjack lobby/host endpoints and the Blackjack gameplay endpoints are registered
through different server callback classes. Both inject BlackjackRoomService.

The previous room registry was stored on each BlackjackRoomService instance:
    private readonly Dictionary<...> _rooms

That allowed HOST to create a table in one service instance while BET/ROOM looked for it
in another instance and returned ROOM NOT FOUND.

The table registry and lock are now static/process-wide:
    private static readonly object _sync
    private static readonly Dictionary<...> _rooms

All Blackjack routes on the SPT server now use the same room state. This is also the
correct architecture for shared Fika tables.

BUTTON TEXT:
The general casino _buttonStyle uses a 26px font, which was too large for the compact
Blackjack controls. Blackjack now has its own 13px centered button style.

Adjusted:
- HOST TABLE
- PLACE BET
- DEAL
- NEW HAND
- HIT
- STAND
- DOUBLE
- LEAVE TABLE

Longer controls also received slightly wider button rectangles.


PHASE 12C / 1.4.0
-----------------
Visual:
- Dealer cards: 96x116.
- Player cards: 68x58.
- Blackjack status: 19px bold centered.
- All occupied seats render on the table.

Rules review:
Blackjack has no single universal official ruleset; casinos publish table-specific
variations. Pep's Casino now explicitly uses:
- 6 decks
- Dealer stands on all 17s (S17)
- Blackjack pays 3:2
- Normal win pays 1:1
- Push returns wager
- Dealer hole card hidden during player turns
- Dealer blackjack checked before player actions
- Double on any initial two-card hand
- Double draws one card and stands
- Minimum bet ₽1,000

Not yet implemented: splits, insurance/even money, surrender.

Corrections:
- Dealer natural resolves before player actions.
- Player blackjack vs dealer blackjack pushes.
- Natural blackjack pays stake + 3:2 profit.
- Host alone controls DEAL / NEW HAND.

Multiplayer/Fika:
- Up to five ready players are dealt into the same hand.
- Server preflights all ready players' Rouble balances before taking wagers.
- Turn order advances seat-by-seat.
- Only active seat can HIT/STAND/DOUBLE.
- Dealer resolves after the last active player.
- Server pays every winning seat through CurrencyService.
- Joining during an active hand enters the following hand.

Native EFT loss taunts remain queued for the audio pass after mapping EFT's real voice
phrase API in Assembly-CSharp; no guessed reflection/audio hook was added.


PHASE 12C FIX 1 / 1.4.1
------------------------
Fixed server compile error CS0111:

    BlackjackRoomService already defines a member called TryPlayer
    with the same parameter types.

The Phase 12C generation pass inserted the new multiplayer TryPlayer helper but left the
older helper in the class. Fix 1 removes the duplicate definition only.

No Blackjack rules, multiplayer behavior, UI sizing, payouts, or room logic changed.


PHASE 12C FIX 2 / 1.4.2
------------------------
Fixed the server compile errors caused by BlackjackPlayerState missing TurnComplete.

Added:
    public bool TurnComplete { get; set; }

Also ensured:
- ResetHand() clears TurnComplete.
- CloneRoom() copies TurnComplete.
- Client DTO includes turnComplete for synchronized multiplayer state.

No rules, payouts, card rendering, status sizing, or turn-order behavior changed.


PHASE 12C FIX 3 / 1.4.3
------------------------
- Fixed cut-off current-player text by replacing the oversized slot win style with
  dedicated Blackjack player/turn styles and larger label rectangles.
- Blackjack server responses now carry the requesting player's authoritative Rouble
  balance.
- Room polling mirrors that server balance into the live Character/Gear inventory, so
  wagers and payouts visibly update without leaving/re-entering the Gear tab.
- Action responses mirror immediately as well.
- Each player now records their own live Rouble StackMaxSize when betting. Wager
  deductions, doubles and payouts use that player's stack limit, preserving stack-limit
  mod compatibility in multiplayer.


PHASE 12C FIX 4 / 1.4.4
------------------------
Fixed the DEAL error:

    SERVER DID NOT RETURN CREATED RUB STACK

ROOT CAUSE
ServerCurrencyService previously recalculated the entire balance as:

    requiredStacks = targetBalance / reportedStackMax

That is unsafe with currency stack-limit mods. A player's existing Rouble stacks can
have a topology/capacity that does not match the StackMaxSize currently reported by the
client. Even a simple ₽10,000 deduction could therefore make the old code think it
needed to CREATE more Rouble stacks.

FIX
Currency changes are now delta-based instead of rebuilding the whole currency layout.

SPENDING / DECREASE:
- Compute only the amount being removed.
- Subtract from existing stacks from the end.
- Remove a stack only if it becomes fully consumed.
- NEVER create a stack while deducting currency.
- Existing oversized stacks are left valid.

PAYOUT / INCREASE:
- Fill existing stacks first.
- Preserve any existing stack already larger than reported StackMaxSize.
- Create a new stack only for genuine overflow.

SPT ADD TIMING:
After InventoryHelper.AddItemToStash succeeds, the service no longer fails merely
because the new item is not immediately visible in pmc.Inventory.Items. Some SPT
helper paths expose the created item through the item-event output before the profile
list reflects it.

This fix applies to both GP and Rouble currency operations.


PHASE 12C FIX 5 / 1.4.5
------------------------
Fixed Blackjack STAND request timing out after ~6 seconds.

ROOT CAUSE
Phase 12C added CurrencyStackMax to each Blackjack seat, but SetBet() did not actually
copy the client's reported value into the player state because the generated assignment
patch missed the spaced source line.

That left:
    CurrencyStackMax = 1

for every Blackjack player.

Spending still worked because Fix 4's delta-based deduction never creates stacks.
However, when STAND ended a winning hand, payout code saw stackMax=1. Existing Rouble
stacks therefore appeared "full", and a ₽20,000 win could try to create approximately
20,000 one-Rouble stacks. The HTTP request then exceeded the client's 6-second timeout,
which caused the SPT request cancellation shown in the log.

FIX
SetBet() now always records:
    p.CurrencyStackMax = Math.Max(1, currencyStackMax);

CurrencyService also has a defensive fallback:
    effective stack max >= largest existing stack count

so a bad caller value can never explode a normal payout into thousands of tiny stacks.

The Blackjack HTTP timeout was increased from 6 to 15 seconds as additional headroom,
but the stack-max bug above is the actual timeout fix.

No Blackjack rules or payout percentages changed.


PHASE 12D / 1.5.0
------------------
This pass intentionally does NOT depend on multiplayer testing.

ROUND LIFECYCLE
- A resolved hand remains on screen for 5 seconds so the result can be read.
- The server then automatically returns the table to PLACE YOUR BETS on room polling.
- Cards, results, ready state and old wagers are cleared for the next hand.
- The player remains seated at the same table.
- Manual NEW HAND is no longer required in the normal UI.
- The lifecycle is server-authoritative so it is structured for later Fika testing.

PRESENTATION
- Added explicit PLACE YOUR BET / YOUR TURN / WAITING FOR PLAYER / DEALER TURN /
  HAND COMPLETE state text.
- Added a large end-of-hand result banner.
- Win and natural Blackjack banners show net profit.
- Push explicitly says the wager was returned.
- Loss shows the wager lost.
- Dealer hole card remains hidden only during the player phase and is revealed for
  dealer resolution/results.
- Player/card spacing was retained from the previous clipping fixes.
- Controls are disabled while a request is pending or when the action is illegal.

EDGE-CASE HARDENING
- DOUBLE now validates room phase, active seat, two-card requirement, completed/bust
  state and available Roubles BEFORE deducting the second wager.
- This fixes the dangerous case where an invalid DOUBLE request could remove money
  before BlackjackRoomService rejected the action.
- DEAL now validates the room phase before wager deductions.
- Hitting exactly 21 automatically completes the player's turn.
- Natural Blackjack, dealer natural Blackjack, push, player bust, dealer bust and
  double-down resolution remain server-authoritative.
- Current rules remain: six decks, dealer stands on soft 17, Blackjack pays 3:2,
  normal win pays 1:1, push returns wager, double on any initial two cards.

NOT YET INCLUDED
- Fika 2-5 player stress testing (deferred until another tester is available).
- Tarkov voice taunts. We should wire those after identifying the exact EFT 1.4.12
  phrase/voice API in the user's current Assembly-CSharp so we do not guess.
- Split / insurance / surrender.


PHASE 12D VOICE PASS / 1.5.1
-----------------------------
NATIVE EFT BLACKJACK LOSS TAUNTS

Uses the exact current Assembly-CSharp API supplied during development:

    EFT.Player.Say(
        EPhraseTrigger phrase,
        bool demand = false,
        float delay = 0f,
        ETagStatus mask = 0,
        int probability = 100,
        bool aggressive = false)

A native voice line is attempted exactly once when the local Blackjack hand transitions
into Result=LOSE.

Randomized native phrase pool:
    Toxic
    Provocation
    BadWork
    Negative
    OnMutter

The call uses:
    demand: true
    probability: 100
    aggressive: true

No taunt occurs for:
    WIN
    BLACKJACK
    PUSH

PLAYER LOOKUP
BlackjackVoiceService searches loaded EFT.Player objects and prefers one whose reflected
Profile/ProfileId matches the Character inventory controller. It then falls back to a
sole loaded Player or IsYourPlayer=true.

This keeps the voice call strongly typed while avoiding compile-time dependence on
profile identity members that may move/rename between EFT builds.

The trigger is checked on both:
- direct Blackjack action responses
- normal shared-room polling

A per-hand key based on ResolvedUtc prevents the same loss from speaking repeatedly while
the completed hand remains visible for the five-second result period.

If the Character screen has no loaded EFT.Player/Speaker object, the mod logs:
    Blackjack loss taunt skipped: no loaded local EFT.Player/Speaker was found.

That log will tell us whether the Character preview environment exposes a usable native
speaker. If it does not, the next fallback is to route through the menu's player-owner /
preview speaker object rather than bundling custom audio.


PHASE 12D VOICE FIX 1 / 1.5.2
-------------------------------
Fixed client compile error:

    InventoryController could not be found

BlackjackVoiceService.cs now imports:

    using EFT.InventoryLogic;

No voice behavior, Blackjack rules, round lifecycle, or server logic changed.


PHASE 12D VOICE FIX 2 / 1.5.3
-------------------------------
The previous build confirmed that the Character screen does not expose a loaded
EFT.Player object.

This build uses the BaseSpeaker implementation supplied from the current
Assembly-CSharp.

IMPORTANT DETAIL
BaseSpeaker.Play() is NOT used from the menu because it accesses:

    Singleton<GameWorld>.Instance.SpeakerManager

which is an in-raid dependency.

Instead the Character-preview path uses:

    BaseSpeaker.PlayDirectRandom(EPhraseTrigger)

PlayDirectRandom uses the speaker's already-loaded PhrasesBanks and raises the
speaker's OnPhraseTold event without requiring GameWorld/SpeakerManager.

PREVIEW SPEAKER DISCOVERY
BaseSpeaker is not a UnityEngine.Object, so Resources.FindObjectsOfTypeAll<BaseSpeaker>()
cannot be used.

BlackjackVoiceService now:
1. Enumerates loaded UnityEngine.Object instances.
2. Reflects their fields/properties whose type derives from BaseSpeaker.
3. Finds an initialized speaker with a PlayerVoice and a compatible phrase bank.
4. Calls PlayDirectRandom() for one randomized loss taunt.

Phrase pool remains:
    Toxic
    Provocation
    BadWork
    Negative
    OnMutter

If no usable preview speaker is found, the previous EFT.Player.Say() path remains as a
fallback for environments where a real Player is loaded.

Expected success log:
    Found Character preview speaker: voice=<voice>, banks=<count>
    Blackjack preview-speaker loss taunt: Toxic (<voice>)

Expected failure log:
    Blackjack loss taunt skipped: no usable Character preview BaseSpeaker or EFT.Player was found.


PHASE 12D VOICE FIX 3 / 1.5.4
-------------------------------
Uses the newly supplied EFT.UI.GUISounds.PlaySound API as the Character-menu playback
path:

    GUISounds.PlaySound(
        AudioClip clip,
        bool single = false,
        bool commonUiSound = false,
        float volume = 1f)

WHY THIS PATH
BaseSpeaker.Play() requires GameWorld.SpeakerManager and is therefore unsafe in the
Character menu.

BaseSpeaker.PlayDirectRandom() avoids GameWorld, but it only raises BaseSpeaker's
OnPhraseTold event. If the Character preview does not have its normal speaker listener
wired in this UI state, no actual AudioSource plays the clip.

Fix 3 now:
1. Finds the initialized Character-preview BaseSpeaker.
2. Selects one randomized native TagBank/TaggedClip for the taunt phrase.
3. Resolves the actual UnityEngine.AudioClip from TaggedClip.
   - The AudioClip resolver is reflective because the supplied TaggedClip member layout
     has not yet been decompiled.
   - It checks obvious Clip/AudioClip members first, then recursively inspects nested
     fields/properties up to depth 3.
4. Finds the same EFT.UI.GUISounds object already used by Pep's Casino slot sounds.
5. Plays the native voice AudioClip through:
       PlaySound(
           clip,
           single: true,
           commonUiSound: true,
           volume: 1f)

This route is menu-safe and uses EFT's existing UI audio sources/mixer rather than
creating or bundling a custom AudioSource.

Expected success log:
    Blackjack GUISounds loss taunt: Toxic (<voice>, clip=<clip name>)

If a TaggedClip is found but its AudioClip cannot be located:
    Blackjack taunt bank Toxic produced a TaggedClip but no AudioClip member could be resolved.

If that happens, decompiling TaggedClip is the only remaining information needed to
replace the reflective clip lookup with the exact member.


PHASE 12D VOICE FIX 4 / 1.5.5
-------------------------------
TaggedClip was decompiled and confirmed to contain:

    public AudioClip Clip;
    public float Volume;

The reflective AudioClip search from Fix 3 has been removed.

BlackjackVoiceService now uses the exact native members:

    AudioClip audioClip = taggedClip.Clip;

and:

    float volume =
        taggedClip.Volume > 0f
            ? taggedClip.Volume
            : 1f;

The clip is then played through EFT.UI.GUISounds.PlaySound(...).

This removes the last guessed part of the menu voice playback path.

Expected success log:
    Blackjack GUISounds loss taunt: Toxic (<voice>, clip=<clip name>)

If the clip resolves but no audio is heard, the next thing to inspect is whether the
Character-preview BaseSpeaker itself is being found and which voice/banks it contains.


PHASE 12D VOICE FIX 5 / 1.5.6
-------------------------------
The previous test proved the Character screen has neither a discoverable EFT.Player nor
a discoverable BaseSpeaker.

This build no longer requires either one.

DIRECT PROFILE VOICE PATH
EFT.BaseSpeaker.Init() showed the exact native asset-loading path:

    Singleton<IEasyAssets>.Instance.GetAsset<Voice>(
        InGameBundles.TakePhrasePath(playerVoice))

BlackjackVoiceService now reads the current Character profile's voice identifier/name
from the InventoryController.Profile (preferring Info.Voice, with reflective fallbacks),
loads that native Voice asset directly, chooses the requested TagBank and TaggedClip,
then plays TaggedClip.Clip through the already-working EFT GUISounds menu AudioSource.

No GameWorld, Player, or BaseSpeaker instance is required for this primary path.

Lookup attempts these profile members:
    Profile.Info.Voice
    VoiceId / VoiceID
    VoiceName
    PlayerVoice
    and equivalent direct Profile / Customization members

Expected logs:
    Resolved Blackjack profile voice from <type>.Voice: <voice>
    Blackjack profile-voice loss taunt: Toxic (<voice>, clip=<clip>)

If voice lookup fails:
    Blackjack could not resolve a voice id/name from the Character profile.

If the voice resolves but no requested phrase bank contains clips:
    Blackjack profile voice '<voice>' loaded, but none of the selected taunt banks
    contained playable clips.

The older BaseSpeaker and EFT.Player paths remain as fallbacks.


PHASE 12D VOICE FIX 6 / 1.5.7
-------------------------------
The supplied EFT.PhraseSounds class gives us a fallback that no longer depends on the
Character profile exposing an exact Voice string.

PhraseSounds contains:
    public Voice[] Voices;

and each Voice exposes:
    Voice.Name
    Voice.Banks

New primary fallback after exact profile-voice lookup:
1. Resolve the PMC side/faction from Profile.Info.Side / Profile.Side when available.
2. Find loaded PhraseSounds assets.
3. Select a native Voice whose Name begins with "Usec" or "Bear" for that side.
4. If side cannot be resolved, select from any native USEC/BEAR voice rather than a
   Scav/boss voice.
5. Find one of the native loss-taunt TagBanks.
6. Select TaggedClip.Clip.
7. Play it through EFT.UI.GUISounds.PlaySound.

Expected success:
    Resolved Blackjack profile side from <type>.Side: Usec
    Blackjack PhraseSounds loss taunt: Toxic (side=Usec, voice=Usec_..., clip=...)

This means exact Character voice matching is still preferred when available, but a
missing profile voice identifier no longer prevents native EFT taunts from playing.

If PhraseSounds itself is not loaded:
    Blackjack PhraseSounds fallback: no PhraseSounds asset is loaded.


PHASE 12D VOICE FIX 7 / 1.5.8
-------------------------------
The previous test showed PhraseSounds is not loaded in the Character menu.

The supplied PhraseSounds.GetVoice() implementation also revealed EFT's built-in
fallback voice names:

    USEC -> "Usec_1"
    BEAR -> "Bear_1"
    SCAV -> "Scav_1"

Fix 7 no longer requires PhraseSounds to be loaded.

After exact profile voice and PhraseSounds lookup fail, Blackjack now directly loads
EFT's stock PMC Voice assets through the same native path BaseSpeaker.Init uses:

    Singleton<IEasyAssets>.Instance.GetAsset<Voice>(
        InGameBundles.TakePhrasePath("Usec_1"))

or Bear_1.

If the Character profile side can be resolved, that faction is tried first. If side is
also unavailable in the menu, Usec_1 and Bear_1 are tried in sequence. Scav_1 is not
used for the Blackjack PMC taunt fallback.

The selected native TaggedClip.Clip is still played through EFT GUISounds.

Expected success:
    Blackjack stock-voice loss taunt: Toxic
        (side=unknown, voice=Usec_1, clip=<clip>)

This path requires neither:
- profile voice id
- PhraseSounds asset
- BaseSpeaker
- EFT.Player
- GameWorld


PHASE 12E / 1.6.0 - TABLE-WIDE SYNCHRONIZED TAUNTS
---------------------------------------------------
This build prepares Blackjack loss taunts for Fika/shared-table playback without
requiring another player for the current test pass.

SERVER-AUTHORITATIVE TAUNT EVENTS
When a hand resolves, the server creates one BlackjackTauntEvent for EACH losing player.

Each event contains:
    EventId
    LosingProfileId
    LosingDisplayName
    Phrase
    VoiceName
    DelaySeconds
    CreatedUtc

All clients polling the same Blackjack room receive the same event data.

RANDOM STAGGER
Each loser receives an independently randomized delay from:
    0.0 to 4.0 seconds

This means multiple losing players do not all speak at the instant the dealer resolves.
Some overlap remains possible by design, but the timing is more conversational/natural.

CONSISTENT AUDIO ON EVERY CLIENT
Phrase, fallback voice and delay are selected once on the server.

The native clip index is selected deterministically from EventId on each client, so all
clients choose the same TaggedClip from the same Voice/TagBank.

Each table client therefore receives:
    same loser
    same phrase
    same voice
    same native clip
    same 0-4 second delay

EVENT DE-DUPLICATION
Each client keeps a SeenTableEvents set. Room polling may return the same resolved hand
multiple times, but each EventId is scheduled only once per client.

AUDIO OVERLAP
Synchronized table taunts call GUISounds.PlaySound with:
    single: false

so two staggered loser taunts are allowed to overlap if their random delays land close
together, matching the requested behavior.

CURRENT REMOTE-VOICE LIMITATION
The Character/menu profile currently does not expose another player's actual equipped
voice identifier. Until Fika multiplayer testing gives us a better identity source, the
server assigns each profile a stable Usec_1 or Bear_1 stock PMC voice based on profile id.

That stock assignment is stable and, crucially, identical for every connected table
client. Once remote PMC side/voice data is available, VoiceName can be populated with
the real remote voice without changing the synchronization system.

TESTING WITHOUT ANOTHER PLAYER
Solo losses should now log:
    Scheduled table taunt ... delay=X.XXs
followed 0-4 seconds later by:
    Played synchronized table taunt ...

A solo hand creates exactly one event. The multi-loser staggering logic is already in
place but can be validated with real Fika players later.


PHASE 12E FIX 1 / 1.6.1
------------------------
Fixed synchronized taunts failing when the server-selected phrase does not exist on the
selected stock voice.

Observed example:
    phrase=Toxic
    voice=Usec_1
    "voice Usec_1 has no playable Toxic bank"

EFT voice packs do not all contain the same EPhraseTrigger banks.

NEW BEHAVIOR
The client first tries the synchronized server-selected phrase. If that Voice does not
contain a playable bank for it, it scans this casino-appropriate fallback set:

    Provocation
    BadWork
    Negative
    OnMutter
    Toxic

Only banks actually present on that Voice and containing clips are eligible.

The fallback choice remains synchronized:
- every table client has the same VoiceName
- every table client has the same EventId
- the playable candidate order is fixed
- EventId deterministically chooses the fallback bank
- EventId + selected phrase deterministically chooses the TaggedClip

Therefore all players at the table still hear the same native line for the event.

The original randomized 0-4 second per-loser stagger is unchanged.


PHASE 12F / 1.7.0 - QUIET REQUESTS + CASINO CAREER
---------------------------------------------------
REQUEST/LOG CLEANUP
- Removed Pep's own routine slot-spin request/completion info logs.
- Removed routine client spin-complete and GUISounds success logs.
- Removed routine synchronized-taunt scheduling/playback info logs.
- Warnings and errors remain.

JACKPOT REQUEST SPAM
The old 2-second /pep-casino/jackpot poll is removed.

Jackpot state now refreshes only:
- when Pep's Casino opens
- when the SLOTS tab is selected
- through every normal slot-spin response

This removes the constant SPT "[Client Request] /pep-casino/jackpot" spam.

Blackjack room/lobby polling remains only while the BLACKJACK tab is active because it is
required for shared Fika table state and synchronized taunts.

CASINO CAREER STATS
Added a STATS tab with server-persistent, per-profile career statistics.

Stored at:
    user/mods/PepSlotMachine/casino_stats.json

Slots:
    spins
    GP wagered / returned / net
    biggest slot return
    jackpots won
    biggest jackpot

Blackjack:
    hands
    wins / losses / pushes
    natural blackjacks
    roubles wagered / returned / net
    biggest single-hand blackjack profit

Stats update server-side after successful slot transactions and resolved Blackjack hands.
The STATS tab refreshes on entry instead of polling continuously.


PHASE 12F FIX 1 / 1.7.1
------------------------
Fixed client compile error:

    CS0103: The name 'CreateRequest' does not exist in the current context

The new casino-stats request accidentally referenced a helper that this client class
does not have.

GetStats() now uses the same explicit UnityWebRequest setup already used by GetJackpot():
- POST
- UploadHandlerRaw
- DownloadHandlerBuffer
- LocalCertificateHandler
- content/compression headers
- PHPSESSID cookie
- 15 second timeout

No stats logic, logging changes, casino gameplay, or UI behavior changed.


PHASE 12G / 1.8.0 - BLACKJACK STATS FIX + WIN AUDIO
----------------------------------------------------
BLACKJACK STATS FIX
Blackjack stats are now recorded inside BlackjackRoomService.Resolve(), which is the
authoritative point where final WIN / LOSE / PUSH / BLACKJACK results are assigned.

Previously stats were recorded later in BlackjackGameCallback.ResolveIfNeeded(), alongside
payout delivery. That made statistics dependent on the HTTP/action path reaching that
callback. The room service is a more reliable source of truth.

Resolve() only processes a room while Phase == DEALER and changes it to RESOLVED, so each
hand is recorded exactly once.

The old callback stats recording was removed to prevent double counting.

BLACKJACK WIN AUDIO
Blackjack room voice events now include:
    Kind = LOSS or WIN

Winning players (WIN or BLACKJACK) create synchronized table-wide celebration events,
just like losing players create taunt events.

Win phrase pool:
    GoodWork
    OnGoodWork
    Ready
    Roger

Client fallback pool also includes:
    Greetings

All clients at the table receive the same event id / phrase / fallback voice and choose
the same native clip. Winner lines use a shorter random 0-1.5 second delay so multiple
winners do not all fire at precisely the same instant.

Loss staggering remains 0-4 seconds.

SLOT WIN VOICE
A successful slot-machine win now plays one native celebratory PMC voice line locally
for the spinner only.

Candidate phrases:
    GoodWork
    OnGoodWork
    Ready
    Roger
    Greetings

This slot celebration is never added to the shared Blackjack room event stream, so other
players do not hear another player's slot-machine win.

The existing normal UI win sound still plays as well.


PHASE 12H / 1.9.0 - SPLITS + 60 SECOND TURN TIMER + SLOT BUY-IN
----------------------------------------------------------------
BLACKJACK SPLITS
- SPLIT is available on an active two-card pair with matching ranks.
- The second wager is deducted before the split and refunded if the server rejects it.
- Resplitting is supported up to 4 total hands.
- Double after split is supported.
- Split aces receive exactly one additional card per hand and automatically stand.
- A 21 made after splitting is a normal 21, NOT a natural Blackjack and therefore does
  not receive the 3:2 natural payout.
- Each split hand has its own cards, wager, payout, bust/stand/result state.
- The player plays split hands sequentially before the turn advances to the next seat.
- The UI displays H1/H2/H3/H4 vertically with the active hand highlighted.
- Career Blackjack stats record each resolved split hand.
- Voice result selection is based on the player's aggregate net result after all hands.

60 SECOND TURN LIMIT
- Every active player turn receives a server-authoritative 60 second deadline.
- HIT, SPLIT and other continuing actions reset the full 60 second timer.
- The Character casino UI displays the countdown.
- At 10 seconds or less the timer uses the highlighted turn style.
- If the timer reaches zero before the active player acts, the server:
    * forfeits all wagers in that player's current round
    * records those hands as losses in career stats
    * removes the player from the Blackjack room
    * transfers host status if necessary
    * advances to the next player's turn
- If nobody remains to act, the dealer phase begins.
- The kicked client receives:
      TURN TIMEOUT - KICKED FROM TABLE
  and automatically leaves the local room UI.
- This is evaluated from normal room polling, so no separate spammy timer HTTP request
  was added.

SLOT BUY-IN
When the selected slot bet is larger than the player's current GP balance, the normal
SPIN button becomes:

    BUY 5 GP
    ₽10,000

- Each purchase removes ₽10,000 and adds 5 GP.
- It may be used repeatedly.
- The button is disabled if the player has less than ₽10,000.
- Both RUB and GP live stash balances are mirrored immediately.
- The server transaction attempts to roll back the RUB charge if GP creation fails.
- ServerCurrencyService can now create a brand-new GP stack when the player has zero GP,
  using another stackable inventory item as the native item seed.
- Modded GP/Rouble stack limits are still passed independently from the client.

SURRENDER
Not implemented, by design.


PHASE 12I / 2.0.0
------------------
BLACKJACK STATS
Fixed the stale-stat problem caused by CasinoStatsService being injected into multiple
server services with separate in-memory caches. The cache/lock/load state is now static
and shared, so Blackjack updates are immediately visible to /pep-casino/stats.

BUY-IN LIMIT
The 5 GP / ₽10,000 buy-in is now available only while total GP is below 5.
Once the player has 5+ GP, both client and server block further buy-ins. If the selected
slot bet is higher than the player's current GP while they already have 5+, the button
shows NOT ENOUGH GP instead of offering another purchase.

TABLE MATERIALS
Pep's Casino now uses a procedural dark-burgundy velvet/felt playing surface and a
procedural mahogany wood rail/border. The existing burgundy/gold color scheme is kept.
No external texture files are required.

BLACKJACK INSURANCE
Insurance is the next completed gameplay step:
- offered only when the dealer up-card is an Ace
- costs half the original hand wager
- pays 2:1 profit (3x total return) on dealer natural Blackjack
- otherwise the insurance wager is lost
- offered before normal turns / splits
- uses the existing 60-second decision timer
- INSURE ₽X and NO INSURANCE buttons
- insurance wagers/returns feed career Rouble totals
- Stats also shows insurance wins / insurance bets
- surrender remains intentionally omitted


PHASE 12J / 2.1.0 - SERVER BUY-IN CONFIG + CLIENT ODDS CLEANUP
---------------------------------------------------------------
TEST ODDS
The client-side F12 "Test Odds" option has been removed.

The client always sends testOdds=false, and the server independently ignores the incoming
testOdds value and always generates RELEASE odds.

The visible RELEASE/TEST footer label has also been removed.

SERVER BUY-IN CONFIG
Added:
    user/mods/PepSlotMachine/casino_config.json

Default:
{
  "BuyInCostRoubles": 10000
}

BuyInCostRoubles is the server-authoritative Rouble price for the fixed 5 GP buy-in.

The config service reloads automatically when the JSON file changes on disk. The casino
client fetches the current server value when Pep's Casino opens and uses it for the
button text and insufficient-Rouble message.

The existing GP restriction is unchanged:
    buy-in only while total GP < 5

The server enforces both the GP threshold and configured Rouble cost.


PHASE 12K / 2.2.0 - BLACKJACK QOL
----------------------------------
SERVER-CONFIGURABLE TABLE LIMITS
casino_config.json now also supports:

{
  "BuyInCostRoubles": 10000,
  "BlackjackMinBet": 1000,
  "BlackjackMaxBet": 50000
}

The server sanitizes the values so:
- minimum is at least ₽1
- maximum can never be lower than minimum

BlackjackRoomService enforces both limits. A modified client cannot place a bet outside
the server-configured range.

The client fetches the same values through /pep-casino/config and displays the current
table limits in the Blackjack betting area.

BET PRESETS
Added one-click Blackjack presets:
    ₽1K
    ₽5K
    ₽10K
    ₽25K
    ₽50K

Presets outside the configured server table range are hidden.
Presets the local player cannot afford are disabled.

REBET
Each player now remembers LastBaseBet separately from Wager.

That is important because Wager can later include:
- splits
- doubles
- insurance

REBET therefore repeats only the original base hand wager, not the previous hand's
expanded total exposure.

During WAITING, REBET is enabled only when:
- a previous base bet exists
- it is still inside the current server table limits
- the player has enough Roubles

DEAL remains host-only and is enabled once at least one player has a ready wager.


PHASE 12K FIX 1 / 2.2.1
------------------------
Fixed server compile errors:
    CasinoServerConfig.BlackjackMinBet not found
    CasinoServerConfig.BlackjackMaxBet not found

The Phase 12K consumers and config response were updated correctly, but the packaged
CasinoServerConfig model itself still contained only BuyInCostRoubles.

CasinoServerConfig now definitively contains:
    BuyInCostRoubles = 10000
    BlackjackMinBet = 1000
    BlackjackMaxBet = 50000

Sanitize() and Clone() also explicitly preserve/validate the Blackjack values.

No gameplay or UI behavior was otherwise changed.

PHASE 12K FIX 2 / 2.2.2
------------------------
- Moved the host DEAL button left so it no longer overlaps LEAVE TABLE.
- Blackjack preset buttons are now generated from BlackjackMinBet and BlackjackMaxBet.
- The table-limit label already uses the live server values and continues to update from /pep-casino/config.
- The selected wager is clamped into the current configured range when config is refreshed.
- Dynamic preset labels support normal amounts, K amounts, and M amounts.


PHASE 12L / 2.3.0 - RULES + RECENT HISTORY
-------------------------------------------
BLACKJACK RULES PANEL
Added an in-table RULES button.

The overlay summarizes the actual implemented rules:
- 6 decks
- S17
- natural Blackjack 3:2
- double any first two
- double after split
- up to 4 split hands
- split aces receive one card and stand
- split 21 is not a natural
- insurance 2:1 profit
- no surrender
- 60 second action timeout / kick
- current server-configured min/max wager

The rules panel reads the live client copy of the server table limits.

RECENT CASINO HISTORY
The STATS tab now contains:
    CAREER
    HISTORY

The server persists the newest 20 activity entries per profile in casino_stats.json.

Slot history records:
- wager
- returned GP
- net GP
- WIN / LOSS / PUSH / JACKPOT
- jackpot/win detail

Blackjack history records one aggregate event per completed round:
- total wager after doubles/splits/insurance
- total payout
- net Roubles
- aggregate result
- number of hands

Career Blackjack statistics are still recorded per resolved hand, while HISTORY is
recorded once per full player round so split hands do not spam the list.

The History screen displays the newest 10 saved events.


PHASE 12L FIX 1 / 2.3.1
------------------------
Fixed the STATS page overlap between the CASINO STATS heading and the CAREER/HISTORY
buttons.

New layout:
- heading at y=176
- CAREER/HISTORY buttons at y=218
- career/history content starts at y=282

The history list was shifted down and reduced to 9 visible rows so it cannot collide
with the footer. The selected CAREER/HISTORY tab now uses the stronger button style.

No gameplay, stats calculations, persistence, or server behavior changed.


PHASE 12M / 2.4.0 - SOLO AUTO DEAL + RESULT POLISH + ROBUST SETTLEMENT
----------------------------------------------------------------------
SOLO AUTO DEAL
Added a client F12 Blackjack option:
    Solo Auto Deal

Default:
    ON

When enabled and the local player is:
- the only player in the room
- the host
- in WAITING
- has an accepted wager

the client automatically sends DEAL after ~0.85 seconds.

Manual DEAL remains available, and multiplayer tables never auto-deal.

RESULT PRESENTATION
Blackjack results now get stronger per-hand presentation:
- WIN / BLACKJACK = green hand-result label
- LOSE / BUST = red hand-result label
- PUSH = neutral hand-result label
- split hands show their own outcome beside H1/H2/H3/H4
- single hands get a result label below the cards
- insurance wins/losses get their own result line
- the existing aggregate result banner remains visible during the resolved phase

DUPLICATE PAYOUT PROTECTION
BlackjackRoomState now contains:
    SettlementId
    SettlementApplied

Every dealt hand creates a fresh SettlementId.

After Resolve(), BlackjackGameRoutes must atomically ClaimSettlement() before applying
Rouble payouts. Only one request can claim a resolved settlement. Subsequent retries,
polls or duplicate HTTP requests receive the room state but cannot pay the same hand
again.

This specifically protects against the previous route behavior where calling the
dealer-finalization path again while a room was already RESOLVED could apply payouts
again.

ROOM HOUSEKEEPING
Inactive WAITING/RESOLVED rooms now expire after 15 minutes.
Actively playing PLAYER/INSURANCE/DEALER rooms retain the longer 30 minute stale timeout.

Existing timeout kicks/leave handling still forfeits in-progress wagers and removes the
player from the room.


PHASE 12N / 2.5.0 - FIKA MULTIPLAYER HARDENING
------------------------------------------------
TARGETED SERVER DIAGNOSTICS
casino_config.json now supports:

    "BlackjackDiagnostics": false

When enabled, Pep's Casino writes targeted Blackjack lifecycle logs only:
- room host/create
- player join
- player leave
- accepted bet
- deal + settlement id
- timeout removal
- host transfer
- hand resolution
- settlement claim

Normal room/lobby polling is NOT logged by Pep's Casino, so enabling diagnostics does
not restore the old request-spam problem.

ROOM STATE REVISION
BlackjackRoomState now includes:
    StateRevision
    LastNotice

StateRevision increases on meaningful table changes such as:
- join
- bet
- deal
- turn/insurance-seat transition
- resolve
- settlement claim
- leave/host transfer
- next betting round

When diagnostics are enabled, the client displays the current revision in the table
header. This gives us a simple way to compare two Fika clients during testing and see
whether both are looking at the same logical room state.

MULTIPLAYER STATUS UI
The Blackjack table header now shows:
    HOST
    PLAYERS X/5
    REV (when diagnostics are enabled)

Each seat now explicitly shows one of:
    ACTIVE
    WAITING
    DONE
    NEXT HAND
    READY
    NO BET

Players who join while a hand is already in progress are labeled NEXT HAND instead of
appearing ambiguously idle.

ROOM NOTICES
The table surfaces meaningful lifecycle notices such as:
    <player> JOINED TABLE
    <player> JOINED - NEXT HAND
    <player> LEFT TABLE
    HOST TRANSFERRED TO <player>
    <player> TIMED OUT - REMOVED
    HAND STARTED
    HAND RESOLVED
    SETTLEMENT CLAIMED
    BETTING ROUND OPEN

HOST TRANSFER
When a host leaves or is removed, the lowest occupied seat is promoted as before, but
the transfer is now explicitly surfaced in room state and diagnostics.

This phase intentionally does not add Fika-specific assembly references. The shared SPT
server room remains the synchronization authority, so it can be tested with normal solo
play now and with actual Fika clients later.


PHASE 12O / 2.6.0 - SPT 4.1.2 CURRENCY AUDIT
----------------------------------------------
The complete casino currency layer was reviewed against SPT 4.1.2's server payment
patterns and refactored.

Highlights:
- native AddItemsDirectRequest + InventoryHelper.AddItemsToStash
- native ItemHelper.SplitStackIntoSeparateItems
- server-template stack limits
- stash-only balance parity with the Character client
- whole-number StackObjectsCount normalization
- one shared server currency implementation for Slots/Buy-in/Blackjack
- multiplayer Blackjack wager rollback on pre-deal failure
- settlement payout rollback/retry handling
- dynamic configured buy-in success message
- faster no-op client mirroring

See:
    CURRENCY_AUDIT_SPT_4.1.2.md
for the detailed audit.


PHASE 12O FIX 1 / 2.6.1
------------------------
Fixed the first compile error from the SPT 4.1.2 currency refactor.

Verified directly against SPT 4.1.2 source:
- InventoryHelper is in SPTarkov.Server.Core.Helpers.Profile
- SplitStackIntoSeparateItems returns List<List<Item>>
- AddItemsDirectRequest accepts grouped item/mod lists

The reward/fallback code now uses the correct grouped shape throughout.
