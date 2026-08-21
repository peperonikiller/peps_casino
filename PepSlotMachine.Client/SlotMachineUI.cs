using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PepSlotMachine
{
    public class SlotMachineUI : MonoBehaviour
    {
        private const int ReelCount = 5;
        private const int RowsPerReel = 3;
        private const float SymbolHeight = 94f;

        private bool _visible;
        private bool _spinning;
        private bool _transactionPending;

        private string _status = "";
        private string _promptMessage = "";
        private float _promptUntil;

        private Rect _windowRect;

        private GameObject _inputBlockerObject;
        private Canvas _inputBlockerCanvas;

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _balanceStyle;
        private GUIStyle _symbolStyle;
        private GUIStyle _centerSymbolStyle;
        private GUIStyle _winningSymbolStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _smallButtonStyle;
        private GUIStyle _winStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _symbolCodeStyle;
        private GUIStyle _symbolNameStyle;
        private GUIStyle _blackjackButtonStyle;
        private GUIStyle _blackjackStatusStyle;
        private GUIStyle _blackjackCardStyle;
        private GUIStyle _blackjackCardCompactStyle;
        private GUIStyle _blackjackPlayerStyle;
        private GUIStyle _blackjackPlayerTurnStyle;
        private GUIStyle _blackjackResultStyle;
        private GUIStyle _blackjackHandWinStyle;
        private GUIStyle _blackjackHandLoseStyle;
        private GUIStyle _blackjackHandPushStyle;

        private bool _stylesInitialized;
        private Texture2D _panelTexture;
        private Texture2D _feltTexture;
        private Texture2D _mahoganyTexture;
        private Texture2D _symbolTexture;
        private Texture2D _centerTexture;
        private Texture2D _winningTexture;
        private Texture2D _symbolBadgeTexture;
        private Texture2D _dividerTexture;

        private int _balance;
        private int _bet = 5;
        private int _lastWin;
        private int _winningPayline = -1;

        private int _pendingWin;
        private int _pendingWinningPayline = -1;
        private int _pendingFinalBalance;
        private SlotCell[] _pendingWinningCells;
        private SlotLineWin[] _pendingLineWins;

        private SlotLineWin[] _lineWins;
        private bool _pendingJackpot;
        private bool _jackpot;
        private int _displayedWinAmount;
        private int _displayedBalance;
        private float _winPresentationStartedAt;
        private int _activeWinningLineIndex = -1;
        private string _winTierLabel = "";

        private float _jackpotCelebrationStartedAt = -100f;
        private const float JackpotCelebrationSeconds = 4.25f;

        private enum CasinoTab
        {
            Slots,
            Blackjack,
            Shop,
            Stats
        }

        private CasinoTab _activeTab =
            CasinoTab.Slots;

        private BlackjackLobbyState _blackjackLobby;
        private BlackjackRoomState _currentBlackjackRoom;
        private float _nextBlackjackLobbyPollAt;
        private const float BlackjackLobbyPollSeconds = 2f;
        private string _blackjackStatus = "HOST OR JOIN A TABLE";
        private int _blackjackBet = 10000;
        private bool _blackjackRequestPending;
        private bool _soloAutoDealScheduled;
        private string _soloAutoDealRoomId;

        private CasinoPlayerStats _casinoStats;
        private bool _statsLoading;
        private bool _statsDirty = true;
        private bool _statsShowHistory;
        private bool _showBlackjackRules;

        private int _jackpotAmount = 500;
        private int _lastJackpotPayout;
        private int _buyInCostRoubles = 10000;
        private int _blackjackMinBet = 1000;
        private int _blackjackMaxBet = 50000;
        private bool _blackjackDiagnostics;

        private CasinoShopItem[] _shopItems =
            Array.Empty<CasinoShopItem>();

        private string _shopStatus =
            "CASINO CHIP EXCHANGE";

        private bool _shopPurchasePending;

        private readonly Dictionary<string, Sprite> _shopSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private bool _shopIconsLoading;

        private Vector2 _shopScrollPosition =
            Vector2.zero;

        private readonly int[] _bets = { 1, 5, 10, 25, 50 };

        private readonly string[,] _displaySymbols =
            new string[ReelCount, RowsPerReel];

        private readonly string[,] _finalSymbols =
            new string[ReelCount, RowsPerReel];

        private readonly bool[,] _winningCells =
            new bool[ReelCount, RowsPerReel];

        private readonly bool[] _reelSpinning =
            new bool[ReelCount];

        private readonly float[] _reelOffsets =
            new float[ReelCount];

        private readonly float[] _reelSpeeds =
            new float[ReelCount];

        private readonly float[] _reelSettleOffsets =
            new float[ReelCount];

        private readonly string[] _incomingSymbols =
            new string[ReelCount];

        // Persistent visual reel strips. These do not determine the
        // server-authoritative result; they only make the animation behave like
        // physical reels by moving through a stable repeating sequence.
        private readonly string[][] _visualReelStrips =
        {
            new[]
            {
                "GP", "DOGTAG", "ROUBLES", "PROKILL", "GOLDSTAR",
                "GP", "LABS", "DOGTAG", "LEDX", "ROUBLES",
                "GP", "BTC", "PROKILL", "DOGTAG", "RR",
                "ROUBLES", "GOLDSTAR", "GP", "LABS", "JACKPOT",
                "DOGTAG", "LEDX", "GP", "ROUBLES", "PROKILL"
            },
            new[]
            {
                "DOGTAG", "GP", "PROKILL", "ROUBLES", "GOLDSTAR",
                "LABS", "GP", "DOGTAG", "BTC", "ROUBLES",
                "LEDX", "GP", "PROKILL", "RR", "DOGTAG",
                "GOLDSTAR", "ROUBLES", "LABS", "GP", "JACKPOT",
                "LEDX", "DOGTAG", "ROUBLES", "GP", "PROKILL"
            },
            new[]
            {
                "ROUBLES", "PROKILL", "GP", "DOGTAG", "GOLDSTAR",
                "GP", "LEDX", "LABS", "DOGTAG", "ROUBLES",
                "BTC", "GP", "PROKILL", "DOGTAG", "RR",
                "GOLDSTAR", "ROUBLES", "GP", "LABS", "JACKPOT",
                "DOGTAG", "BTC", "GP", "LEDX", "ROUBLES"
            },
            new[]
            {
                "GP", "ROUBLES", "DOGTAG", "PROKILL", "LABS",
                "GOLDSTAR", "GP", "LEDX", "DOGTAG", "ROUBLES",
                "PROKILL", "BTC", "GP", "DOGTAG", "RR",
                "ROUBLES", "GOLDSTAR", "LABS", "GP", "JACKPOT",
                "DOGTAG", "PROKILL", "LEDX", "GP", "ROUBLES"
            },
            new[]
            {
                "PROKILL", "DOGTAG", "GP", "ROUBLES", "GOLDSTAR",
                "LABS", "ROUBLES", "GP", "LEDX", "DOGTAG",
                "BTC", "PROKILL", "GP", "RR", "ROUBLES",
                "DOGTAG", "GOLDSTAR", "GP", "LABS", "JACKPOT",
                "LEDX", "ROUBLES", "DOGTAG", "GP", "PROKILL"
            }
        };

        private readonly int[] _visualReelStripIndices =
            new int[ReelCount];

        private GUISounds _guiSounds;

        private readonly Dictionary<string, Sprite> _symbolSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private bool _iconsLoading;
        private bool _iconsLoaded;
        private bool _showSlotPaytable;

        // Every slot symbol is backed by a real EFT item template so the
        // reels can always display an item icon instead of a synthetic symbol.
        private readonly Dictionary<string, string> _symbolTemplateIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GP"] = "5d235b4d86f7742e017bc88a",
                ["DOGTAG"] = "59f32c3b86f77472a31742f0",
                ["PROKILL"] = "5c1267ee86f77416ec610f72",
                ["ROUBLES"] = "5449016a4bdc2d6f028b456f",
                ["GOLDSTAR"] = "5751a89d24597722aa0e8db0",
                ["LABS"] = "5c94bbff86f7747ee735c08f",
                ["LEDX"] = "5c0530ee86f774697952d952",
                ["BTC"] = "59faff1d86f7746c51718c9c",
                ["RR"] = "5c0126f40db834002a125382",
                ["JACKPOT"] = "5d235a5986f77443f6329bc6"
            };


        private readonly Dictionary<string, SymbolVisual> _symbolVisuals =
            new Dictionary<string, SymbolVisual>(StringComparer.OrdinalIgnoreCase)
            {
                ["GP"] = new SymbolVisual("GP", "GP COIN", new Color(.68f, .52f, .12f)),
                ["DOGTAG"] = new SymbolVisual("DT", "DOGTAG", new Color(.35f, .38f, .38f)),
                ["PROKILL"] = new SymbolVisual("PK", "PROKILL", new Color(.56f, .44f, .20f)),
                ["ROUBLES"] = new SymbolVisual("RUB", "ROUBLES", new Color(.31f, .48f, .28f)),
                ["GOLDSTAR"] = new SymbolVisual("GS", "GOLDSTAR", new Color(.65f, .49f, .16f)),
                ["LABS"] = new SymbolVisual("LAB", "KEYCARD", new Color(.30f, .51f, .52f)),
                ["LEDX"] = new SymbolVisual("LX", "LEDX", new Color(.50f, .54f, .46f)),
                ["BTC"] = new SymbolVisual("BTC", "BITCOIN", new Color(.68f, .42f, .09f)),
                ["RR"] = new SymbolVisual("RR", "RED REBEL", new Color(.49f, .18f, .16f)),
                ["JACKPOT"] = new SymbolVisual("★", "GOLD SKULL", new Color(.78f, .58f, .12f))
            };

        private void Awake()
        {
            InitializeReels();
            ResolveGuiSounds();
        }

        private void OnDisable()
        {
            SetCasinoInputBlocker(
                false);
        }

        private void OnDestroy()
        {
            if (_inputBlockerObject != null)
            {
                Destroy(
                    _inputBlockerObject);

                _inputBlockerObject =
                    null;

                _inputBlockerCanvas =
                    null;
            }
        }

        private void SetCasinoVisible(
            bool visible)
        {
            _visible =
                visible;

            SetCasinoInputBlocker(
                visible);
        }

        private void SetCasinoInputBlocker(
            bool enabled)
        {
            if (!enabled)
            {
                if (_inputBlockerObject != null)
                {
                    _inputBlockerObject.SetActive(
                        false);
                }

                return;
            }

            if (_inputBlockerObject == null)
            {
                _inputBlockerObject =
                    new GameObject(
                        "PepCasinoInputBlocker");

                _inputBlockerObject.hideFlags =
                    HideFlags.HideAndDontSave;

                _inputBlockerCanvas =
                    _inputBlockerObject.AddComponent<Canvas>();

                _inputBlockerCanvas.renderMode =
                    RenderMode.ScreenSpaceOverlay;

                _inputBlockerCanvas.sortingOrder =
                    short.MaxValue;

                _inputBlockerObject.AddComponent<GraphicRaycaster>();

                GameObject blocker =
                    new GameObject(
                        "RaycastBlocker");

                blocker.transform.SetParent(
                    _inputBlockerObject.transform,
                    false);

                RectTransform rect =
                    blocker.AddComponent<RectTransform>();

                rect.anchorMin =
                    Vector2.zero;

                rect.anchorMax =
                    Vector2.one;

                rect.offsetMin =
                    Vector2.zero;

                rect.offsetMax =
                    Vector2.zero;

                Image image =
                    blocker.AddComponent<Image>();

                image.color =
                    new Color(
                        0f,
                        0f,
                        0f,
                        0f);

                image.raycastTarget =
                    true;
            }

            _inputBlockerObject.SetActive(
                true);

            _inputBlockerObject.transform.SetAsLastSibling();
        }

        private void InitializeReels()
        {
            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                string[] strip =
                    _visualReelStrips[reel];

                int startIndex =
                    UnityEngine.Random.Range(
                        0,
                        strip.Length);

                _visualReelStripIndices[reel] =
                    startIndex;

                for (int row = 0;
                     row < RowsPerReel;
                     row++)
                {
                    string symbol =
                        GetStripSymbol(
                            reel,
                            startIndex + row);

                    _displaySymbols[reel, row] =
                        symbol;

                    _finalSymbols[reel, row] =
                        symbol;
                }

                _incomingSymbols[reel] =
                    GetStripSymbol(
                        reel,
                        startIndex +
                        RowsPerReel);
            }
        }

        private string GetStripSymbol(
            int reel,
            int index)
        {
            string[] strip =
                _visualReelStrips[
                    Mathf.Clamp(
                        reel,
                        0,
                        ReelCount - 1)];

            if (strip == null ||
                strip.Length == 0)
            {
                return "GP";
            }

            int wrapped =
                index %
                strip.Length;

            if (wrapped < 0)
            {
                wrapped +=
                    strip.Length;
            }

            return strip[
                wrapped];
        }

        private void AdvanceVisualReelStrip(
            int reel)
        {
            string[] strip =
                _visualReelStrips[reel];

            if (strip == null ||
                strip.Length == 0)
            {
                _incomingSymbols[reel] =
                    "GP";

                return;
            }

            _visualReelStripIndices[reel] =
                (_visualReelStripIndices[reel] + 1) %
                strip.Length;

            _incomingSymbols[reel] =
                GetStripSymbol(
                    reel,
                    _visualReelStripIndices[reel] +
                    RowsPerReel);
        }

        public void Toggle()
        {
            if (_spinning || _transactionPending)
            {
                return;
            }

            if (!_visible)
            {
                InventoryController controller;

                if (!CurrencyService.TryGetActiveCharacterInventoryController(
                    out controller))
                {
                    ShowPrompt(
                        "SLOT MACHINE UNAVAILABLE\nOpen the CHARACTER screen to play.");
                    return;
                }

                SetCasinoVisible(
                    true);

                RefreshBalance(controller);
                RefreshJackpotOnce(controller);
                RefreshServerConfig(controller);
                RefreshStats(controller, false);

                if (!_iconsLoaded && !_iconsLoading)
                {
                    LoadSymbolIcons(controller);
                }

                PlayNativeUISound(
                    EUISoundType.MenuInspectorWindowOpen);

                return;
            }

            PlayNativeUISound(EUISoundType.MenuInspectorWindowClose);
            SetCasinoVisible(
                false);
        }

        private void ShowPrompt(string message)
        {
            _promptMessage = message;
            _promptUntil = Time.unscaledTime + 3f;
        }

        private void RefreshBalance(
            InventoryController controller)
        {
            _balance =
                CurrencyService.GetBalance(controller, CurrencyService.Gp);

            _displayedBalance = _balance;
            _displayedWinAmount = _lastWin;

            _status = "SERVER-AUTHORITATIVE CASINO CHIP MODE";
        }

        private void Update()
        {
            if (!_visible)
            {
                return;
            }

            if (!_spinning && !_transactionPending)
            {
                if (!CurrencyService.IsCachedCharacterScreenActive())
                {
                    SetCasinoVisible(
                        false);

                    ShowPrompt(
                        "SLOT MACHINE CLOSED\nReturn to the CHARACTER screen to play.");
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    SetCasinoVisible(
                        false);

                    return;
                }
            }

            if (_visible &&
                _activeTab ==
                CasinoTab.Blackjack &&
                Time.unscaledTime >=
                _nextBlackjackLobbyPollAt)
            {
                _nextBlackjackLobbyPollAt =
                    Time.unscaledTime +
                    BlackjackLobbyPollSeconds;

                InventoryController blackjackController;

                if (CurrencyService
                    .TryGetActiveCharacterInventoryController(
                        out blackjackController))
                {
                    if (_currentBlackjackRoom != null)
                    {
                        StartCoroutine(
                            SlotServerClient.GetBlackjackRoom(
                                _currentBlackjackRoom.RoomId,
                                blackjackController.ID,
                                result =>
                                {
                                    if (result == null)
                                        return;

                                    ApplyBlackjackBalance(
                                        blackjackController,
                                        result);

                                    if (!result.Success)
                                    {
                                        _blackjackStatus =
                                            result.Message
                                            ?? "TABLE REQUEST FAILED";

                                        if (!string.IsNullOrEmpty(result.Message) &&
                                            result.Message.IndexOf(
                                                "KICKED",
                                                StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            _currentBlackjackRoom =
                                                null;

                                            _soloAutoDealScheduled =
                                                false;

                                            _soloAutoDealRoomId =
                                                null;

                                            _nextBlackjackLobbyPollAt =
                                                0f;
                                        }

                                        return;
                                    }

                                    if (result.Room != null)
                                    {
                                        _currentBlackjackRoom =
                                            result.Room;

                                        if(result.Room.Phase=="RESOLVED")
                                            _statsDirty=true;

                                        BlackjackVoiceService.ProcessTableTaunts(
                                            this,
                                            blackjackController,
                                            result.Room);

                                        _blackjackStatus =
                                            result.Room.Message
                                            ?? string.Empty;
                                    }
                                }));
                    }
                    else
                    {
                        StartCoroutine(
                            SlotServerClient.GetBlackjackLobby(
                                blackjackController.ID,
                                lobby =>
                                {
                                    if (lobby != null) _blackjackLobby = lobby;
                                }));
                    }
                }
            }

            UpdateReels();
        }

        private void RefreshServerConfig(
            InventoryController controller)
        {
            if (controller == null)
                return;

            StartCoroutine(
                SlotServerClient.GetCasinoConfig(
                    controller.ID,
                    config =>
                    {
                        if (config != null)
                        {
                            _buyInCostRoubles =
                                Math.Max(
                                    0,
                                    config.BuyInCostRoubles);

                            _blackjackMinBet =
                                Math.Max(
                                    1,
                                    config.BlackjackMinBet);

                            _blackjackMaxBet =
                                Math.Max(
                                    _blackjackMinBet,
                                    config.BlackjackMaxBet);

                            _blackjackDiagnostics =
                                config.BlackjackDiagnostics;

                            _shopItems =
                                config.ShopItems
                                ?? Array.Empty<CasinoShopItem>();

                            LoadShopIcons(
                                controller);

                            _blackjackBet =
                                Math.Max(
                                    _blackjackMinBet,
                                    Math.Min(
                                        _blackjackMaxBet,
                                        _blackjackBet));
                        }
                    }));
        }

        private void RefreshJackpotOnce(InventoryController controller)
        {
            if(controller==null)return;

            StartCoroutine(
                SlotServerClient.GetJackpot(
                    controller.ID,
                    state =>
                    {
                        if(state!=null)
                            _jackpotAmount=Math.Max(0,state.Amount);
                    }));
        }

        private void RefreshStats(InventoryController controller,bool force)
        {
            if(controller==null||_statsLoading||(!force&&!_statsDirty&&_casinoStats!=null))
                return;

            _statsLoading=true;

            StartCoroutine(
                SlotServerClient.GetStats(
                    controller.ID,
                    stats =>
                    {
                        _statsLoading=false;
                        if(stats!=null)
                        {
                            _casinoStats=stats;
                            _statsDirty=false;
                        }
                    }));
        }

        private void UpdateReels()
        {
            if (!_spinning)
            {
                return;
            }

            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                if (!_reelSpinning[reel])
                {
                    continue;
                }

                _reelOffsets[reel] +=
                    _reelSpeeds[reel] *
                    Time.unscaledDeltaTime;

                while (_reelOffsets[reel] >= 1f)
                {
                    _reelOffsets[reel] -= 1f;

                    _displaySymbols[reel, 0] =
                        _displaySymbols[reel, 1];

                    _displaySymbols[reel, 1] =
                        _displaySymbols[reel, 2];

                    _displaySymbols[reel, 2] =
                        _incomingSymbols[reel];

                    AdvanceVisualReelStrip(
                        reel);

                    PlayNativeUISound(
                        EUISoundType.ButtonOver);
                }
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (Time.unscaledTime < _promptUntil)
            {
                DrawPrompt();
            }

            if (!_visible)
            {
                return;
            }

            float width =
                Mathf.Min(
                    Screen.width * .86f,
                    1320f);

            float height =
                Mathf.Min(
                    Screen.height * .90f,
                    900f);

            _windowRect =
                new Rect(
                    (Screen.width - width) * .5f,
                    (Screen.height - height) * .5f,
                    width,
                    height);

            Color old = GUI.color;

            GUI.color =
                new Color(0f, 0f, 0f, .86f);

            GUI.DrawTexture(
                new Rect(
                    0f,
                    0f,
                    Screen.width,
                    Screen.height),
                Texture2D.whiteTexture);

            GUI.color = old;

            GUI.DrawTexture(
                _windowRect,
                _mahoganyTexture,
                ScaleMode.StretchToFill,
                true);

            Rect feltRect=new Rect(
                _windowRect.x+16f,
                _windowRect.y+16f,
                _windowRect.width-32f,
                _windowRect.height-32f);

            GUI.DrawTexture(
                feltRect,
                _feltTexture,
                ScaleMode.StretchToFill,
                true);

            SetCasinoInputBlocker(
                true);

            GUI.BeginGroup(_windowRect);
            DrawMachine();
            GUI.EndGroup();

            Event currentEvent =
                Event.current;

            if (currentEvent != null &&
                currentEvent.isMouse &&
                currentEvent.type != EventType.Used)
            {
                currentEvent.Use();
            }
        }

        private void DrawPrompt()
        {
            Rect rect =
                new Rect(
                    (Screen.width - 520f) * .5f,
                    Screen.height * .18f,
                    520f,
                    115f);

            GUI.DrawTexture(
                rect,
                _panelTexture,
                ScaleMode.StretchToFill,
                true);

            GUI.Label(
                rect,
                _promptMessage,
                _statusStyle);
        }

        private void DrawMachine()
        {
            float width =
                _windowRect.width;

            float height =
                _windowRect.height;

            Color oldColor =
                GUI.color;

            GUI.color =
                new Color(
                    .78f,
                    .53f,
                    .12f,
                    1f);

            GUI.DrawTexture(
                new Rect(
                    18f,
                    12f,
                    width - 36f,
                    4f),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(
                    18f,
                    122f,
                    width - 36f,
                    2f),
                Texture2D.whiteTexture);

            GUI.color =
                oldColor;

            GUI.Label(
                new Rect(
                    34f,
                    18f,
                    330f,
                    42f),
                "Pep's Casino",
                _titleStyle);

            GUI.Label(
                new Rect(
                    34f,
                    58f,
                    330f,
                    24f),
                "TARKOV WAGER HOUSE",
                _subtitleStyle);

            GUI.Label(
                new Rect(
                    width * .5f - 230f,
                    16f,
                    460f,
                    26f),
                "JACKPOT",
                _statusStyle);

            GUI.Label(
                new Rect(
                    width * .5f - 260f,
                    40f,
                    520f,
                    58f),
                $"{_jackpotAmount:N0} CHIPS",
                _winStyle);

            float tabWidth = 132f;
            float tabGap = 12f;
            float totalTabsWidth = tabWidth * 4f + tabGap * 3f;
            float tabX = width * .5f - totalTabsWidth * .5f;

            if (GUI.Button(
                    new Rect(tabX,132f,tabWidth,34f),
                    "SLOTS",
                    _smallButtonStyle))
            {
                _activeTab=CasinoTab.Slots;
                InventoryController c;
                if(CurrencyService.TryGetActiveCharacterInventoryController(out c))
                    RefreshJackpotOnce(c);
            }

            tabX += tabWidth + tabGap;

            if (GUI.Button(
                    new Rect(tabX,132f,tabWidth,34f),
                    "BLACKJACK",
                    _smallButtonStyle))
            {
                _activeTab=CasinoTab.Blackjack;
                _nextBlackjackLobbyPollAt=0f;
            }

            tabX += tabWidth + tabGap;

            if (GUI.Button(
                    new Rect(tabX,132f,tabWidth,34f),
                    "SHOP",
                    _smallButtonStyle))
            {
                _activeTab=CasinoTab.Shop;
                InventoryController c;
                if(CurrencyService.TryGetActiveCharacterInventoryController(out c))
                {
                    RefreshBalance(c);
                    RefreshServerConfig(c);
                    LoadShopIcons(c);
                }
            }

            tabX += tabWidth + tabGap;

            if (GUI.Button(
                    new Rect(tabX,132f,tabWidth,34f),
                    "STATS",
                    _smallButtonStyle))
            {
                _activeTab=CasinoTab.Stats;
                InventoryController c;
                if(CurrencyService.TryGetActiveCharacterInventoryController(out c))
                    RefreshStats(c,true);
            }

            if(_activeTab==CasinoTab.Slots)
                DrawSlotsTab(width,height);
            else if(_activeTab==CasinoTab.Blackjack)
                DrawBlackjackTab(width,height);
            else if(_activeTab==CasinoTab.Shop)
                DrawShopTab(width,height);
            else
                DrawStatsTab(width,height);

            if (!_spinning &&
                !_transactionPending &&
                GUI.Button(
                    new Rect(
                        width - 62f,
                        24f,
                        38f,
                        34f),
                    "X",
                    _smallButtonStyle))
            {
                SetCasinoVisible(
                    false);
            }
        }

        private void DrawSlotsTab(
            float width,
            float height)
        {
            GUI.Label(
                new Rect(
                    42f,
                    174f,
                    330f,
                    38f),
                $"CHIP BALANCE: {_displayedBalance:N0}",
                _balanceStyle);

            GUI.Label(
                new Rect(
                    width - 372f,
                    174f,
                    330f,
                    38f),
                $"LAST WIN: {_displayedWinAmount:N0}",
                _balanceStyle);

            DrawReels(
                width);

            DrawJackpotCelebration(
                width);

            DrawControls(
                width);

            GUI.enabled =
                !_spinning &&
                !_transactionPending;

            if (GUI.Button(
                    new Rect(
                        42f,
                        548f,
                        128f,
                        34f),
                    _showSlotPaytable
                        ? "CLOSE TABLE"
                        : "PAYTABLE",
                    _smallButtonStyle))
            {
                _showSlotPaytable =
                    !_showSlotPaytable;
            }

            GUI.enabled =
                true;

            if (_showSlotPaytable)
            {
                DrawSlotPaytable(
                    width);
            }
            else
            {
                DrawPayoutBreakdown(
                    width);
            }

            if (_lastJackpotPayout > 0 &&
                !_spinning &&
                !_transactionPending)
            {
                GUI.Label(
                    new Rect(
                        width * .5f - 330f,
                        690f,
                        660f,
                        52f),
                    $"JACKPOT PAID: +{_lastJackpotPayout:N0} CHIPS",
                    _winStyle);
            }

            GUI.Label(
                new Rect(
                    35f,
                    height - 82f,
                    width - 70f,
                    26f),
                _status,
                _statusStyle);
        }

        private void DrawJackpotCelebration(
            float width)
        {
            if (!_jackpot ||
                _spinning)
            {
                return;
            }

            float elapsed =
                Time.unscaledTime -
                _jackpotCelebrationStartedAt;

            if (elapsed < 0f ||
                elapsed >
                JackpotCelebrationSeconds)
            {
                return;
            }

            const float reelWidth = 165f;
            const float spacing = 15f;

            float totalWidth =
                reelWidth * ReelCount +
                spacing * (ReelCount - 1);

            float startX =
                (width - totalWidth) *
                .5f;

            float startY =
                232f;

            float reelHeight =
                SymbolHeight *
                RowsPerReel;

            float pulse =
                .58f +
                .42f *
                Mathf.Abs(
                    Mathf.Sin(
                        Time.unscaledTime *
                        7.5f));

            Color old =
                GUI.color;

            GUI.color =
                new Color(
                    1f,
                    .68f,
                    .10f,
                    pulse);

            // Flashing four-sided frame around the entire reel bank.
            GUI.DrawTexture(
                new Rect(
                    startX - 10f,
                    startY - 10f,
                    totalWidth + 20f,
                    5f),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(
                    startX - 10f,
                    startY + reelHeight + 5f,
                    totalWidth + 20f,
                    5f),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(
                    startX - 10f,
                    startY - 10f,
                    5f,
                    reelHeight + 20f),
                Texture2D.whiteTexture);

            GUI.DrawTexture(
                new Rect(
                    startX + totalWidth + 5f,
                    startY - 10f,
                    5f,
                    reelHeight + 20f),
                Texture2D.whiteTexture);

            GUI.color =
                old;

            float bannerPulse =
                1f +
                .055f *
                Mathf.Sin(
                    Time.unscaledTime *
                    8f);

            const float baseBannerWidth =
                640f;

            float bannerWidth =
                baseBannerWidth *
                bannerPulse;

            Rect banner =
                new Rect(
                    width * .5f -
                    bannerWidth * .5f,
                    498f,
                    bannerWidth,
                    46f);

            Color oldBg =
                GUI.color;

            GUI.color =
                new Color(
                    .03f,
                    .018f,
                    .005f,
                    .94f);

            GUI.DrawTexture(
                banner,
                Texture2D.whiteTexture);

            GUI.color =
                oldBg;

            GUI.Label(
                banner,
                $"★ JACKPOT  +{_lastJackpotPayout:N0} CHIPS ★",
                _winStyle);
        }

        private void DrawShopTab(
            float width,
            float height)
        {
            InventoryController controller = null;

            bool haveController =
                CurrencyService.TryGetActiveCharacterInventoryController(
                    out controller);

            int liveChips =
                haveController
                    ? CurrencyService.GetBalance(
                        controller,
                        CurrencyService.Gp)
                    : 0;

            _balance =
                liveChips;

            _displayedBalance =
                liveChips;

            GUI.Label(
                new Rect(
                    42f,
                    176f,
                    width - 84f,
                    34f),
                "CASINO CHIP SHOP",
                _titleStyle);

            GUI.Label(
                new Rect(
                    42f,
                    214f,
                    width - 84f,
                    30f),
                $"AVAILABLE: {liveChips:N0} CASINO CHIPS",
                _balanceStyle);

            CasinoShopItem[] items =
                _shopItems
                ?? Array.Empty<CasinoShopItem>();

            if (items.Length == 0)
            {
                GUI.Label(
                    new Rect(
                        42f,
                        286f,
                        width - 84f,
                        30f),
                    "NO SHOP ITEMS CONFIGURED",
                    _statusStyle);
            }
            else
            {
                const int columns =
                    3;

                const float cardHeight =
                    190f;

                const float gap =
                    16f;

                float contentLeft =
                    50f;

                float contentTop =
                    260f;

                float contentWidth =
                    width - 100f;

                float cardWidth =
                    (contentWidth -
                     gap * (columns - 1)) /
                    columns;

                int rows =
                    Mathf.CeilToInt(
                        items.Length /
                        (float)columns);

                float viewportHeight =
                    height -
                    contentTop -
                    106f;

                float contentHeight =
                    Math.Max(
                        viewportHeight,
                        rows * cardHeight +
                        Math.Max(
                            0,
                            rows - 1) *
                        gap);

                Rect viewport =
                    new Rect(
                        contentLeft,
                        contentTop,
                        contentWidth,
                        viewportHeight);

                Rect contentRect =
                    new Rect(
                        0f,
                        0f,
                        contentWidth - 18f,
                        contentHeight);

                _shopScrollPosition =
                    GUI.BeginScrollView(
                        viewport,
                        _shopScrollPosition,
                        contentRect);

                for (int index = 0;
                     index < items.Length;
                     index++)
                {
                    CasinoShopItem item =
                        items[index];

                    if (item == null)
                        continue;

                    int column =
                        index %
                        columns;

                    int row =
                        index /
                        columns;

                    float x =
                        column *
                        (cardWidth + gap);

                    float y =
                        row *
                        (cardHeight + gap);

                    DrawShopCard(
                        new Rect(
                            x,
                            y,
                            cardWidth,
                            cardHeight),
                        controller,
                        haveController,
                        liveChips,
                        item);
                }

                GUI.EndScrollView();
            }

            GUI.Label(
                new Rect(
                    42f,
                    height - 76f,
                    width - 84f,
                    26f),
                _shopStatus,
                _statusStyle);
        }

        private void DrawShopCard(
            Rect card,
            InventoryController controller,
            bool haveController,
            int liveChips,
            CasinoShopItem item)
        {
            int cost =
                Math.Max(
                    1,
                    item.ChipCost);

            int quantity =
                Math.Max(
                    1,
                    item.Quantity);

            GUI.Box(
                card,
                GUIContent.none,
                _blackjackCardStyle);

            Rect iconArea =
                new Rect(
                    card.x + 14f,
                    card.y + 12f,
                    card.width - 28f,
                    92f);

            Sprite sprite =
                null;

            bool hasSprite =
                !string.IsNullOrWhiteSpace(
                    item.TemplateId) &&
                _shopSprites.TryGetValue(
                    item.TemplateId,
                    out sprite) &&
                sprite != null &&
                sprite.texture != null;

            if (hasSprite)
            {
                Rect source =
                    sprite.textureRect;

                Rect texCoords =
                    new Rect(
                        source.x /
                        sprite.texture.width,
                        source.y /
                        sprite.texture.height,
                        source.width /
                        sprite.texture.width,
                        source.height /
                        sprite.texture.height);

                float sourceAspect =
                    source.height > 0f
                        ? source.width /
                          source.height
                        : 1f;

                Rect fitted =
                    FitRectPreserveAspect(
                        iconArea,
                        sourceAspect);

                GUI.DrawTextureWithTexCoords(
                    fitted,
                    sprite.texture,
                    texCoords,
                    alphaBlend: true);
            }
            else
            {
                GUI.Label(
                    iconArea,
                    _shopIconsLoading
                        ? "LOADING..."
                        : "NO ICON",
                    _subtitleStyle);
            }

            GUI.Label(
                new Rect(
                    card.x + 12f,
                    card.y + 108f,
                    card.width - 24f,
                    28f),
                quantity > 1
                    ? $"{quantity}x {item.DisplayName}"
                    : item.DisplayName,
                _statusStyle);

            GUI.Label(
                new Rect(
                    card.x + 12f,
                    card.y + 136f,
                    card.width - 116f,
                    24f),
                $"{cost} CHIP{(cost == 1 ? "" : "S")}",
                _balanceStyle);

            bool canBuy =
                haveController &&
                controller != null &&
                !_transactionPending &&
                !_shopPurchasePending &&
                liveChips >=
                cost;

            bool oldEnabled =
                GUI.enabled;

            GUI.enabled =
                canBuy;

            if (GUI.Button(
                    new Rect(
                        card.x +
                        card.width -
                        94f,
                        card.y +
                        139f,
                        78f,
                        32f),
                    "BUY",
                    _smallButtonStyle))
            {
                BeginShopPurchase(
                    controller,
                    item);
            }

            GUI.enabled =
                oldEnabled;
        }

        private static Rect FitRectPreserveAspect(
            Rect bounds,
            float aspect)
        {
            if (aspect <= 0f)
                return bounds;

            float width =
                bounds.width;

            float height =
                width /
                aspect;

            if (height >
                bounds.height)
            {
                height =
                    bounds.height;

                width =
                    height *
                    aspect;
            }

            return new Rect(
                bounds.x +
                (bounds.width - width) *
                .5f,
                bounds.y +
                (bounds.height - height) *
                .5f,
                width,
                height);
        }

        private async void LoadShopIcons(
            InventoryController controller)
        {
            if (_shopIconsLoading ||
                controller == null)
            {
                return;
            }

            CasinoShopItem[] items =
                _shopItems
                ?? Array.Empty<CasinoShopItem>();

            if (items.Length == 0)
                return;

            _shopIconsLoading =
                true;

            try
            {
                EFT.ItemFactory factory =
                    Singleton<EFT.ItemFactory>.Instance;

                if (factory == null)
                {
                    Plugin.Log?.LogWarning(
                        "ItemFactory unavailable; shop icons will use text fallback.");

                    return;
                }

                foreach (CasinoShopItem shopItem in
                         items)
                {
                    if (shopItem == null ||
                        string.IsNullOrWhiteSpace(
                            shopItem.TemplateId) ||
                        _shopSprites.ContainsKey(
                            shopItem.TemplateId))
                    {
                        continue;
                    }

                    try
                    {
                        Item item =
                            factory.CreateItem(
                                ((IDatabaseIdGenerator)controller).NextId,
                                shopItem.TemplateId,
                                null);

                        if (item == null)
                            continue;

                        Sprite sprite =
                            await ItemViewFactory.GetItemSpriteAsync(
                                item,
                                1);

                        if (sprite != null)
                        {
                            _shopSprites[
                                shopItem.TemplateId] =
                                sprite;

                            Plugin.Log?.LogInfo(
                                $"Loaded casino shop icon: {shopItem.DisplayName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning(
                            $"Shop icon failed for {shopItem.DisplayName} ({shopItem.TemplateId}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Casino shop icon preload failed: {ex}");
            }
            finally
            {
                _shopIconsLoading =
                    false;
            }
        }

        private void BeginShopPurchase(
            InventoryController controller,
            CasinoShopItem item)
        {
            if (controller == null ||
                item == null ||
                _transactionPending ||
                _shopPurchasePending)
            {
                return;
            }

            int balance =
                CurrencyService.GetBalance(
                    controller,
                    CurrencyService.Gp);

            int cost =
                Math.Max(
                    1,
                    item.ChipCost);

            if (balance < cost)
            {
                _shopStatus =
                    "NOT ENOUGH CASINO CHIPS";
                return;
            }

            _shopPurchasePending = true;
            _transactionPending = true;
            _shopStatus = "PROCESSING PURCHASE...";

            StartCoroutine(
                CasinoItemEventClient.SendShopBuy(
                    controller,
                    item.TemplateId,
                    CurrencyService.GetStackMax(
                        controller,
                        CurrencyService.Gp),
                    (requestId, eventResult) =>
                    {
                        if (eventResult == null ||
                            !eventResult.Success)
                        {
                            _shopPurchasePending = false;
                            _transactionPending = false;
                            RefreshBalance(controller);
                            _shopStatus =
                                eventResult?.Error
                                ?? "SHOP INVENTORY TRANSACTION FAILED";
                            return;
                        }

                        StartCoroutine(
                            FinishShopPurchase(
                                controller,
                                controller.ID,
                                requestId));
                    }));
        }

        private IEnumerator FinishShopPurchase(
            InventoryController controller,
            string profileId,
            string requestId)
        {
            CasinoShopPurchaseResponse response = null;

            yield return SlotServerClient.GetShopPurchaseResult(
                profileId,
                requestId,
                result => response = result);

            _shopPurchasePending = false;
            _transactionPending = false;

            RefreshBalance(controller);

            if (response == null ||
                !response.Success)
            {
                _shopStatus =
                    response?.Message
                    ?? "SHOP RESULT UNAVAILABLE";
                yield break;
            }

            _shopStatus =
                response.Message
                ?? "PURCHASE COMPLETE";

            PlayNativeUISound(
                EUISoundType.TradeOperationComplete);
        }

        private void DrawStatsTab(float width,float height)
        {
            GUI.Label(
                new Rect(
                    42f,
                    176f,
                    width-84f,
                    34f),
                "CASINO STATS",
                _titleStyle);

            float tabsY =
                218f;

            if(GUI.Button(
                new Rect(width*.5f-155f,tabsY,145f,32f),
                "CAREER",
                _statsShowHistory
                    ? _smallButtonStyle
                    : _blackjackButtonStyle))
            {
                _statsShowHistory=false;
            }

            if(GUI.Button(
                new Rect(width*.5f+10f,tabsY,145f,32f),
                "HISTORY",
                _statsShowHistory
                    ? _blackjackButtonStyle
                    : _smallButtonStyle))
            {
                _statsShowHistory=true;
            }

            if(_statsLoading&&_casinoStats==null)
            {
                GUI.Label(
                    new Rect(42f,274f,width-84f,34f),
                    "LOADING STATS...",
                    _statusStyle);

                return;
            }

            if(_casinoStats==null)
            {
                GUI.Label(
                    new Rect(42f,274f,width-84f,34f),
                    "NO STATS AVAILABLE",
                    _statusStyle);

                return;
            }

            if(_statsShowHistory)
                DrawStatsHistory(width,height);
            else
                DrawCareerStats(width,height);
        }

        private void DrawCareerStats(float width,float height)
        {
            float left=62f;
            float right=width*.52f;
            float y=282f;

            GUI.Label(
                new Rect(left,y,420f,32f),
                "SLOTS",
                _statusStyle);

            GUI.Label(
                new Rect(right,y,420f,32f),
                "BLACKJACK",
                _statusStyle);

            y+=44f;

            DrawStatLine(left,y,"SPINS",_casinoStats.SlotSpins.ToString("N0"));
            DrawStatLine(right,y,"HANDS",_casinoStats.BlackjackHands.ToString("N0")); y+=34f;

            DrawStatLine(left,y,"CHIPS WAGERED",_casinoStats.GpWagered.ToString("N0"));
            DrawStatLine(right,y,"W / L / P",$"{_casinoStats.BlackjackWins:N0} / {_casinoStats.BlackjackLosses:N0} / {_casinoStats.BlackjackPushes:N0}"); y+=34f;

            DrawStatLine(left,y,"CHIPS RETURNED",_casinoStats.GpReturned.ToString("N0"));
            DrawStatLine(right,y,"NATURAL BLACKJACKS",_casinoStats.NaturalBlackjacks.ToString("N0")); y+=34f;

            DrawStatLine(left,y,"NET CHIPS",FormatSigned(_casinoStats.GpNet));
            DrawStatLine(right,y,"₽ WAGERED",$"₽{_casinoStats.RoublesWagered:N0}"); y+=34f;

            DrawStatLine(left,y,"BIGGEST SLOT RETURN",_casinoStats.BiggestSlotReturn.ToString("N0"));
            DrawStatLine(right,y,"₽ RETURNED",$"₽{_casinoStats.RoublesReturned:N0}"); y+=34f;

            DrawStatLine(left,y,"JACKPOTS WON",_casinoStats.JackpotsWon.ToString("N0"));
            DrawStatLine(right,y,"NET ₽",FormatSignedRoubles(_casinoStats.RoublesNet)); y+=34f;

            DrawStatLine(left,y,"BIGGEST JACKPOT",_casinoStats.BiggestJackpot.ToString("N0"));
            DrawStatLine(right,y,"BIGGEST BJ PROFIT",$"₽{_casinoStats.BiggestBlackjackProfit:N0}"); y+=34f;

            DrawStatLine(right,y,"INSURANCE W / BETS",$"{_casinoStats.InsuranceWins:N0} / {_casinoStats.InsuranceBets:N0}");

            GUI.Label(
                new Rect(42f,height-72f,width-84f,26f),
                "SERVER-PERSISTENT • PER PROFILE",
                _subtitleStyle);
        }

        private void DrawStatsHistory(float width,float height)
        {
            CasinoHistoryEntry[] history=
                _casinoStats.RecentHistory
                ?? Array.Empty<CasinoHistoryEntry>();

            GUI.Label(
                new Rect(62f,282f,width-124f,28f),
                "RECENT ACTIVITY • NEWEST FIRST",
                _statusStyle);

            if(history.Length==0)
            {
                GUI.Label(
                    new Rect(62f,328f,width-124f,28f),
                    "NO RECENT CASINO ACTIVITY YET",
                    _subtitleStyle);

                return;
            }

            float y=326f;

            foreach(CasinoHistoryEntry entry in history.Take(9))
            {
                bool blackjack=
                    string.Equals(
                        entry.Type,
                        "BLACKJACK",
                        StringComparison.OrdinalIgnoreCase);

                string net=
                    blackjack
                        ? FormatSignedRoubles(entry.Net)
                        : FormatSigned(entry.Net)+" CHIPS";

                string title=
                    blackjack
                        ? $"BLACKJACK • {entry.Result}"
                        : $"SLOTS • {entry.Result}";

                GUI.Label(
                    new Rect(68f,y,width*.32f,26f),
                    title,
                    _blackjackPlayerStyle);

                GUI.Label(
                    new Rect(width*.34f,y,width*.25f,26f),
                    blackjack
                        ? $"BET ₽{entry.Wager:N0}"
                        : $"BET {entry.Wager:N0} CHIPS",
                    _subtitleStyle);

                GUI.Label(
                    new Rect(width*.58f,y,width*.18f,26f),
                    net,
                    entry.Net>0
                        ? _balanceStyle
                        : _subtitleStyle);

                GUI.Label(
                    new Rect(width*.76f,y,width*.20f,26f),
                    entry.Detail??"",
                    _subtitleStyle);

                y+=34f;
            }

            GUI.Label(
                new Rect(42f,height-72f,width-84f,26f),
                "LAST 20 EVENTS ARE SAVED • SHOWING 9",
                _subtitleStyle);
        }

        private void DrawStatLine(float x,float y,string label,string value)
        {
            GUI.Label(new Rect(x,y,235f,28f),label,_subtitleStyle);
            GUI.Label(new Rect(x+235f,y,190f,28f),value,_balanceStyle);
        }

        private static string FormatSigned(long value)
            => value>=0?$"+{value:N0}":value.ToString("N0");

        private static string FormatSignedRoubles(long value)
            => value>=0?$"+₽{value:N0}":$"-₽{Math.Abs(value):N0}";

        private void DrawBlackjackRulesOverlay(
            float width,
            float height)
        {
            Rect panel=
                new Rect(
                    width*.5f-350f,
                    285f,
                    700f,
                    330f);

            GUI.DrawTexture(
                panel,
                _panelTexture,
                ScaleMode.StretchToFill,
                true);

            GUI.Label(
                new Rect(panel.x+24f,panel.y+18f,500f,34f),
                "PEP'S CASINO BLACKJACK RULES",
                _titleStyle);

            if(GUI.Button(
                new Rect(panel.x+610f,panel.y+16f,66f,30f),
                "CLOSE",
                _smallButtonStyle))
            {
                _showBlackjackRules=false;
                return;
            }

            string rules=
                "• 6-deck shoe. Dealer stands on all 17 (S17).\\n"+
                "• Natural Blackjack pays 3:2. Regular wins pay 1:1.\\n"+
                "• Double is allowed on any first two cards, including after a split.\\n"+
                "• Matching-rank pairs may be split up to 4 total hands.\\n"+
                "• Split aces receive one card each and automatically stand.\\n"+
                "• A split-hand 21 is a normal 21, not a natural Blackjack.\\n"+
                "• Insurance is offered when the dealer shows an Ace and pays 2:1 profit.\\n"+
                "• No surrender.\\n"+
                "• Each active decision has a 60-second timer. Timeout forfeits the hand and removes you from the table.\\n"+
                $"• Table limits: ₽{_blackjackMinBet:N0} - ₽{_blackjackMaxBet:N0}.";

            GUI.Label(
                new Rect(
                    panel.x+30f,
                    panel.y+68f,
                    panel.width-60f,
                    panel.height-92f),
                rules,
                _blackjackPlayerStyle);
        }

        private void DrawBlackjackTab(float width,float height)
        {
            InventoryController controller;
            bool ok=CurrencyService.TryGetActiveCharacterInventoryController(out controller);
            int rub=ok?CurrencyService.GetBalance(controller,CurrencyService.Roubles):0;

            GUI.Label(new Rect(42f,178f,420f,34f),$"ROUBLES: ₽{rub:N0}",_balanceStyle);
            GUI.Label(new Rect(width*.5f-390f,202f,780f,42f),_blackjackStatus,_blackjackStatusStyle);

            if(_currentBlackjackRoom==null)
            {
                if(GUI.Button(new Rect(42f,252f,180f,42f),"HOST TABLE",_blackjackButtonStyle))
                    HostBlackjackTable(controller);

                GUI.Label(new Rect(42f,310f,460f,30f),"OPEN TABLES",_statusStyle);
                var rooms=_blackjackLobby?.Rooms??Array.Empty<BlackjackRoomState>();
                float y=348f;

                if(rooms.Length==0)
                    GUI.Label(new Rect(42f,y,520f,28f),"NO HOSTED TABLES",_subtitleStyle);

                foreach(var room in rooms.Take(8))
                {
                    int n=room.Players?.Length??0;
                    GUI.Label(new Rect(58f,y+8f,500f,28f),$"{room.HostName} // TABLE {room.RoomId} // {n}/5",_subtitleStyle);
                    if(n<5&&GUI.Button(new Rect(width-182f,y+3f,120f,32f),"JOIN",_smallButtonStyle))
                        JoinBlackjackTable(controller,room.RoomId);
                    y+=44f;
                }
                return;
            }

            var r=_currentBlackjackRoom;
            var me=r.Players?.FirstOrDefault(x=>x.ProfileId==controller?.ID);

            GUI.Label(new Rect(42f,244f,400f,30f),$"TABLE {r.RoomId}",_titleStyle);

            string tableMeta=
                $"HOST: {r.HostName}  •  PLAYERS {(r.Players?.Length??0)}/5";

            if(_blackjackDiagnostics)
                tableMeta += $"  •  REV {r.StateRevision}";

            GUI.Label(
                new Rect(42f,272f,520f,22f),
                tableMeta,
                _subtitleStyle);

            GUI.Label(new Rect(width-520f,246f,400f,26f),"6 DECK • S17 • BLACKJACK 3:2 • DOUBLE ANY 2",_subtitleStyle);

            if(_blackjackDiagnostics)
            {
                GUI.Label(
                    new Rect(width-190f,246f,72f,22f),
                    "DIAG ON",
                    _blackjackPlayerTurnStyle);
            }

            if(GUI.Button(
                new Rect(width-108f,240f,66f,30f),
                "RULES",
                _smallButtonStyle))
            {
                _showBlackjackRules=
                    !_showBlackjackRules;
            }

            string phaseText =
                r.Phase=="WAITING" ? "PLACE YOUR BET" :
                r.Phase=="INSURANCE" && me!=null && r.ActiveSeat==me.Seat ? "INSURANCE?" :
                r.Phase=="INSURANCE" ? "INSURANCE DECISION" :
                r.Phase=="PLAYER" && me!=null && r.ActiveSeat==me.Seat ? "YOUR TURN" :
                r.Phase=="PLAYER" ? "WAITING FOR PLAYER" :
                r.Phase=="DEALER" ? "DEALER TURN" :
                r.Phase=="RESOLVED" ? "HAND COMPLETE" :
                r.Phase??"";

            GUI.Label(new Rect(width*.5f-220f,278f,440f,32f),phaseText,_blackjackStatusStyle);

            if((r.Phase=="PLAYER" || r.Phase=="INSURANCE") && r.TurnDeadlineUtc.HasValue)
            {
                int seconds=
                    Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (r.TurnDeadlineUtc.Value.ToUniversalTime()-DateTime.UtcNow)
                            .TotalSeconds));

                string timerText=
                    me!=null && r.ActiveSeat==me.Seat
                        ? $"YOUR TURN: {seconds}s"
                        : $"TURN TIMER: {seconds}s";

                GUI.Label(
                    new Rect(width*.5f-180f,308f,360f,26f),
                    timerText,
                    seconds<=10?_blackjackPlayerTurnStyle:_subtitleStyle);
            }

            GUI.Label(new Rect(width*.5f-150f,334f,300f,28f),"DEALER",_statusStyle);

            bool hideDealerHole=r.Phase=="PLAYER";
            DrawBlackjackCards(r.DealerCards??Array.Empty<BlackjackCard>(),width*.5f,364f,hideDealerHole);

            var players=r.Players??Array.Empty<BlackjackPlayerState>();
            float[] sx={190f,440f,width*.5f,width-440f,width-190f};
            float py=492f;

            foreach(var player in players.OrderBy(x=>x.Seat))
            {
                float px=sx[Math.Max(0,Math.Min(4,player.Seat))];
                bool turn=
                    (r.Phase=="PLAYER"||r.Phase=="INSURANCE")&&
                    r.ActiveSeat==player.Seat;

                string name=
                    player.DisplayName+
                    (player.IsHost?" [HOST]":"")+
                    (turn?" ◀ TURN":"");

                GUI.Label(
                    new Rect(px-130f,py,260f,28f),
                    name,
                    turn?_blackjackPlayerTurnStyle:_blackjackPlayerStyle);

                string playerState;

                if(turn)
                    playerState="ACTIVE";
                else if(r.Phase is "PLAYER" or "INSURANCE" or "DEALER")
                    playerState=
                        player.Ready&&player.Hands!=null&&player.Hands.Length>0
                            ? (player.TurnComplete?"DONE":"WAITING")
                            : "NEXT HAND";
                else
                    playerState=
                        player.Ready&&player.Wager>0
                            ? "READY"
                            : "NO BET";

                string wagerText=
                    player.Wager>0
                        ? $"BET ₽{player.Wager:N0}  •  {playerState}"
                        : playerState;

                if(!string.IsNullOrEmpty(player.Result))
                    wagerText += $"  //  {player.Result}";

                GUI.Label(
                    new Rect(px-130f,py+28f,260f,24f),
                    wagerText,
                    turn?_blackjackPlayerTurnStyle:_blackjackPlayerStyle);

                var hands=player.Hands??Array.Empty<BlackjackHandState>();

                if(hands.Length<=1)
                {
                    var cards=
                        hands.Length==1
                            ? hands[0].Cards
                            : player.Cards;

                    DrawBlackjackCards(
                        cards??Array.Empty<BlackjackCard>(),
                        px,
                        py+56f,
                        false,
                        true);

                    if(r.Phase=="RESOLVED" &&
                       hands.Length==1 &&
                       !string.IsNullOrEmpty(hands[0].Result))
                    {
                        GUI.Label(
                            new Rect(px-130f,py+122f,260f,24f),
                            hands[0].Result,
                            GetBlackjackHandResultStyle(
                                hands[0].Result));
                    }
                }
                else
                {
                    for(int hi=0;hi<hands.Length;hi++)
                    {
                        var hand=hands[hi];
                        bool active=
                            turn &&
                            player.ActiveHandIndex==hi;

                        string handLabel=
                            $"H{hi+1} ₽{hand.Wager:N0}";

                        if(!string.IsNullOrEmpty(hand.Result))
                            handLabel += $" {hand.Result}";

                        GUIStyle handLabelStyle=
                            active
                                ? _blackjackPlayerTurnStyle
                                : GetBlackjackHandResultStyle(
                                    hand.Result);

                        GUI.Label(
                            new Rect(px-130f,py+52f+hi*70f,260f,20f),
                            handLabel+(active?" ◀":""),
                            handLabelStyle);

                        DrawBlackjackCards(
                            hand.Cards??Array.Empty<BlackjackCard>(),
                            px,
                            py+72f+hi*70f,
                            false,
                            true);
                    }
                }
            }

            if(r.Phase=="RESOLVED" && me!=null)
            {
                string result=me.Result??"";
                int net=me.Payout-me.Wager;
                string resultText =
                    (me.Hands?.Length??0)>1
                        ? (net>0
                            ? $"SPLIT RESULT   +₽{net:N0}"
                            : net<0
                                ? $"SPLIT RESULT   -₽{Math.Abs(net):N0}"
                                : "SPLIT RESULT   PUSH")
                        : result=="BLACKJACK"
                            ? $"BLACKJACK!   +₽{Math.Max(0,net):N0}"
                            : result=="WIN"
                                ? $"YOU WIN   +₽{Math.Max(0,net):N0}"
                                : result=="PUSH"
                                    ? "PUSH   //   WAGER RETURNED"
                                    : $"YOU LOSE   -₽{Math.Abs(Math.Min(0,net)):N0}";

                GUI.Label(
                    new Rect(width*.5f-360f,height-226f,720f,46f),
                    resultText,
                    _blackjackResultStyle);

                if(me.InsuranceWager>0)
                {
                    string insuranceText=
                        me.InsurancePayout>0
                            ? $"INSURANCE WIN   +₽{me.InsurancePayout-me.InsuranceWager:N0}"
                            : $"INSURANCE LOST   -₽{me.InsuranceWager:N0}";

                    GUI.Label(
                        new Rect(width*.5f-300f,height-188f,600f,24f),
                        insuranceText,
                        me.InsurancePayout>0
                            ? _blackjackHandWinStyle
                            : _blackjackHandLoseStyle);
                }

                GUI.Label(
                    new Rect(width*.5f-300f,height-158f,600f,24f),
                    "RESULT DISPLAY • NEXT BETTING ROUND OPENS AUTOMATICALLY",
                    _subtitleStyle);
            }

            float cy=height-112f;

            if(!string.IsNullOrEmpty(r.LastNotice))
            {
                GUI.Label(
                    new Rect(
                        width*.5f-320f,
                        cy-86f,
                        640f,
                        22f),
                    r.LastNotice,
                    _subtitleStyle);
            }

            if(r.Phase=="WAITING")
            {
                GUI.Label(
                    new Rect(42f,cy-58f,640f,24f),
                    $"TABLE LIMITS  ₽{_blackjackMinBet:N0} - ₽{_blackjackMaxBet:N0}",
                    _subtitleStyle);

                if(Plugin.SoloAutoDeal!=null &&
                   Plugin.SoloAutoDeal.Value &&
                   r.Players!=null &&
                   r.Players.Length==1 &&
                   me!=null &&
                   me.IsHost)
                {
                    GUI.Label(
                        new Rect(width-390f,cy-58f,348f,24f),
                        "SOLO AUTO DEAL: ON",
                        _subtitleStyle);
                }

                int[] presets=
                    BuildBlackjackBetPresets(
                        _blackjackMinBet,
                        _blackjackMaxBet);

                float presetX=42f;

                foreach(int preset in presets)
                {

                    bool oldPresetEnabled=GUI.enabled;
                    GUI.enabled=
                        !_blackjackRequestPending &&
                        rub>=preset;

                    if(GUI.Button(
                        new Rect(presetX,cy-30f,92f,30f),
                        FormatBlackjackBetButton(preset),
                        _blackjackButtonStyle))
                    {
                        _blackjackBet=preset;
                    }

                    GUI.enabled=oldPresetEnabled;
                    presetX+=100f;
                }

                GUI.Label(
                    new Rect(42f,cy+8f,180f,34f),
                    $"BET ₽{_blackjackBet:N0}",
                    _blackjackStatusStyle);

                bool oldEnabled=GUI.enabled;

                GUI.enabled=
                    !_blackjackRequestPending &&
                    _blackjackBet>=_blackjackMinBet &&
                    _blackjackBet<=_blackjackMaxBet &&
                    rub>=_blackjackBet;

                if(GUI.Button(
                    new Rect(232f,cy+4f,120f,38f),
                    "BET",
                    _blackjackButtonStyle))
                {
                    SetBlackjackBet(
                        controller,
                        _blackjackBet);
                }

                int rebet=
                    me?.LastBaseBet
                    ?? 0;

                GUI.enabled=
                    !_blackjackRequestPending &&
                    rebet>=_blackjackMinBet &&
                    rebet<=_blackjackMaxBet &&
                    rub>=rebet;

                if(GUI.Button(
                    new Rect(362f,cy+4f,140f,38f),
                    rebet>0
                        ? $"REBET ₽{rebet:N0}"
                        : "REBET",
                    _blackjackButtonStyle))
                {
                    _blackjackBet=
                        rebet;

                    SetBlackjackBet(
                        controller,
                        rebet);
                }

                GUI.enabled=oldEnabled;

                if(me!=null &&
                   me.IsHost)
                {
                    bool canDeal=
                        r.Players!=null &&
                        r.Players.Any(
                            x=>x.Ready&&x.Wager>0);

                    GUI.enabled=
                        !_blackjackRequestPending &&
                        canDeal;

                    if(GUI.Button(
                        new Rect(width-365f,cy,130f,38f),
                        "DEAL",
                        _blackjackButtonStyle))
                    {
                        BlackjackDo(
                            controller,
                            "/pep-casino/blackjack/deal");
                    }

                    GUI.enabled=oldEnabled;
                }
            }
            else if(r.Phase=="INSURANCE"&&me!=null&&r.ActiveSeat==me.Seat)
            {
                bool oldEnabled=GUI.enabled;
                int insuranceCost=
                    me.Hands!=null&&me.Hands.Length>0
                        ? me.Hands[0].Wager/2
                        : 0;

                GUI.enabled=
                    !_blackjackRequestPending &&
                    !me.InsuranceDecisionMade &&
                    insuranceCost>0 &&
                    rub>=insuranceCost;

                if(GUI.Button(
                    new Rect(42f,cy,190f,38f),
                    $"INSURE ₽{insuranceCost:N0}",
                    _blackjackButtonStyle))
                {
                    BlackjackInsurance(controller,true);
                }

                GUI.enabled=
                    !_blackjackRequestPending &&
                    !me.InsuranceDecisionMade;

                if(GUI.Button(
                    new Rect(244f,cy,190f,38f),
                    "NO INSURANCE",
                    _blackjackButtonStyle))
                {
                    BlackjackInsurance(controller,false);
                }

                GUI.enabled=oldEnabled;
            }
            else if(r.Phase=="PLAYER"&&me!=null&&r.ActiveSeat==me.Seat)
            {
                bool oldEnabled=GUI.enabled;

                BlackjackHandState activeHand=null;

                if(me.Hands!=null &&
                   me.ActiveHandIndex>=0 &&
                   me.ActiveHandIndex<me.Hands.Length)
                {
                    activeHand=me.Hands[me.ActiveHandIndex];
                }

                bool handLive=
                    activeHand!=null &&
                    !activeHand.TurnComplete &&
                    !activeHand.Busted;

                GUI.enabled=
                    !_blackjackRequestPending &&
                    handLive;

                if(GUI.Button(new Rect(42f,cy,100f,38f),"HIT",_blackjackButtonStyle))
                    BlackjackDo(controller,"/pep-casino/blackjack/hit");

                if(GUI.Button(new Rect(152f,cy,100f,38f),"STAND",_blackjackButtonStyle))
                    BlackjackDo(controller,"/pep-casino/blackjack/stand");

                GUI.enabled=
                    !_blackjackRequestPending &&
                    handLive &&
                    activeHand.Cards?.Length==2 &&
                    !activeHand.IsSplitAce &&
                    rub>=activeHand.Wager;

                if(GUI.Button(new Rect(262f,cy,110f,38f),"DOUBLE",_blackjackButtonStyle))
                    BlackjackDo(controller,"/pep-casino/blackjack/double");

                bool pair=
                    activeHand!=null &&
                    activeHand.Cards!=null &&
                    activeHand.Cards.Length==2 &&
                    activeHand.Cards[0].Rank==
                        activeHand.Cards[1].Rank;

                GUI.enabled=
                    !_blackjackRequestPending &&
                    handLive &&
                    pair &&
                    !activeHand.IsSplitAce &&
                    (me.Hands?.Length??0)<4 &&
                    rub>=activeHand.Wager;

                if(GUI.Button(new Rect(382f,cy,110f,38f),"SPLIT",_blackjackButtonStyle))
                    BlackjackDo(controller,"/pep-casino/blackjack/split");

                GUI.enabled=oldEnabled;
            }

            bool leaveEnabled=GUI.enabled;
            GUI.enabled=!_blackjackRequestPending;
            if(GUI.Button(new Rect(width-215f,cy,170f,38f),"LEAVE TABLE",_blackjackButtonStyle))
                LeaveBlackjackTable(controller);
            GUI.enabled=leaveEnabled;

            if(_showBlackjackRules)
                DrawBlackjackRulesOverlay(width,height);
        }

        private static int[] BuildBlackjackBetPresets(int minBet,int maxBet)
        {
            minBet=Math.Max(1,minBet);
            maxBet=Math.Max(minBet,maxBet);

            if(minBet==maxBet)
                return new[]{minBet};

            var values=new List<int>{minBet};
            double span=maxBet-minBet;

            for(int i=1;i<4;i++)
            {
                int raw=(int)Math.Round(minBet+(span*i/4d));
                int rounded=RoundBlackjackPreset(raw);
                rounded=Math.Max(minBet,Math.Min(maxBet,rounded));

                if(!values.Contains(rounded))
                    values.Add(rounded);
            }

            if(!values.Contains(maxBet))
                values.Add(maxBet);

            return values
                .Distinct()
                .OrderBy(x=>x)
                .Take(5)
                .ToArray();
        }

        private static int RoundBlackjackPreset(int value)
        {
            int step=
                value>=1000000?100000:
                value>=100000?10000:
                value>=10000?5000:
                value>=1000?1000:
                value>=100?100:10;

            return Math.Max(1,(int)Math.Round(value/(double)step)*step);
        }

        private static string FormatBlackjackBetButton(int amount)
        {
            if(amount>=1000000)
                return $"₽{amount/1000000d:0.##}M";

            if(amount>=1000)
                return $"₽{amount/1000d:0.##}K";

            return $"₽{amount:N0}";
        }

        private GUIStyle GetBlackjackHandResultStyle(
            string result)
        {
            switch (
                result?.ToUpperInvariant())
            {
                case "BLACKJACK":
                case "WIN":
                    return _blackjackHandWinStyle;

                case "PUSH":
                    return _blackjackHandPushStyle;

                case "LOSE":
                case "BUST":
                    return _blackjackHandLoseStyle;

                default:
                    return _subtitleStyle;
            }
        }

        private void ScheduleSoloAutoDeal(
            InventoryController controller,
            BlackjackRoomState room)
        {
            if (controller == null ||
                room == null ||
                Plugin.SoloAutoDeal == null ||
                !Plugin.SoloAutoDeal.Value ||
                _soloAutoDealScheduled)
            {
                return;
            }

            BlackjackPlayerState me =
                room.Players?
                    .FirstOrDefault(
                        x => x.ProfileId == controller.ID);

            bool soloReady =
                room.Phase == "WAITING" &&
                room.Players != null &&
                room.Players.Length == 1 &&
                me != null &&
                me.IsHost &&
                me.Ready &&
                me.Wager > 0;

            if (!soloReady)
                return;

            _soloAutoDealScheduled =
                true;

            _soloAutoDealRoomId =
                room.RoomId;

            StartCoroutine(
                SoloAutoDealCoroutine(
                    controller,
                    room.RoomId));
        }

        private IEnumerator SoloAutoDealCoroutine(
            InventoryController controller,
            string roomId)
        {
            yield return
                new WaitForSecondsRealtime(
                    .85f);

            _soloAutoDealScheduled =
                false;

            if (controller == null ||
                _currentBlackjackRoom == null ||
                _blackjackRequestPending ||
                _currentBlackjackRoom.RoomId != roomId ||
                _currentBlackjackRoom.Phase != "WAITING")
            {
                yield break;
            }

            BlackjackPlayerState me =
                _currentBlackjackRoom.Players?
                    .FirstOrDefault(
                        x => x.ProfileId == controller.ID);

            if (_currentBlackjackRoom.Players == null ||
                _currentBlackjackRoom.Players.Length != 1 ||
                me == null ||
                !me.IsHost ||
                !me.Ready ||
                me.Wager <= 0)
            {
                yield break;
            }

            BlackjackDo(
                controller,
                "/pep-casino/blackjack/deal");
        }

        private void DrawBlackjackCards(BlackjackCard[] cards,float cx,float y,bool hideHole,bool compact=false)
        {
            cards=cards??Array.Empty<BlackjackCard>();
            float w=compact?68f:96f,h=compact?58f:116f,g=compact?10f:14f;
            float total=cards.Length==0?0:cards.Length*w+(cards.Length-1)*g;
            float x=cx-total*.5f;
            for(int i=0;i<cards.Length;i++)
            {
                GUI.Box(new Rect(x,y,w,h),GUIContent.none,_blackjackButtonStyle);
                string suit=cards[i].Suit=="H"?"♥":cards[i].Suit=="D"?"♦":cards[i].Suit=="C"?"♣":"♠";
                GUI.Label(new Rect(x,y,w,h),hideHole&&i==1?"?":cards[i].Rank+suit,compact?_blackjackCardCompactStyle:_blackjackCardStyle);
                x+=w+g;
            }
        }

        private void ApplyBlackjackBalance(
            InventoryController controller,
            BlackjackRoomActionResult result)
        {
            if (controller == null ||
                result == null ||
                result.Balance < 0)
            {
                return;
            }

            CurrencyService.MirrorServerBalance(
                controller,
                CurrencyService.Roubles,
                result.Balance);
        }

        private void SetBlackjackBet(InventoryController c,int bet)
        {
            if(c==null||_currentBlackjackRoom==null||_blackjackRequestPending)return;

            _blackjackRequestPending=true;

            int max=
                CurrencyService.GetStackMax(
                    c,
                    CurrencyService.Roubles);

            StartCoroutine(
                SlotServerClient.SetBlackjackBet(
                    _currentBlackjackRoom.RoomId,
                    c.ID,
                    bet,
                    max,
                    r =>
                    {
                        _blackjackRequestPending=false;

                        if(r==null)
                            return;

                        ApplyBlackjackBalance(
                            c,
                            r);

                        _blackjackStatus=
                            (r.Room!=null&&!string.IsNullOrEmpty(r.Room.Message))
                                ? r.Room.Message
                                : (r.Message??"");

                        if(r.Success&&r.Room!=null)
                        {
                            _currentBlackjackRoom=
                                r.Room;

                            if(r.Room.Phase=="RESOLVED")
                                _statsDirty=true;

                            BlackjackVoiceService.ProcessTableTaunts(
                                this,
                                c,
                                r.Room);

                            ScheduleSoloAutoDeal(
                                c,
                                r.Room);
                        }
                    }));
        }
        private void BlackjackInsurance(
            InventoryController controller,
            bool take)
        {
            if(controller==null||_currentBlackjackRoom==null||_blackjackRequestPending)return;

            _blackjackRequestPending=true;

            StartCoroutine(
                SlotServerClient.BlackjackInsurance(
                    _currentBlackjackRoom.RoomId,
                    controller.ID,
                    take,
                    result =>
                    {
                        _blackjackRequestPending=false;
                        if(result==null)return;

                        ApplyBlackjackBalance(controller,result);

                        _blackjackStatus=
                            result.Room!=null&&!string.IsNullOrEmpty(result.Room.Message)
                                ? result.Room.Message
                                : (result.Message??"");

                        if(!result.Success)return;

                        if(result.Room!=null)
                        {
                            _currentBlackjackRoom=result.Room;

                            if(result.Room.Phase=="RESOLVED")
                                _statsDirty=true;

                            BlackjackVoiceService.ProcessTableTaunts(
                                this,
                                controller,
                                result.Room);
                        }
                    }));
        }

        private void BlackjackDo(InventoryController c,string path)
        {
            if(c==null||_currentBlackjackRoom==null||_blackjackRequestPending)return;

            _blackjackRequestPending=true;

            int max=
                CurrencyService.GetStackMax(
                    c,
                    CurrencyService.Roubles);

            StartCoroutine(
                SlotServerClient.BlackjackAction(
                    path,
                    _currentBlackjackRoom.RoomId,
                    c.ID,
                    max,
                    r =>
                    {
                        _blackjackRequestPending=false;

                        if(r==null)
                            return;

                        ApplyBlackjackBalance(
                            c,
                            r);

                        _blackjackStatus=
                            (r.Room!=null&&!string.IsNullOrEmpty(r.Room.Message))
                                ? r.Room.Message
                                : (r.Message??"");

                        if(!r.Success)
                        {
                            if(!string.IsNullOrEmpty(r.Message) &&
                               r.Message.IndexOf(
                                   "KICKED",
                                   StringComparison.OrdinalIgnoreCase)>=0)
                            {
                                _currentBlackjackRoom=null;
                                _nextBlackjackLobbyPollAt=0f;
                            }

                            return;
                        }

                        if(r.Room!=null)
                        {
                            _currentBlackjackRoom=r.Room;

                            if(r.Room.Phase=="RESOLVED")
                                _statsDirty=true;

                            BlackjackVoiceService.ProcessTableTaunts(
                                this,
                                c,
                                r.Room);
                        }
                    }));
        }

        private string GetPlayerDisplayName(
            InventoryController controller)
        {
            try
            {
                return controller?.Profile?.Nickname
                    ?? controller?.ID
                    ?? "Player";
            }
            catch
            {
                return controller?.ID
                    ?? "Player";
            }
        }

        private void HostBlackjackTable(
            InventoryController controller)
        {
            if (controller == null)
            {
                _blackjackStatus =
                    "CHARACTER INVENTORY REQUIRED";
                return;
            }

            _blackjackStatus =
                "HOSTING...";

            StartCoroutine(
                SlotServerClient.HostBlackjack(
                    controller.ID,
                    GetPlayerDisplayName(controller),
                    result =>
                    {
                        if (result == null)
                        {
                            return;
                        }

                        _blackjackStatus =
                            result.Message
                            ?? string.Empty;

                        if (result.Success)
                        {
                            _currentBlackjackRoom =
                                result.Room;
                        }
                    }));
        }

        private void JoinBlackjackTable(
            InventoryController controller,
            string roomId)
        {
            if (controller == null ||
                string.IsNullOrEmpty(roomId))
            {
                return;
            }

            _blackjackStatus =
                "JOINING...";

            StartCoroutine(
                SlotServerClient.JoinBlackjack(
                    roomId,
                    controller.ID,
                    GetPlayerDisplayName(controller),
                    result =>
                    {
                        if (result == null)
                        {
                            return;
                        }

                        _blackjackStatus =
                            result.Message
                            ?? string.Empty;

                        if (result.Success)
                        {
                            _currentBlackjackRoom =
                                result.Room;
                        }
                    }));
        }

        private void LeaveBlackjackTable(
            InventoryController controller)
        {
            if (controller == null ||
                _currentBlackjackRoom == null)
            {
                return;
            }

            string roomId =
                _currentBlackjackRoom.RoomId;

            StartCoroutine(
                SlotServerClient.LeaveBlackjack(
                    roomId,
                    controller.ID,
                    result =>
                    {
                        _blackjackStatus =
                            result?.Message
                            ?? "LEFT";

                        _currentBlackjackRoom =
                            null;

                        _nextBlackjackLobbyPollAt =
                            0f;
                    }));
        }

        private void DrawReels(float width)
        {
            const float reelWidth = 165f;
            const float spacing = 15f;

            float totalWidth =
                reelWidth * ReelCount +
                spacing * (ReelCount - 1);

            float startX =
                (width - totalWidth) * .5f;

            float startY = 232f;

            bool presentingWin =
                !_spinning &&
                _lastWin > 0 &&
                _lineWins != null &&
                _lineWins.Length > 0;

            int activeLine =
                -1;

            if (presentingWin)
            {
                float elapsed =
                    Mathf.Max(
                        0f,
                        Time.unscaledTime -
                        _winPresentationStartedAt);

                activeLine =
                    Mathf.FloorToInt(
                        elapsed / 1.15f) %
                    _lineWins.Length;

                _activeWinningLineIndex =
                    activeLine;
            }

            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                float x =
                    startX +
                    reel *
                    (reelWidth + spacing);

                Rect reelRect =
                    new Rect(
                        x,
                        startY,
                        reelWidth,
                        SymbolHeight * RowsPerReel);

                Color old =
                    GUI.color;

                GUI.color =
                    new Color(
                        .02f,
                        .02f,
                        .02f,
                        1f);

                GUI.DrawTexture(
                    new Rect(
                        x - 4f,
                        startY - 4f,
                        reelWidth + 8f,
                        SymbolHeight * 3f + 8f),
                    Texture2D.whiteTexture);

                GUI.color =
                    old;

                GUI.BeginGroup(
                    reelRect);

                float offsetPixels =
                    (_reelOffsets[reel] +
                     _reelSettleOffsets[reel]) *
                    SymbolHeight;

                for (int slot = 0;
                     slot < RowsPerReel + 1;
                     slot++)
                {
                    string symbol =
                        slot < RowsPerReel
                            ? _displaySymbols[reel, slot]
                            : _incomingSymbols[reel];

                    float y =
                        slot *
                        SymbolHeight -
                        offsetPixels;

                    int visibleRow =
                        Mathf.Clamp(
                            slot,
                            0,
                            RowsPerReel - 1);

                    bool winning =
                        !_spinning &&
                        slot < RowsPerReel &&
                        _winningCells[
                            reel,
                            visibleRow];

                    bool activeWinning =
                        winning &&
                        IsCellOnActiveWinningLine(
                            reel,
                            visibleRow,
                            activeLine);

                    bool center =
                        slot == 1;

                    Color cardOld =
                        GUI.color;

                    if (presentingWin &&
                        slot < RowsPerReel &&
                        !activeWinning)
                    {
                        GUI.color =
                            winning
                                ? new Color(1f, 1f, 1f, .72f)
                                : new Color(.58f, .58f, .58f, .48f);
                    }
                    else if (activeWinning)
                    {
                        float pulse =
                            .82f +
                            .18f *
                            Mathf.Sin(
                                Time.unscaledTime *
                                8f);

                        GUI.color =
                            new Color(
                                1f,
                                pulse,
                                .48f,
                                1f);
                    }

                    DrawSymbolCard(
                        new Rect(
                            4f,
                            y + 4f,
                            reelWidth - 8f,
                            SymbolHeight - 8f),
                        symbol,
                        activeWinning || winning,
                        center);

                    GUI.color =
                        cardOld;
                }

                GUI.EndGroup();

                GUI.DrawTexture(
                    new Rect(
                        x,
                        startY + SymbolHeight - 1f,
                        reelWidth,
                        2f),
                    _dividerTexture);

                GUI.DrawTexture(
                    new Rect(
                        x,
                        startY + SymbolHeight * 2f - 1f,
                        reelWidth,
                        2f),
                    _dividerTexture);
            }

            if (presentingWin &&
                activeLine >= 0)
            {
                DrawActivePayline(
                    startX,
                    startY,
                    reelWidth,
                    spacing,
                    activeLine);
            }
        }

        private bool IsCellOnActiveWinningLine(
            int reel,
            int row,
            int activeLine)
        {
            if (activeLine < 0 ||
                _lineWins == null ||
                activeLine >= _lineWins.Length)
            {
                return false;
            }

            SlotLineWin line =
                _lineWins[activeLine];

            if (line == null ||
                line.Cells == null)
            {
                return false;
            }

            foreach (SlotCell cell in
                     line.Cells)
            {
                if (cell != null &&
                    cell.Reel == reel &&
                    cell.Row == row)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawActivePayline(
            float startX,
            float startY,
            float reelWidth,
            float spacing,
            int activeLine)
        {
            if (_lineWins == null ||
                activeLine < 0 ||
                activeLine >= _lineWins.Length)
            {
                return;
            }

            SlotLineWin line =
                _lineWins[activeLine];

            if (line == null ||
                line.Cells == null ||
                line.Cells.Length < 2)
            {
                return;
            }

            Color old =
                GUI.color;

            float pulse =
                .70f +
                .30f *
                Mathf.Sin(
                    Time.unscaledTime *
                    7f);

            GUI.color =
                new Color(
                    1f,
                    .72f,
                    .18f,
                    pulse);

            for (int i = 0;
                 i < line.Cells.Length - 1;
                 i++)
            {
                SlotCell from =
                    line.Cells[i];

                SlotCell to =
                    line.Cells[i + 1];

                if (from == null ||
                    to == null)
                {
                    continue;
                }

                Vector2 p1 =
                    new Vector2(
                        startX +
                        from.Reel *
                        (reelWidth + spacing) +
                        reelWidth * .5f,
                        startY +
                        from.Row *
                        SymbolHeight +
                        SymbolHeight * .5f);

                Vector2 p2 =
                    new Vector2(
                        startX +
                        to.Reel *
                        (reelWidth + spacing) +
                        reelWidth * .5f,
                        startY +
                        to.Row *
                        SymbolHeight +
                        SymbolHeight * .5f);

                DrawGuiLine(
                    p1,
                    p2,
                    4f);
            }

            GUI.color =
                old;
        }

        private static void DrawGuiLine(
            Vector2 from,
            Vector2 to,
            float thickness)
        {
            Vector2 delta =
                to - from;

            float angle =
                Mathf.Atan2(
                    delta.y,
                    delta.x) *
                Mathf.Rad2Deg;

            Matrix4x4 oldMatrix =
                GUI.matrix;

            GUIUtility.RotateAroundPivot(
                angle,
                from);

            GUI.DrawTexture(
                new Rect(
                    from.x,
                    from.y -
                    thickness * .5f,
                    delta.magnitude,
                    thickness),
                Texture2D.whiteTexture);

            GUI.matrix =
                oldMatrix;
        }

        private async void LoadSymbolIcons(InventoryController controller)
        {
            if (_iconsLoading || _iconsLoaded || controller == null)
                return;

            _iconsLoading = true;

            try
            {
                EFT.ItemFactory factory =
                    Singleton<EFT.ItemFactory>.Instance;

                if (factory == null)
                {
                    Plugin.Log?.LogWarning(
                        "ItemFactory unavailable; reel icons will use text fallback.");
                    return;
                }

                foreach (KeyValuePair<string, string> pair in _symbolTemplateIds)
                {
                    try
                    {
                        Item item =
                            factory.CreateItem(
                                ((IDatabaseIdGenerator)controller).NextId,
                                pair.Value,
                                null);

                        if (item == null)
                            continue;

                        Sprite sprite =
                            await ItemViewFactory.GetItemSpriteAsync(item, 1);

                        if (sprite != null)
                        {
                            _symbolSprites[pair.Key] = sprite;
                            Plugin.Log?.LogInfo(
                                $"Loaded EFT reel icon: {pair.Key}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning(
                            $"Reel icon failed for {pair.Key}: {ex.Message}");
                    }
                }

                _iconsLoaded = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"EFT reel icon preload failed: {ex}");
            }
            finally
            {
                _iconsLoading = false;
            }
        }

        private void DrawSymbolCard(Rect rect, string symbol, bool winning, bool center)
        {
            SymbolVisual visual;
            if (!_symbolVisuals.TryGetValue(symbol ?? string.Empty, out visual))
            {
                visual = new SymbolVisual("?", symbol ?? "UNKNOWN", new Color(.35f, .35f, .35f));
            }

            Color old = GUI.color;
            Color tint = winning
                ? new Color(.95f, .75f, .20f, 1f)
                : visual.Tint;

            GUI.color = new Color(tint.r, tint.g, tint.b, center ? .95f : .72f);
            GUI.DrawTexture(rect, winning ? _winningTexture : _symbolBadgeTexture, ScaleMode.StretchToFill, true);
            GUI.color = old;

            Sprite sprite = null;
            bool hasSprite =
                symbol != null &&
                _symbolSprites.TryGetValue(symbol, out sprite) &&
                sprite != null &&
                sprite.texture != null;

            if (hasSprite)
            {
                Rect iconRect =
                    new Rect(
                        rect.x + 5f,
                        rect.y + 5f,
                        68f,
                        rect.height - 10f);

                Rect source = sprite.textureRect;

                Rect texCoords =
                    new Rect(
                        source.x / sprite.texture.width,
                        source.y / sprite.texture.height,
                        source.width / sprite.texture.width,
                        source.height / sprite.texture.height);

                GUI.DrawTextureWithTexCoords(
                    iconRect,
                    sprite.texture,
                    texCoords,
                    alphaBlend: true);
            }
            else
            {
                GUI.Label(
                    new Rect(
                        rect.x + 8f,
                        rect.y + 8f,
                        58f,
                        rect.height - 16f),
                    visual.Code,
                    _symbolCodeStyle);
            }

            GUI.Label(
                new Rect(
                    rect.x + 75f,
                    rect.y + 8f,
                    rect.width - 83f,
                    rect.height - 16f),
                visual.Name,
                _symbolNameStyle);
        }

        private void DrawSlotPaytable(
            float width)
        {
            const float panelWidth = 820f;
            const float panelHeight = 430f;
            const float iconSize = 44f;
            const float rowHeight = 62f;
            const float columnGap = 18f;

            float x =
                (width - panelWidth) * .5f;

            float y = 205f;

            // Solid backing is intentional: the previous transparent panel let
            // reel art/text show through and made the paytable difficult to read.
            Color oldColor = GUI.color;
            GUI.color = new Color(.025f, .018f, .015f, .985f);
            GUI.DrawTexture(
                new Rect(
                    x,
                    y,
                    panelWidth,
                    panelHeight),
                Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Box(
                new Rect(
                    x,
                    y,
                    panelWidth,
                    panelHeight),
                GUIContent.none,
                _blackjackCardStyle);

            GUI.Label(
                new Rect(
                    x + 24f,
                    y + 16f,
                    panelWidth - 48f,
                    30f),
                "SLOT PAYTABLE",
                _titleStyle);

            GUI.Label(
                new Rect(
                    x + 24f,
                    y + 47f,
                    panelWidth - 48f,
                    24f),
                "PAYOUT MULTIPLIERS ARE APPLIED TO YOUR SELECTED BET",
                _subtitleStyle);

            string[] symbols =
            {
                "GP", "DOGTAG", "PROKILL", "ROUBLES", "GOLDSTAR",
                "LABS", "LEDX", "BTC", "RR", "JACKPOT"
            };

            int[] baseMultipliers =
            {
                2, 2, 3, 4, 5, 7, 10, 15, 25, 45
            };

            const int rowsPerColumn = 5;
            float columnWidth =
                (panelWidth - 48f - columnGap) * .5f;

            for (int i = 0;
                 i < symbols.Length;
                 i++)
            {
                int column =
                    i / rowsPerColumn;

                int row =
                    i % rowsPerColumn;

                float columnX =
                    x +
                    24f +
                    column *
                    (columnWidth + columnGap);

                float rowY =
                    y +
                    82f +
                    row *
                    rowHeight;

                string symbol =
                    symbols[i];

                if (_symbolSprites.TryGetValue(
                        symbol,
                        out Sprite sprite) &&
                    sprite != null &&
                    sprite.texture != null)
                {
                    Rect source =
                        sprite.textureRect;

                    Rect texCoords =
                        new Rect(
                            source.x / sprite.texture.width,
                            source.y / sprite.texture.height,
                            source.width / sprite.texture.width,
                            source.height / sprite.texture.height);

                    float aspect =
                        source.height > 0f
                            ? source.width / source.height
                            : 1f;

                    GUI.DrawTextureWithTexCoords(
                        FitRectPreserveAspect(
                            new Rect(
                                columnX,
                                rowY + 5f,
                                iconSize,
                                iconSize),
                            aspect),
                        sprite.texture,
                        texCoords,
                        alphaBlend: true);
                }

                string name =
                    _symbolVisuals.TryGetValue(
                        symbol,
                        out SymbolVisual visual)
                        ? visual.Name
                        : symbol;

                int three =
                    baseMultipliers[i];

                int four =
                    three * 3;

                int five =
                    three * 10;

                GUI.Label(
                    new Rect(
                        columnX + 54f,
                        rowY + 3f,
                        columnWidth - 58f,
                        24f),
                    name,
                    _statusStyle);

                GUI.Label(
                    new Rect(
                        columnX + 54f,
                        rowY + 28f,
                        columnWidth - 58f,
                        24f),
                    $"3x {three}x    4x {four}x    5x {five}x",
                    _subtitleStyle);
            }

            GUI.Label(
                new Rect(
                    x + 24f,
                    y + panelHeight - 34f,
                    panelWidth - 48f,
                    24f),
                "★ 3+ GOLD SKULLS ON ONE PAYLINE WIN THE PROGRESSIVE JACKPOT",
                _balanceStyle);
        }

        private void DrawPayoutBreakdown(float width)
        {
            if (_lineWins == null ||
                _lineWins.Length == 0 ||
                _spinning ||
                _transactionPending)
            {
                return;
            }

            float panelWidth = 420f;
            float x = width - panelWidth - 36f;
            float y = 544f;

            GUI.Label(
                new Rect(
                    x,
                    y,
                    panelWidth,
                    28f),
                _jackpot
                    ? "★ JACKPOT ★"
                    : "PAYOUT BREAKDOWN",
                _jackpot
                    ? _winStyle
                    : _statusStyle);

            y += 30f;

            int maxLines =
                Mathf.Min(
                    _lineWins.Length,
                    5);

            for (int i = 0;
                 i < maxLines;
                 i++)
            {
                SlotLineWin line =
                    _lineWins[i];

                string symbol =
                    string.IsNullOrEmpty(line.Symbol)
                        ? "?"
                        : line.Symbol;

                GUI.Label(
                    new Rect(
                        x,
                        y,
                        panelWidth,
                        22f),
                    line.Jackpot
                        ? $"LINE {line.Payline + 1}   ★ GOLD SKULL x{line.Matches} JACKPOT   +{line.Win} CHIPS"
                        : $"LINE {line.Payline + 1}   {symbol} x{line.Matches}   +{line.Win} CHIPS",
                    _subtitleStyle);

                y += 22f;
            }

            GUI.Label(
                new Rect(
                    x,
                    y + 6f,
                    panelWidth,
                    30f),
                $"TOTAL: +{_displayedWinAmount} CHIPS",
                _balanceStyle);
        }

        private void DrawControls(float width)
        {
            float centerX =
                width * .5f;

            GUI.Label(
                new Rect(
                    centerX - 140f,
                    554f,
                    280f,
                    42f),
                $"BET: {_bet} CHIPS",
                _balanceStyle);

            InventoryController controller =
                null;

            int roubles =
                0;

            if (CurrencyService
                .TryGetActiveCharacterInventoryController(
                    out controller))
            {
                roubles =
                    CurrencyService.GetBalance(
                        controller,
                        CurrencyService.Roubles);
            }

            bool controlsReady =
                !_spinning &&
                !_transactionPending;

            bool insufficientGp =
                _balance <
                _bet;

            bool canBuyIn =
                insufficientGp &&
                _balance < 5;

            GUI.enabled =
                controlsReady;

            if (GUI.Button(
                new Rect(
                    centerX - 245f,
                    612f,
                    78f,
                    50f),
                "-",
                _smallButtonStyle))
            {
                ChangeBet(-1);
            }

            if (GUI.Button(
                new Rect(
                    centerX + 167f,
                    612f,
                    78f,
                    50f),
                "+",
                _smallButtonStyle))
            {
                ChangeBet(1);
            }

            GUI.enabled =
                controlsReady &&
                (!insufficientGp ||
                 (canBuyIn &&
                  roubles >= _buyInCostRoubles));

            string mainLabel =
                _transactionPending
                    ? "SERVER..."
                    : _spinning
                        ? "SPINNING..."
                        : insufficientGp
                            ? (canBuyIn
                                ? $"BUY 5 CHIPS\n₽{_buyInCostRoubles:N0}"
                                : "NOT ENOUGH CHIPS")
                            : "SPIN";

            if (GUI.Button(
                new Rect(
                    centerX - 140f,
                    598f,
                    280f,
                    78f),
                mainLabel,
                _buttonStyle))
            {
                if (canBuyIn)
                    BeginBuyIn();
                else if (!insufficientGp)
                    BeginServerSpin();
            }

            GUI.enabled =
                true;

            if (insufficientGp &&
                !_transactionPending)
            {
                GUI.Label(
                    new Rect(
                        centerX - 300f,
                        680f,
                        600f,
                        28f),
                    !canBuyIn
                        ? "BUY-IN LOCKED ONCE YOU HAVE 5+ CHIPS"
                        : (roubles >= _buyInCostRoubles
                            ? "NOT ENOUGH CHIPS // BUY-IN ADDS 5 GP"
                            : $"NEED ₽{_buyInCostRoubles:N0} FOR 5 CHIP BUY-IN"),
                    _subtitleStyle);
            }
            else if (_lastWin > 0 &&
                     !_spinning &&
                     !_transactionPending)
            {
                GUI.Label(
                    new Rect(
                        centerX - 270f,
                        687f,
                        540f,
                        64f),
                    $"WIN: +{_lastWin} CHIPS",
                    _winStyle);
            }
        }

        private void ChangeBet(int direction)
        {
            int index = 0;

            for (int i = 0;
                 i < _bets.Length;
                 i++)
            {
                if (_bets[i] == _bet)
                {
                    index = i;
                    break;
                }
            }

            _bet =
                _bets[
                    Mathf.Clamp(
                        index + direction,
                        0,
                        _bets.Length - 1)];
        }

        private void BeginBuyIn()
        {
            if (_spinning ||
                _transactionPending)
            {
                return;
            }

            InventoryController controller;

            if (!CurrencyService
                .TryGetActiveCharacterInventoryController(
                    out controller))
            {
                _status =
                    "CHARACTER SCREEN NOT ACTIVE";

                return;
            }

            string profileId =
                CurrencyService.GetProfileId(
                    controller);

            if (string.IsNullOrEmpty(
                    profileId))
            {
                _status =
                    "COULD NOT RESOLVE PROFILE ID";

                return;
            }

            int chips =
                CurrencyService.GetBalance(
                    controller,
                    CurrencyService.Gp);

            if (chips >= 5)
            {
                _status =
                    "BUY-IN LOCKED AT 5+ CHIPS";

                return;
            }

            int roubles =
                CurrencyService.GetBalance(
                    controller,
                    CurrencyService.Roubles);

            if (roubles <
                _buyInCostRoubles)
            {
                _status =
                    $"NEED ₽{_buyInCostRoubles:N0} TO BUY 5 CHIPS";

                return;
            }

            _transactionPending =
                true;

            _status =
                "BUYING 5 CHIPS...";

            PlayNativeUISound(
                EUISoundType.ButtonClick);

            StartCoroutine(
                CasinoItemEventClient.SendBuyIn(
                    controller,
                    CurrencyService.GetStackMax(
                        controller,
                        CurrencyService.Gp),
                    CurrencyService.GetStackMax(
                        controller,
                        CurrencyService.Roubles),
                    (requestId, eventResult) =>
                    {
                        if (eventResult == null ||
                            !eventResult.Success)
                        {
                            _transactionPending =
                                false;

                            _status =
                                eventResult?.Error
                                ?? "BUY-IN INVENTORY TRANSACTION FAILED";

                            RefreshBalance(
                                controller);

                            return;
                        }

                        // SendOperationRightNow invokes its callback only after
                        // ClientBackendSession.SendCallback has applied the
                        // normal SPT profileChanges to EFT's live profile.
                        StartCoroutine(
                            FinishBuyInResult(
                                controller,
                                profileId,
                                requestId));
                    }));
        }

        private IEnumerator FinishBuyInResult(
            InventoryController controller,
            string profileId,
            string requestId)
        {
            CasinoBuyInResponse response =
                null;

            yield return
                SlotServerClient.GetBuyInResult(
                    profileId,
                    requestId,
                    result =>
                    {
                        response =
                            result;
                    });

            _transactionPending =
                false;

            RefreshBalance(
                controller);

            if (response == null ||
                !response.Success)
            {
                _status =
                    response?.Message
                    ?? "BUY-IN RESULT UNAVAILABLE";

                yield break;
            }

            int liveRoubles =
                CurrencyService.GetBalance(
                    controller,
                    CurrencyService.Roubles);

            if (_balance !=
                    response.GpBalance ||
                liveRoubles !=
                    response.RoubleBalance)
            {
                Plugin.Log?.LogWarning(
                    $"Native buy-in completed but balance verification differed. " +
                    $"Chips={_balance}/{response.GpBalance}, " +
                    $"RUB={liveRoubles}/{response.RoubleBalance}");
            }

            _status =
                response.Message
                ?? $"BOUGHT 5 CHIPS FOR ₽{_buyInCostRoubles:N0}";

            PlayNativeUISound(
                EUISoundType.TradeOperationComplete);
        }

        private void BeginServerSpin()
        {
            if (_spinning ||
                _transactionPending)
            {
                return;
            }

            InventoryController controller;

            if (!CurrencyService
                .TryGetActiveCharacterInventoryController(
                    out controller))
            {
                _status =
                    "CHARACTER SCREEN NOT ACTIVE";

                return;
            }

            string profileId =
                CurrencyService.GetProfileId(
                    controller);

            if (string.IsNullOrEmpty(
                    profileId))
            {
                _status =
                    "COULD NOT RESOLVE PROFILE ID";

                return;
            }

            int balance =
                CurrencyService.GetBalance(
                    controller,
                    CurrencyService.Gp);

            if (balance <
                _bet)
            {
                _status =
                    "NOT ENOUGH CHIPS";

                return;
            }

            _transactionPending =
                true;

            PlayNativeUISound(
                EUISoundType.ButtonClick);

            _lastWin = 0;
            _winningPayline = -1;
            _pendingWin = 0;
            _pendingWinningPayline = -1;
            _pendingFinalBalance = balance;
            _pendingWinningCells = null;
            _pendingLineWins = null;
            _lineWins = null;
            _pendingJackpot = false;
            _jackpot = false;
            _lastJackpotPayout = 0;
            _displayedWinAmount = 0;
            ClearWinningCells();

            _displayedBalance =
                Math.Max(
                    0,
                    balance - _bet);

            _status =
                "PROCESSING CASINO TRANSACTION...";

            StartCoroutine(
                CasinoItemEventClient.SendSpin(
                    controller,
                    _bet,
                    Plugin.JackpotEnabled == null ||
                    Plugin.JackpotEnabled.Value,
                    CurrencyService.GetStackMax(
                        controller,
                        CurrencyService.Gp),
                    (requestId, eventResult) =>
                    {
                        if (eventResult == null ||
                            !eventResult.Success)
                        {
                            _transactionPending =
                                false;

                            RefreshBalance(
                                controller);

                            _status =
                                eventResult?.Error
                                ?? "CASINO INVENTORY TRANSACTION FAILED";

                            return;
                        }

                        // At this point EFT has already consumed the SPT
                        // profileChanges generated by /items/moving.
                        StartCoroutine(
                            FinishSpinResult(
                                controller,
                                profileId,
                                requestId));
                    }));
        }

        private IEnumerator FinishSpinResult(
            InventoryController controller,
            string profileId,
            string requestId)
        {
            SlotSpinResponse serverResponse =
                null;

            yield return
                SlotServerClient.GetSlotSpinResult(
                    profileId,
                    requestId,
                    response =>
                    {
                        serverResponse =
                            response;
                    });

            _transactionPending =
                false;

            RefreshBalance(
                controller);

            if (serverResponse == null ||
                !serverResponse.Success)
            {
                _status =
                    serverResponse?.Message
                    ?? "SPIN RESULT UNAVAILABLE";

                yield break;
            }

            if (_balance !=
                serverResponse.Balance)
            {
                Plugin.Log?.LogWarning(
                    $"Native casino item-event completed but Casino Chip " +
                    $"balance differs. Live={_balance}, Server={serverResponse.Balance}");
            }

            if (serverResponse.Symbols == null ||
                serverResponse.Symbols.Length != ReelCount)
            {
                _status =
                    "SERVER RETURNED INVALID REELS";

                yield break;
            }

            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                if (serverResponse.Symbols[reel] == null ||
                    serverResponse.Symbols[reel].Length != RowsPerReel)
                {
                    _status =
                        "SERVER RETURNED INVALID REEL ROWS";

                    yield break;
                }

                for (int row = 0;
                     row < RowsPerReel;
                     row++)
                {
                    _finalSymbols[reel, row] =
                        serverResponse.Symbols[reel][row];
                }
            }

            _pendingWin =
                serverResponse.Win;

            _pendingWinningPayline =
                serverResponse.WinningPayline;

            _pendingFinalBalance =
                serverResponse.Balance;

            _pendingWinningCells =
                serverResponse.WinningCells;

            _pendingLineWins =
                serverResponse.LineWins;

            _pendingJackpot =
                serverResponse.Jackpot;

            _jackpotAmount =
                Math.Max(
                    0,
                    serverResponse.JackpotAmount);

            _lastJackpotPayout =
                Math.Max(
                    0,
                    serverResponse.JackpotPayout);

            _statsDirty =
                true;

            StartCoroutine(
                SpinRoutine(
                    controller));
        }


        private IEnumerator SpinRoutine(
            InventoryController controller)
        {
            _spinning = true;
            _status = "SPINNING...";

            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                _reelSpinning[reel] = true;
                _reelSpeeds[reel] =
                    19.5f +
                    reel * 1.35f;
                _reelOffsets[reel] = 0f;
                _reelSettleOffsets[reel] = 0f;

                // Slight startup staggering makes the reels feel physically
                // linked instead of all beginning on the same frame.
                yield return
                    new WaitForSecondsRealtime(
                        .055f);
            }

            yield return
                new WaitForSecondsRealtime(
                    .72f);

            bool jackpotAnticipation =
                _pendingLineWins != null &&
                Array.Exists(
                    _pendingLineWins,
                    line =>
                        line != null &&
                        string.Equals(
                            line.Symbol,
                            "JACKPOT",
                            StringComparison.OrdinalIgnoreCase) &&
                        line.Matches >= 3);

            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                if (jackpotAnticipation &&
                    reel == 2)
                {
                    _status =
                        "★ JACKPOT CHANCE ★";

                    for (int remaining = reel;
                         remaining < ReelCount;
                         remaining++)
                    {
                        _reelSpeeds[remaining] =
                            Mathf.Max(
                                6.8f,
                                _reelSpeeds[remaining] *
                                .52f);
                    }

                    PlayNativeUISound(
                        EUISoundType.ButtonClick);

                    yield return
                        new WaitForSecondsRealtime(
                            .82f);
                }

                float elapsed = 0f;
                float startSpeed =
                    _reelSpeeds[reel];

                // Later reels take progressively longer to coast down.
                float stopDuration =
                    .40f +
                    reel * .085f;

                while (elapsed <
                       stopDuration)
                {
                    elapsed +=
                        Time.unscaledDeltaTime;

                    float t =
                        Mathf.Clamp01(
                            elapsed /
                            stopDuration);

                    // Smooth deceleration with a little more "weight" at the
                    // beginning than the previous cubic snap-down.
                    float eased =
                        1f -
                        Mathf.Pow(
                            1f - t,
                            2.35f);

                    _reelSpeeds[reel] =
                        Mathf.Lerp(
                            startSpeed,
                            .82f,
                            eased);

                    yield return null;
                }

                // Stop the free-running reel updater here. From this point
                // forward we manually feed the server-selected symbols into the
                // reel one at a time. This makes the final icons physically
                // scroll into view instead of replacing all three rows on the
                // last frame.
                _reelSpinning[reel] =
                    false;

                _reelSpeeds[reel] =
                    0f;

                for (int landingRow = 0;
                     landingRow < RowsPerReel;
                     landingRow++)
                {
                    _incomingSymbols[reel] =
                        _finalSymbols[reel, landingRow];

                    float landingStart =
                        _reelOffsets[reel];

                    float landingElapsed =
                        0f;

                    float landingDuration =
                        .12f +
                        landingRow * .025f;

                    while (landingElapsed <
                           landingDuration)
                    {
                        landingElapsed +=
                            Time.unscaledDeltaTime;

                        float t =
                            Mathf.Clamp01(
                                landingElapsed /
                                landingDuration);

                        // Smoothstep keeps each incoming icon moving at the
                        // same visual speed as the reel rather than popping.
                        float smooth =
                            t * t *
                            (3f - 2f * t);

                        _reelOffsets[reel] =
                            Mathf.Lerp(
                                landingStart,
                                1f,
                                smooth);

                        yield return null;
                    }

                    // Advance the physical reel exactly one symbol.
                    _displaySymbols[reel, 0] =
                        _displaySymbols[reel, 1];

                    _displaySymbols[reel, 1] =
                        _displaySymbols[reel, 2];

                    _displaySymbols[reel, 2] =
                        _incomingSymbols[reel];

                    _reelOffsets[reel] =
                        0f;
                }

                // Resume the persistent visual strip from a position whose
                // next symbol follows the landed result. The next spin therefore
                // continues naturally instead of starting from random symbols.
                _visualReelStripIndices[reel] =
                    FindBestStripResumeIndex(
                        reel,
                        _displaySymbols[reel, RowsPerReel - 1]);

                _incomingSymbols[reel] =
                    GetStripSymbol(
                        reel,
                        _visualReelStripIndices[reel] +
                        RowsPerReel);

                PlayNativeUISound(
                    EUISoundType.MenuInstallMag);

                // Tiny overshoot/bounce after each reel locks in.
                float bounceElapsed =
                    0f;

                const float bounceDuration =
                    .16f;

                while (bounceElapsed <
                       bounceDuration)
                {
                    bounceElapsed +=
                        Time.unscaledDeltaTime;

                    float t =
                        Mathf.Clamp01(
                            bounceElapsed /
                            bounceDuration);

                    // Decaying single bounce: down, then back to neutral.
                    _reelSettleOffsets[reel] =
                        Mathf.Sin(
                            t *
                            Mathf.PI) *
                        .055f *
                        (1f - t);

                    yield return null;
                }

                _reelSettleOffsets[reel] =
                    0f;

                // More separation between reel stops makes each reveal readable.
                yield return
                    new WaitForSecondsRealtime(
                        reel == ReelCount - 1
                            ? .12f
                            : .18f +
                              reel * .025f);
            }

            // Reveal win state only after every reel is fully settled.
            _lastWin =
                _pendingWin;

            _winningPayline =
                _pendingWinningPayline;

            ClearWinningCells();

            if (_pendingWinningCells != null)
            {
                foreach (SlotCell cell in
                         _pendingWinningCells)
                {
                    if (cell.Reel >= 0 &&
                        cell.Reel < ReelCount &&
                        cell.Row >= 0 &&
                        cell.Row < RowsPerReel)
                    {
                        _winningCells[
                            cell.Reel,
                            cell.Row] =
                            true;
                    }
                }
            }

            _lineWins =
                _pendingLineWins;

            _jackpot =
                _pendingJackpot;

            int startBalance =
                _displayedBalance;

            int finalBalance =
                _pendingFinalBalance;

            _balance =
                finalBalance;

            _spinning =
                false;

            _winTierLabel =
                GetWinTierLabel(
                    _lastWin,
                    _bet,
                    _jackpot);

            _winPresentationStartedAt =
                Time.unscaledTime;

            if (_jackpot)
            {
                _jackpotCelebrationStartedAt =
                    Time.unscaledTime;
            }

            _activeWinningLineIndex =
                _lastWin > 0
                    ? 0
                    : -1;

            _status =
                _lastWin > 0
                    ? $"{_winTierLabel}  +{_lastWin} CHIPS"
                    : "NO WIN";

            if (_lastWin > 0)
            {
                PlayNativeUISound(
                    _jackpot
                        ? EUISoundType.QuestCompleted
                        : EUISoundType.TradeOperationComplete);
            }

            float balanceElapsed =
                0f;

            float balanceDuration =
                _jackpot
                    ? 2.40f
                    : _lastWin >= _bet * 50
                        ? 1.80f
                        : _lastWin >= _bet * 15
                            ? 1.35f
                            : _lastWin >= _bet * 5
                                ? 1.00f
                                : .72f;

            while (balanceElapsed <
                   balanceDuration)
            {
                balanceElapsed +=
                    Time.unscaledDeltaTime;

                float t =
                    Mathf.Clamp01(
                        balanceElapsed /
                        balanceDuration);

                _displayedBalance =
                    Mathf.RoundToInt(
                        Mathf.Lerp(
                            startBalance,
                            finalBalance,
                            t));

                _displayedWinAmount =
                    Mathf.RoundToInt(
                        Mathf.Lerp(
                            0f,
                            _lastWin,
                            t));

                yield return null;
            }

            _displayedBalance =
                finalBalance;

            _displayedWinAmount =
                _lastWin;

            RefreshBalance(
                controller);
        }


        private static string GetWinTierLabel(
            int win,
            int bet,
            bool jackpot)
        {
            if (jackpot)
            {
                return "★ JACKPOT ★";
            }

            int safeBet =
                Mathf.Max(
                    1,
                    bet);

            float multiple =
                win /
                (float)safeBet;

            if (multiple >= 50f)
            {
                return "MASSIVE WIN";
            }

            if (multiple >= 15f)
            {
                return "MEGA WIN";
            }

            if (multiple >= 5f)
            {
                return "BIG WIN";
            }

            return "WIN";
        }

        private void ClearWinningCells()
        {
            for (int reel = 0;
                 reel < ReelCount;
                 reel++)
            {
                for (int row = 0;
                     row < RowsPerReel;
                     row++)
                {
                    _winningCells[reel, row] = false;
                }
            }
        }

        private int FindBestStripResumeIndex(
            int reel,
            string lastVisibleSymbol)
        {
            string[] strip =
                _visualReelStrips[reel];

            if (strip == null ||
                strip.Length == 0)
            {
                return 0;
            }

            // Find a strip position whose visible bottom matches the landed
            // bottom symbol. If multiple positions exist, choose one randomly
            // so successive spins do not always resume from the same place.
            List<int> candidates =
                new List<int>();

            for (int index = 0;
                 index < strip.Length;
                 index++)
            {
                string bottom =
                    GetStripSymbol(
                        reel,
                        index +
                        RowsPerReel -
                        1);

                if (string.Equals(
                        bottom,
                        lastVisibleSymbol,
                        StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(
                        index);
                }
            }

            if (candidates.Count == 0)
            {
                return
                    UnityEngine.Random.Range(
                        0,
                        strip.Length);
            }

            return candidates[
                UnityEngine.Random.Range(
                    0,
                    candidates.Count)];
        }

        private string GetRandomDisplaySymbol()
        {
            int reel =
                UnityEngine.Random.Range(
                    0,
                    ReelCount);

            string[] strip =
                _visualReelStrips[reel];

            return strip[
                UnityEngine.Random.Range(
                    0,
                    strip.Length)];
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            _panelTexture =
                Tex(
                    new Color(
                        .055f,
                        .018f,
                        .022f,
                        .99f));

            _feltTexture = CreateVelvetFeltTexture();
            _mahoganyTexture = CreateMahoganyTexture();

            _symbolTexture =
                Tex(
                    new Color(
                        .09f,
                        .095f,
                        .085f,
                        1f));

            _centerTexture =
                Tex(
                    new Color(
                        .13f,
                        .125f,
                        .095f,
                        1f));

            _winningTexture =
                Tex(new Color(.20f, .16f, .06f, 1f));

            _symbolBadgeTexture =
                Tex(Color.white);

            _dividerTexture =
                Tex(new Color(.04f, .045f, .04f, 1f));

            _titleStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 34,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _titleStyle.normal.textColor =
                new Color(.96f, .78f, .28f);

            _subtitleStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                };

            _subtitleStyle.normal.textColor =
                new Color(.5f, .52f, .48f);

            _balanceStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 21,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _balanceStyle.normal.textColor =
                new Color(.78f, .78f, .70f);

            _symbolStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _symbolStyle.normal.textColor =
                new Color(.72f, .72f, .66f);

            _centerSymbolStyle =
                new GUIStyle(_symbolStyle)
                {
                    fontSize = 21
                };

            _centerSymbolStyle.normal.textColor =
                new Color(.95f, .87f, .62f);

            _winningSymbolStyle =
                new GUIStyle(_centerSymbolStyle)
                {
                    fontSize = 22
                };

            _winningSymbolStyle.normal.textColor =
                new Color(1f, .86f, .38f);

            _symbolCodeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _symbolCodeStyle.normal.textColor = new Color(.96f, .94f, .84f);

            _symbolNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _symbolNameStyle.normal.textColor = new Color(.92f, .91f, .82f);

            _buttonStyle =
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold
                };

            _smallButtonStyle =
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 19,
                    fontStyle = FontStyle.Bold
                };

            _blackjackButtonStyle =
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };

            _winStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 31,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _winStyle.normal.textColor =
                new Color(1f, .78f, .22f);

            _statusStyle =
                new GUIStyle(_subtitleStyle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };

            _statusStyle.normal.textColor =
                new Color(.92f, .66f, .22f);

            _blackjackStatusStyle =
                new GUIStyle(_statusStyle)
                {
                    fontSize = 19,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _blackjackCardStyle =
                new GUIStyle(_balanceStyle)
                {
                    fontSize = 27,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _blackjackCardCompactStyle =
                new GUIStyle(_subtitleStyle)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _blackjackPlayerStyle =
                new GUIStyle(_subtitleStyle)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };

            _blackjackPlayerTurnStyle =
                new GUIStyle(_blackjackPlayerStyle)
                {
                    fontSize = 15
                };
            _blackjackPlayerTurnStyle.normal.textColor =
                new Color(1f, .78f, .22f);

            _blackjackResultStyle =
                new GUIStyle(_winStyle)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };

            _blackjackHandWinStyle =
                new GUIStyle(_blackjackPlayerStyle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _blackjackHandWinStyle.normal.textColor =
                new Color(.45f, 1f, .48f);

            _blackjackHandLoseStyle =
                new GUIStyle(_blackjackPlayerStyle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _blackjackHandLoseStyle.normal.textColor =
                new Color(1f, .42f, .38f);

            _blackjackHandPushStyle =
                new GUIStyle(_blackjackPlayerStyle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            _blackjackHandPushStyle.normal.textColor =
                new Color(.82f, .82f, .78f);

            _stylesInitialized = true;
        }

        private Texture2D CreateVelvetFeltTexture()
        {
            const int size=128;
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            System.Random random=new System.Random(7719);
            Color baseColor=new Color(.072f,.018f,.026f,1f);

            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float grain=((float)random.NextDouble()-.5f)*.025f;
                float weave=Mathf.Sin((x+y)*.55f)*.006f;
                float fiber=((x*17+y*31)%23==0)?.018f:0f;
                float v=grain+weave+fiber;

                texture.SetPixel(
                    x,y,
                    new Color(
                        Mathf.Clamp01(baseColor.r+v),
                        Mathf.Clamp01(baseColor.g+v*.35f),
                        Mathf.Clamp01(baseColor.b+v*.45f),
                        1f));
            }

            texture.wrapMode=TextureWrapMode.Repeat;
            texture.filterMode=FilterMode.Bilinear;
            texture.Apply();
            return texture;
        }

        private Texture2D CreateMahoganyTexture()
        {
            const int width=256,height=96;
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            System.Random random=new System.Random(19341);

            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float wave=Mathf.Sin(x*.11f+Mathf.Sin(y*.18f)*2.2f)*.045f;
                float fine=Mathf.Sin(x*.43f+y*.09f)*.018f;
                float noise=((float)random.NextDouble()-.5f)*.025f;
                float g=wave+fine+noise;

                texture.SetPixel(
                    x,y,
                    new Color(
                        Mathf.Clamp01(.22f+g),
                        Mathf.Clamp01(.075f+g*.42f),
                        Mathf.Clamp01(.035f+g*.24f),
                        1f));
            }

            texture.wrapMode=TextureWrapMode.Repeat;
            texture.filterMode=FilterMode.Bilinear;
            texture.Apply();
            return texture;
        }

        private Texture2D Tex(Color color)
        {
            Texture2D texture =
                new Texture2D(
                    1,
                    1,
                    TextureFormat.RGBA32,
                    false);

            texture.SetPixel(
                0,
                0,
                color);

            texture.filterMode =
                FilterMode.Point;

            texture.wrapMode =
                TextureWrapMode.Clamp;

            texture.Apply();

            return texture;
        }
        private void ResolveGuiSounds()
        {
            if (_guiSounds != null)
            {
                return;
            }

            try
            {
                GUISounds[] candidates =
                    Resources.FindObjectsOfTypeAll<GUISounds>();

                foreach (GUISounds candidate in candidates)
                {
                    if (candidate == null)
                    {
                        continue;
                    }

                    _guiSounds = candidate;
                    return;
                }

                Plugin.Log?.LogWarning(
                    "EFT GUISounds instance was not found.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Failed resolving EFT GUISounds: {ex}");
            }
        }

        private void PlayNativeUISound(EUISoundType soundType)
        {
            try
            {
                if (_guiSounds == null)
                {
                    ResolveGuiSounds();
                }

                if (_guiSounds == null)
                {
                    Plugin.Log?.LogWarning(
                        $"Cannot play native UI sound {soundType}: GUISounds unavailable.");
                    return;
                }

                _guiSounds.PlayUISound(soundType);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Native UI sound {soundType} failed: {ex}");
            }
        }

        private sealed class SymbolVisual
        {
            public string Code { get; }
            public string Name { get; }
            public Color Tint { get; }

            public SymbolVisual(string code, string name, Color tint)
            {
                Code = code;
                Name = name;
                Tint = tint;
            }
        }

    }
}
