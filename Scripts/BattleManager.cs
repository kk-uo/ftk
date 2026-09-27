//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Core System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager : Control
{
    [Signal]
    public delegate void BattleFinishedEventHandler(bool playerWon);

    [Signal]
    public delegate void ReturnToMainMenuRequestedEventHandler();

    [Signal]
    public delegate void SuspendRunRequestedEventHandler();

    [Signal]
    public delegate void GoToMapRequestedEventHandler();

    private const int HpFontSize = 20;
    private const int ManaFontSize = 18;
    private const double StackWindow = 0.5;
    private const double GuanxingWindow = 3.0;
    private const double ZhugeCrossbowWindow = 2.0;
    private readonly Player _player = new("玩家");
    private readonly EnemyAI _enemyAi = new();
    private readonly TriggerManager _triggerManager = BattleTriggerEffects.CreateDefaultManager();
    private readonly BattleEncounter _encounter = new();
    private readonly System.Random _dropRandom = new();
    private readonly ActionSlot?[] _actionSlots = new ActionSlot[BattleHotkeySystem.ActionSlotCount];
    private readonly SelectionPresenter _selectionPresenter = new();

    private BattleContext? _context;
    private Label? _turnLabel;
    private Label? _actionLabel;
    private Label? _playerActionDisplayLabel;
    private Label? _turnCounterLabel;
    private RichTextLabel? _recentReportLabel;
    private Control? _battlePresentationSource;
    private Control? _battleStageVisualLayer;
    private Control? _battlePresentationArea;
    private LowHealthScreenBorder? _lowHealthScreenBorder;
    private CanvasLayer? _damageVignetteLayer;
    private PlayerDamageBorder? _playerDamageBorder;
    private DodgeShieldScreenBorder? _dodgeShieldScreenBorder;
    private SkillTriggerToastQueue? _skillTriggerToastQueue;
    private BossStatusUI? _bossStatusUi;
    private PanelContainer? _selectionConfirmPanel;
    private Label? _selectionConfirmLabel;
    private ProgressBar? _selectionProgressBar;
    private HBoxContainer? _actionArea;
    private int _nextActionSlotIndex;
    private ChoicePanel? _cardOverflowChoicePanel;
    private HBoxContainer? _friendlyContainer;
    private BoxContainer? _enemyContainer;
    private Control? _enemyStatusPanel;
    private Button? _restartButton;
    private Button? _returnToMainMenuButton;
    private Button? _debugButton;
    private Button? _skillDebugButton;
    private Button? _battleLogButton;
    private Button? _returnToMapButton;
    private Window? _skillDebugWindow;
    private Window? _battleLogWindow;
    private Window? _battleDebugWindow;
    private VBoxContainer? _skillDebugOwnedList;
    private RichTextLabel? _battleDebugAiViewer;
    private RichTextLabel? _battleDebugHotkeyViewer;
    private Label? _battleDebugShadowViewer;
    private CheckBox? _battleDebugAiDecisionCheckBox;
    private CheckBox? _battleDebugInvincibleCheckBox;
    private LineEdit? _battleDebugEnemySearchInput;
    private OptionButton? _battleDebugEnemyFilterPicker;
    private SpinBox? _battleDebugHpSpin;
    private SpinBox? _battleDebugMaxHpSpin;
    private SpinBox? _battleDebugManaSpin;
    private SpinBox? _battleDebugForageSpin;
    private SpinBox? _battleDebugGoldSpin;
    private readonly OptionButton?[] _battleDebugEnemyPickers = new OptionButton?[3];
    private readonly OptionButton?[] _battleDebugEnemyActionPickers = new OptionButton?[3];
    private LineEdit? _battleLogSearchInput;
    private HFlowContainer? _battleLogFilterRow;
    private VBoxContainer? _battleLogList;
    private ScrollContainer? _battleLogScroll;
    private Tree? _battleLogTree;
    private BattleLogFilter _battleLogFilter = BattleLogFilter.All;
    private BattleLogScope _battleLogScope = BattleLogScope.CurrentBattle;
    private DeveloperLogFilter _developerLogFilter = DeveloperLogFilter.All;
    private bool _battleLogDeveloperView;
    private int? _battleLogSelectedRunId;
    private int? _battleLogSelectedBattleId;
    private int _battleLogSelectedRound;
    private readonly BattleLogManager _battleLogManager = new();
    private readonly Dictionary<CardUI, ReactionOption> _reactionOptionsByCard = new();
    private readonly List<CharacterStatusCard> _friendlyCards = new();
    private readonly List<CharacterStatusCard> _enemyCards = new();
    private readonly PackedScene _characterStatusCardScene =
        GD.Load<PackedScene>("res://Scenes/CharacterStatusCard.tscn");
    private BattleUnit? _selectedTarget;

    private bool _inputLocked;
    private bool _reactionMode;
    private bool _isShowingBattleMessage;
    private bool _battleDebugMode;
    private bool _zhugeMode;
    private Card? _stackingCard;
    private IReaction? _activeReaction;
    private TaskCompletionSource<ReactionOption?>? _reactionSelection;
    private int _stackCount;
    private int _turnNumber = 1;
    private int _battleMessageVersion;
    private int _stackVersion;
    private int _battleRunId;
    private int _lastLoggedVisibleEnemySlots = -1;
    private int _lastLoggedEncounterCount = -1;
    private BattlePhase _phase = BattlePhase.StartPhase;
    private bool _battleFinishedEmitted;
    private bool? _pendingBattleResult;
    private double _selectionRemainingSeconds;
    private double _activeTimerWindow;
    private int _guanxingTimerVersion;
    private string _debugCharacterId = CharacterIds.ZhaoYun;
    private readonly string?[] _debugEnemyIds = { "gate_guard", null, null };
    private readonly CardType?[] _debugEnemyNextActionOverrides = new CardType?[3];
    private readonly HashSet<string> _debugSkillIds = new();
    private int _debugPlayerHp = BattleConstants.InitialHealth;
    private int _debugPlayerMaxHp = BattleConstants.InitialHealth;
    private double _debugPlayerMana = BattleConstants.InitialMana;
    private bool _debugPlayerInvincible;
    private string _battleDebugEnemySearchText = string.Empty;
    private EnemyType? _battleDebugEnemyFilterType;

    private readonly struct BattleSnapshot
    {
        /// <summary>
        /// Core System 的公开入口：BattleSnapshot。
        ///
        /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
        /// </summary>
        public BattleSnapshot(int playerHealth, double playerMana, int enemyHealth, double enemyMana)
        {
            PlayerHealth = playerHealth;
            PlayerMana = playerMana;
            EnemyHealth = enemyHealth;
            EnemyMana = enemyMana;
        }

        public int PlayerHealth { get; }
        public double PlayerMana { get; }
        public int EnemyHealth { get; }
        public double EnemyMana { get; }
    }

    /// <summary>
    /// Core System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        CacheNodes();
        ConfigureStaticUi();
        StartBattle();
    }

    /// <summary>
    /// Core System 的公开入口：ConfigureDebugMode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ConfigureDebugMode(string characterId)
    {
        _battleDebugMode = true;
        _debugCharacterId = string.IsNullOrWhiteSpace(characterId) ? CharacterIds.ZhaoYun : characterId;
        var character = CharacterDatabase.GetCharacter(_debugCharacterId);
        _debugPlayerMaxHp = character.MaxHp;
        _debugPlayerHp = character.MaxHp;
        _debugPlayerMana = BattleConstants.InitialMana;
        _debugPlayerInvincible = false;
        _debugSkillIds.Clear();
        foreach (var skillId in character.SkillIds)
        {
            _debugSkillIds.Add(skillId);
        }

        // Tutorial System hook: when the new-player tutorial is running, this same
        // debug-mode battle is reused as the tutorial's real battle instance. See
        // BattleManager.Tutorial.cs for the additive tutorial-only configuration.
        if (TutorialManager.IsActive)
        {
            ApplyTutorialConfiguration();
        }
    }

    /// <summary>
    /// Core System 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        TooltipManager.HideImmediate();
        DeveloperModeManager.ModeChanged -= RefreshDevModeButtons;
        Localization.LanguageChanged -= RefreshLocalizedButtonTexts;
    }

    private void RefreshDevModeButtons()
    {
        var dev = DeveloperModeManager.IsDeveloperMode;
        if (_debugButton != null) _debugButton.Visible = dev;
        if (_battleLogButton != null) _battleLogButton.Visible = true;
        if (!dev && _battleLogFilter == BattleLogFilter.Debug)
        {
            _battleLogFilter = BattleLogFilter.All;
        }
        RebuildBattleLogView();
    }

    private void RefreshLocalizedButtonTexts()
    {
        if (_returnToMainMenuButton != null) _returnToMainMenuButton.Text = Localization.Get("battle.return_menu");
        if (_debugButton != null) _debugButton.Text = Localization.Get("battle.dev_btn");
        if (_returnToMapButton != null)
        {
            _returnToMapButton.Text = Localization.Get(
                _battleDebugMode ? "battle.return_menu" : "battle.return_map");
        }
        if (_restartButton != null) _restartButton.Text = Localization.Get("battle.restart_btn");
        if (_battleLogButton != null) _battleLogButton.Text = Localization.Get("battle.log_btn");
        if (_skillDebugButton != null) _skillDebugButton.Text = Localization.Get("battle.skill_debug_btn");
        RefreshHoverLocalization();
    }

    /// <summary>
    /// Core System 的公开入口：_Process。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Process(double delta)
    {
        RefreshTemporaryEnemyStageVisualLayout();
        UpdateBattleStageGeometryHover();

        if (_selectionProgressBar == null || _selectionConfirmPanel == null || !_selectionConfirmPanel.Visible || _activeTimerWindow <= 0)
        {
            return;
        }

        _selectionRemainingSeconds = System.Math.Max(0, _selectionRemainingSeconds - delta);
        _selectionProgressBar.Value = _selectionRemainingSeconds / _activeTimerWindow * 100.0;

        // 连弩模式：每帧实时更新剩余时间与当前累积杀数。
        if (_zhugeMode && _selectionConfirmLabel != null)
        {
            _selectionConfirmLabel.Text = Localization.GetFmt("battle.confirm.zhugemode_fmt", _stackCount, $"{_selectionRemainingSeconds:0.0}s");
        }
    }

    private void CacheNodes()
    {
        _turnLabel = GetNode<Label>("%TurnLabel");
        _actionLabel = GetNode<Label>("%ActionLabel");
        _playerActionDisplayLabel = GetNode<Label>("%PlayerActionDisplayLabel");
        _turnCounterLabel = GetNode<Label>("%TurnCounterLabel");
        _recentReportLabel = GetNode<RichTextLabel>("%RecentReportLabel");
        _battlePresentationSource = GetNode<Control>("%BattleStage");
        _skillTriggerToastQueue = GetNode<SkillTriggerToastQueue>("%SkillTriggerToastQueue");
        _battleStageVisualLayer = _battlePresentationSource.GetNodeOrNull<Control>("BattleStageLayer") ?? _battlePresentationSource;
        _actionArea = GetNode<HBoxContainer>("%ActionArea");
        EnsureActionSlotsBuilt();
        var friendlyPanel = GetNode<PanelContainer>("%FriendlyPanel");
        _friendlyContainer = friendlyPanel.GetNode<HBoxContainer>("FriendlyContainer");
        _enemyStatusPanel = GetNode<Control>("%StatusPanel");
        _enemyContainer = GetNode<BoxContainer>("MainMargin/RootLayout/Battlefield/BattlefieldOverlay/StatusPanel/StatusColumn/EnemyContainer");
        CreateBossStatusUi(GetNode<Control>("MainMargin/RootLayout/Battlefield/BattlefieldOverlay"));
        friendlyPanel.AddToGroup("tutorial_player_area");
        GetNode<Control>("MainMargin/RootLayout/Battlefield/BattlefieldOverlay/StatusPanel").AddToGroup("tutorial_enemy_area");
        EnsureFriendlySlotsBuilt();
        EnsureEnemySlotsBuilt();
        _restartButton = GetNode<Button>("%RestartButton");
        _restartButton.Pressed += RestartBattle;
        _returnToMainMenuButton = new Button
        {
            Text = Localization.Get("battle.return_menu"),
            CustomMinimumSize = new Vector2(130, 42)
        };
        _returnToMainMenuButton.Pressed += OnReturnToMainMenuPressed;
        var topBarRow = GetNode<HBoxContainer>("MainMargin/RootLayout/TopBar/TopBarRow");
        topBarRow.AddChild(_returnToMainMenuButton);
        topBarRow.MoveChild(_returnToMainMenuButton, 2);
        _debugButton = new Button
        {
            Text = Localization.Get("battle.dev_btn"),
            CustomMinimumSize = new Vector2(140, 42),
            Visible = DeveloperModeManager.IsDeveloperMode
        };
        _debugButton.Pressed += ShowBattleDebugWindow;
        topBarRow.AddChild(_debugButton);
        topBarRow.MoveChild(_debugButton, 3);
        _skillDebugButton = GetNode<Button>("%SkillDebugButton");
        _skillDebugButton.Hide();
        _battleLogButton = GetNode<Button>("%BattleLogButton");
        _battleLogButton.Pressed += ShowBattleLogWindow;
        _battleLogButton.Visible = true;
        _battleLogButton.AddToGroup("tutorial_battle_log");
        DeveloperModeManager.ModeChanged += RefreshDevModeButtons;
        Localization.LanguageChanged += RefreshLocalizedButtonTexts;

        // 之前只在语言切换事件里刷新这几个按钮的文本，首次进入战斗（包括新手教程
        // 复用的这同一个 Battle 场景）时它们会一直显示 Battle.tscn 里硬编码的占位
        // 文本（例如"重新开始"，恰好是中文所以中文语言下不易察觉），不是真正来自
        // Localization 的当前语言。这里在按钮全部创建完成后立即调用一次，保证首次
        // 加载就和 Localization 的当前语言一致。
        RefreshLocalizedButtonTexts();

        // BattleEffectLayer：选择确认、飘字、出牌揭示弹窗等浮层统一挂载于此，与 %Battlefield 的全局矩形保持同步。
        // 敌方出牌与玩家出牌的唯一展示控件是 ShowCardRevealPopup 中创建的揭示弹窗，居中显示于 BattleField。
        _battlePresentationArea = new Control
        {
            Name = "BattleEffectLayer",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 70
        };
        AddChild(_battlePresentationArea);
        CreateSelectionConfirmUi();
        CreateGameOverOverlay();
        CreateLowHealthScreenBorder();
        CreatePlayerDamageBorder();
    }

    private void CreateLowHealthScreenBorder()
    {
        _lowHealthScreenBorder = new LowHealthScreenBorder
        {
            Name = "LowHealthScreenBorder",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 120,
            ZAsRelative = false
        };
        AddChild(_lowHealthScreenBorder);
    }

    private void CreatePlayerDamageBorder()
    {
        _damageVignetteLayer = new CanvasLayer
        {
            Name = "DamageVignetteLayer",
            Layer = 21
        };
        AddChild(_damageVignetteLayer);

        _playerDamageBorder = new PlayerDamageBorder
        {
            Name = "PlayerDamageBorder",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 0
        };
        _playerDamageBorder.SetAnchorsPreset(LayoutPreset.FullRect);
        _playerDamageBorder.OffsetLeft = 0;
        _playerDamageBorder.OffsetTop = 0;
        _playerDamageBorder.OffsetRight = 0;
        _playerDamageBorder.OffsetBottom = 0;
        _damageVignetteLayer.AddChild(_playerDamageBorder);

        _dodgeShieldScreenBorder = new DodgeShieldScreenBorder
        {
            Name = "DodgeShieldScreenBorder",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1
        };
        _dodgeShieldScreenBorder.SetAnchorsPreset(LayoutPreset.FullRect);
        _dodgeShieldScreenBorder.OffsetLeft = 0;
        _dodgeShieldScreenBorder.OffsetTop = 0;
        _dodgeShieldScreenBorder.OffsetRight = 0;
        _dodgeShieldScreenBorder.OffsetBottom = 0;
        _damageVignetteLayer.AddChild(_dodgeShieldScreenBorder);
    }

    private void ShowPlayerDamageBorder(int actualDamage, int maxHp)
    {
        _playerDamageBorder?.ShowDamage(actualDamage, maxHp);
    }

    private void ShowPlayerDodgeShieldBorder()
    {
        _dodgeShieldScreenBorder?.ShowShield();
    }

    private void EnsureActionSlotsBuilt()
    {
        if (_actionArea == null)
        {
            return;
        }

        if (_actionSlots[0] != null && IsInstanceValid(_actionSlots[0]))
        {
            return;
        }

        foreach (var child in _actionArea.GetChildren())
        {
            _actionArea.RemoveChild(child);
            child.QueueFree();
        }

        for (var i = 0; i < _actionSlots.Length; i++)
        {
            var slot = new ActionSlot();
            slot.Initialize(i);
            slot.ActionRequested += OnActionCardPressed;
            _actionSlots[i] = slot;
            _actionArea.AddChild(slot);
        }
    }

    private void CreateSelectionConfirmUi()
    {
        if (_battlePresentationArea == null)
        {
            return;
        }

        _selectionConfirmPanel = new PanelContainer
        {
            Name = "SelectionConfirmPanel",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        BattleReadBarLayout.PlaceAboveActionArea(_selectionConfirmPanel, new Vector2(480, 72));

        _selectionConfirmPanel.AddThemeStyleboxOverride("panel", BattleUiSkin.CreateTooltipStyle(14));

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 8);
        _selectionConfirmPanel.AddChild(root);

        _selectionConfirmLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = string.Empty
        };
        _selectionConfirmLabel.AddThemeFontSizeOverride("font_size", 26);
        _selectionConfirmLabel.AddThemeColorOverride("font_color", new Color(0.89f, 0.98f, 0.96f));
        root.AddChild(_selectionConfirmLabel);

        _selectionProgressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(400, 16)
        };
        _selectionProgressBar.AddThemeStyleboxOverride("background", BattleUiSkin.CreateHpBackgroundStyle());
        _selectionProgressBar.AddThemeStyleboxOverride("fill", BattleUiSkin.CreateHpFillStyle());
        root.AddChild(_selectionProgressBar);

        _battlePresentationArea.AddChild(_selectionConfirmPanel);
    }

    private void EnsureFriendlySlotsBuilt()
    {
        const int desiredCount = 1;
        if (_friendlyCards.Count == desiredCount) return;
        RebuildFriendlyCards(desiredCount);
    }

    private void RebuildFriendlyCards(int count)
    {
        if (_friendlyContainer == null) return;
        foreach (var child in _friendlyContainer.GetChildren())
        {
            // QueueFree 在帧末才释放。先脱离 Container，避免重建当帧的新旧卡片
            // 同时参与最小尺寸计算，使右对齐 HUD 短暂向左偏移。
            _friendlyContainer.RemoveChild(child);
            child.QueueFree();
        }
        _friendlyCards.Clear();

        for (var i = 0; i < count; i++)
        {
            var card = _characterStatusCardScene.Instantiate<CharacterStatusCard>();
            card.Name = $"PlayerCard{i + 1}";
            card.EnableInlineTooltips = false;
            BattleCardLayout.ConfigureFriendlyCard(card, count);
            card.MouseEntered += () => OnFriendlyStatusCardMouseEntered(card);
            card.MouseExited += () => OnFriendlyStatusCardMouseExited(card);
            _friendlyCards.Add(card);
            _friendlyContainer.AddChild(card);
        }
    }

    private void EnsureEnemySlotsBuilt()
    {
        var desiredCount = System.Math.Max(_encounter.Enemies.Count, 1);
        if (_enemyCards.Count == desiredCount) return;
        RebuildEnemyCards(desiredCount);
    }

    private void RebuildEnemyCards(int count)
    {
        if (_enemyContainer == null) return;
        foreach (var child in _enemyContainer.GetChildren()) child.QueueFree();
        _enemyCards.Clear();

        for (var i = 0; i < count; i++)
        {
            if (i == 1)
            {
                _enemyContainer.AddChild(new Control
                {
                    Name = "EnemyUiSideSpacer",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Ignore
                });
            }

            var card = _characterStatusCardScene.Instantiate<CharacterStatusCard>();
            card.Name = $"EnemyCard{i + 1}";
            card.EnableInlineTooltips = false;
            BattleCardLayout.ConfigureEnemyCard(card, count);
            card.MouseEntered += () => OnEnemyStatusCardMouseEntered(card);
            card.MouseExited += () => OnEnemyStatusCardMouseExited(card);
            card.GuiInput += @event => OnEnemyCardInput(card, @event);
            _enemyCards.Add(card);
            _enemyContainer.AddChild(card);
        }
    }

    private void OnEnemyCardInput(CharacterStatusCard card, InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        var unit = card.Unit;
        if (unit == null || unit.IsDead) return;
        _selectedTarget = unit;
        RefreshSelectedTargetHighlight();
        RefreshUi();
    }

    private void OnFriendlyStatusCardMouseEntered(CharacterStatusCard card)
    {
        if (card.Unit == null)
        {
            return;
        }

        TooltipPresenter.ShowUnit(card.Unit, _context, card);
        card.SetLinkedHighlight(new Color(0.25f, 0.78f, 0.50f), true);
    }

    private void OnFriendlyStatusCardMouseExited(CharacterStatusCard card)
    {
        card.SetLinkedHighlight(new Color(0.25f, 0.78f, 0.50f), false);
        TooltipPresenter.Hide();
    }

    private void OnEnemyStatusCardMouseEntered(CharacterStatusCard card)
    {
        if (card.Unit == null)
        {
            return;
        }

        _selectedTarget = card.Unit;
        RefreshUi();
        TooltipPresenter.ShowEnemy(card.Unit, _context, card);
        if (_bossStatusUi != null)
        {
            _selectionPresenter.HighlightBoss(card.Unit, _bossStatusUi);
        }
    }

    private void OnEnemyStatusCardMouseExited(CharacterStatusCard card)
    {
        // 状态卡离开只隐藏说明；Boss 的黄色选中反馈必须保持到目标切换。
        TooltipPresenter.Hide();
    }

    private void CreateBossStatusUi(Control battlefieldOverlay)
    {
        _bossStatusUi = new BossStatusUI
        {
            Name = "BossStatusUI",
            Visible = false,
            ZIndex = 60,
            MouseFilter = MouseFilterEnum.Stop
        };
        _bossStatusUi.AnchorLeft = 0.5f;
        _bossStatusUi.AnchorRight = 0.5f;
        _bossStatusUi.AnchorTop = 0.0f;
        _bossStatusUi.AnchorBottom = 0.0f;
        _bossStatusUi.OffsetLeft = -460.0f;
        _bossStatusUi.OffsetRight = 460.0f;
        _bossStatusUi.OffsetTop = 20.0f;
        _bossStatusUi.OffsetBottom = 132.0f;
        _bossStatusUi.MouseEntered += OnBossStatusUiMouseEntered;
        _bossStatusUi.MouseExited += OnBossStatusUiMouseExited;
        _bossStatusUi.GuiInput += OnBossStatusUiInput;
        battlefieldOverlay.AddChild(_bossStatusUi);
    }

    private void OnBossStatusUiMouseEntered()
    {
        if (_bossStatusUi?.Unit == null)
        {
            return;
        }

        _selectedTarget = _bossStatusUi.Unit;
        RefreshUi();
        TooltipPresenter.ShowEnemies(GetVisibleBossEnemies(), _context, _bossStatusUi);
        _selectionPresenter.HighlightBoss(_bossStatusUi.Unit, _bossStatusUi);
    }

    private void OnBossStatusUiMouseExited()
    {
        // 顶部Boss状态区与模型共享同一个选中目标，离开状态区不能取消选中高亮。
        TooltipPresenter.Hide();
    }

    private void OnBossStatusUiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            return;
        }

        if (_bossStatusUi?.Unit == null || _bossStatusUi.Unit.IsDead)
        {
            return;
        }

        _selectedTarget = _bossStatusUi.Unit;
        RefreshSelectedTargetHighlight();
        RefreshUi();
    }

    private void ConfigureStaticUi()
    {
        SetLabelFontSize(_playerActionDisplayLabel, 30);
        SetLabelFontSize(_turnCounterLabel, 48);

        GetNode<PanelContainer>("%TopBar").AddThemeStyleboxOverride("panel", BattleUiSkin.CreateHeaderStyle(10));
        ApplyTransparentPanelStyle("%StatusPanel");
        ApplyTransparentPanelStyle("%FriendlyPanel");
        ApplyTransparentPanelStyle("%Battlefield");
        ApplyTransparentPanelStyle("%BattleStage");
        ApplyTransparentPanelStyle("%ActionPanel");

        var topBarRow = GetNode<HBoxContainer>("MainMargin/RootLayout/TopBar/TopBarRow");
        foreach (var child in topBarRow.GetChildren())
        {
            if (child is Button button)
            {
                BattleUiSkin.ApplyButton(button);
            }
        }
    }

    private static void SetLabelFontSize(Label? label, int size)
    {
        label?.AddThemeFontSizeOverride("font_size", size);
    }

    private void ApplyPanelStyle(string nodeName, Color background, Color border)
    {
        var panel = GetNode<PanelContainer>(nodeName);
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            ContentMarginBottom = 18,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 18
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private void ApplyTransparentPanelStyle(string nodeName)
    {
        var panel = GetNode<PanelContainer>(nodeName);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = new Color(0, 0, 0, 0),
            ContentMarginBottom = 8,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8
        });
    }

    private void StartBattle()
    {
        DismissCardRevealPopup();
        DismissCardOverflowRemovalChoice();
        _battleRunId += 1;
        _battleMessageVersion += 1;
        // 图鉴：调试战斗从此处开始整场都不计入永久统计（教学战斗由 TutorialManager.IsActive
        // 在 CodexService 内部自动挡住，不需要在这里额外判断）。
        CodexService.DebugBattleActive = _battleDebugMode;
        _player.SetDisplayName(Localization.Get("battle.player_name"));
        _encounter.Enemies.Clear();
        if (_battleDebugMode)
        {
            _encounter.IsBossFight = false;
            BuildEncounterForDebugMode();
            _player.ResetForNewBattle(_debugPlayerMaxHp, _debugPlayerMaxHp, _debugPlayerMana);
        }
        else
        {
            _encounter.IsBossFight = GameManager.GetNode(GameManager.CurrentNodeId)?.Type == MapNodeType.Boss;
            BuildEncounterForCurrentStage();
            GameManager.RecordPreBattleState();
            _player.ResetForNewBattle(GameManager.MaxHP, GameManager.GetPlayerInitialHealth(), GameManager.GetPlayerInitialMana());
            ApplyRunEquipmentBattleStartBonuses();
        }
        if (!_battleDebugMode && GameManager.CurrentCharacter != null)
        {
            // ResetForNewBattle 会按白板模式清理 Character，因此必须在 Reset 后恢复当前
            // Run 的不可变角色资料。这里只修复角色身份，不改变 HP、费用或技能结算。
            _player.SetCharacter(GameManager.CurrentCharacter);
        }
        // 图鉴：本场战斗遭遇到的每个敌人实例都算一次"发现"（不要求击败）；
        // 共享HP Pool的多个显示实体各自也是真实的 EnemyDefinition，遭遇同样各自计一次。
        foreach (var encounterEnemy in _encounter.Enemies)
        {
            CodexService.RecordEnemyEncounter(encounterEnemy.Definition.Id, GameManager.CurrentChapter);
            if (encounterEnemy.Definition.Type == EnemyType.Boss)
            {
                CodexService.RecordBossChallenge(encounterEnemy.Definition.Id);
            }
        }
        _selectedTarget = GetDefaultEnemyTarget();
        _enemyAi.Reset();
        _turnNumber = 1;
        _lastLoggedVisibleEnemySlots = -1;
        _lastLoggedEncounterCount = -1;
        _battleFinishedEmitted = false;
        _inputLocked = true;
        _isShowingBattleMessage = false;
        _pendingBattleResult = null;
        _battleLogManager.BeginBattle(
            string.IsNullOrWhiteSpace(GameManager.CurrentNodeId)
                ? (_battleDebugMode ? "developer_battle" : "unknown")
                : GameManager.CurrentNodeId);
        _battleLogSelectedRunId = _battleLogManager.Service.CurrentRun.RunId;
        _battleLogSelectedBattleId = _battleLogManager.Service.CurrentBattle?.BattleId;
        _battleLogSelectedRound = 1;
        var battleStartEntry = _battleLogManager.CreateEntry(
            0,
            BattleLogCategory.System,
            BattleLogEventKind.BattleStart,
            BattlePhase.StartPhase.ToString(),
            "battlelog.battle_start");
        _battleLogManager.Add(battleStartEntry);
        ApplyPersistentRuntimeSkills();
        ApplyBattleDebugPlayerState();
        _context = new BattleContext(_player, _triggerManager);
        _context.Encounter = _encounter;
        _context.TurnCounter = _turnNumber;
        _context.LogService = _battleLogManager.Service;
        _context.PlayerDamageBorderRequested = ShowPlayerDamageBorder;
        _context.PlayerSkillPresentationRequested = request => _skillTriggerToastQueue?.Enqueue(request);
        _context.HuangTianVisualRequested = PresentHuangTianVisual;
        _skillTriggerToastQueue?.Clear();
        GameManager.CurrentBattleTurnNumber = _turnNumber;
        _context.EnableEffectDebugLog = true;
        if (_battleDebugMode)
        {
            LogBattleDebugEncounterToContext();
        }
        _triggerManager.RaiseTrigger(TriggerTiming.OnGameStart, _context);
        ClearStack();
        HideGameOverOverlay();
        RenderPlayerSkills();
        RefreshSkillDebugOwnedList();
        RefreshBattleDebugWindow();
        RebuildBattleLogView();
        UpdateRecentReport();
        _ = EnterStartPhase(_battleRunId);
    }

    private void ApplyRunEquipmentBattleStartBonuses()
    {
        // 黄月英【如影随行】锁定最大生命值。背包/已装备物品的战斗开局加成也属于
        // "获得额外最大生命值"，不能借由这里的直接 DebugSet 调用绕开 GameManager.AddMaxHp。
        if (GameManager.HasPersistentPlayerSkill(SkillIds.RuYingSuiXing))
        {
            return;
        }

        var maxHealthBonus =
            GameManager.CountEquipment(EquipmentIds.RustShield) * 10
            + GameManager.CountEquipment(EquipmentIds.GangDun) * 30
            + GameManager.CountEquipment(EquipmentIds.GiantShield) * 45
            + GameManager.CountEquipment(EquipmentIds.SilverLion) * 20
            + GameManager.CountEquipment(EquipmentIds.Tengjia) * 10
            + GameManager.CountEquipment(EquipmentIds.ScaleArmor) * 10
            + GameManager.CountEquipment(EquipmentIds.IronHeavyArmor) * 20
            + GameManager.CountEquipment(EquipmentIds.ClampExoskeleton) * 50
            + GameManager.CountEquipment(EquipmentIds.Conductor) * 10
            + GameManager.CountEquipment(EquipmentIds.WaterproofModule) * 10
            + GameManager.CountEquipment(EquipmentIds.TycoonArmor) * 10;
        if (maxHealthBonus <= 0)
        {
            return;
        }

        _player.DebugSetMaxHealth(_player.MaxHealth + maxHealthBonus);

        // 【煞气缠身】要求当前生命恒为1。装备仍可保留其最大生命加成，
        // 但绝不能把当前生命从1一并抬高。
        if (RunBuffManager.CountStacks(RunBuffIds.ShaQiChenShen) > 0)
        {
            _player.SetCurrentHealth(1);
        }
        else
        {
            _player.DebugSetHealth(_player.Health + maxHealthBonus);
        }
        _context?.AddTriggerLog($"[Equipment] 战斗开始最大生命 +{maxHealthBonus}");
    }

    private void BuildEncounterForDebugMode()
    {
        GD.Print("[Debug] Rebuild Encounter");

        var collectivePresetExpanded = false;
        for (var i = 0; i < _debugEnemyIds.Length; i++)
        {
            var enemyId = _debugEnemyIds[i];
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                continue;
            }

            if (enemyId == "__shu_han_collective__")
            {
                BuildShuHanCollectiveDebugEncounter();
                collectivePresetExpanded = true;
                break;
            }

            var enemy = EnemyFactory.CreateEnemy(enemyId);
            if (enemy == null)
            {
                continue;
            }

            enemy.RuntimeStates["debug_slot"] = i;
            _encounter.Enemies.Add(enemy);
            GD.Print($"[Debug] Enemy Slot={i + 1}, Enemy={enemy.Name}");
        }

        if (!collectivePresetExpanded && _encounter.Enemies.Count == 0)
        {
            var fallbackEnemy = EnemyFactory.CreateEnemy("gate_guard");
            if (fallbackEnemy != null)
            {
                fallbackEnemy.RuntimeStates["debug_slot"] = 0;
                _encounter.Enemies.Add(fallbackEnemy);
                _debugEnemyIds[0] = "gate_guard";
                GD.Print("[Debug] Enemy Slot=1, Enemy=城关守卫 (fallback)");
            }
        }

        GD.Print($"[Debug] Rebuild Encounter Finished. Enemy Count={_encounter.Enemies.Count}");
    }

    private void BuildShuHanCollectiveDebugEncounter()
    {
        var liuBei = EnemyFactory.CreateEnemy("liu_bei");
        var guanYu = EnemyFactory.CreateEnemy("guan_yu");
        var zhangFei = EnemyFactory.CreateEnemy("zhang_fei");
        if (liuBei == null || guanYu == null || zhangFei == null)
        {
            return;
        }

        var sharedPool = new SharedHealthPool(300);
        sharedPool.AddMember(liuBei);
        sharedPool.AddMember(guanYu);
        sharedPool.AddMember(zhangFei);

        liuBei.RuntimeStates["debug_slot"] = 0;
        guanYu.RuntimeStates["debug_slot"] = 1;
        zhangFei.RuntimeStates["debug_slot"] = 2;

        _encounter.IsBossFight = true;
        _encounter.Enemies.Add(liuBei);
        _encounter.Enemies.Add(guanYu);
        _encounter.Enemies.Add(zhangFei);

        GD.Print("[Debug] Enemy Slot=1, Enemy=刘备");
        GD.Print("[Debug] Enemy Slot=2, Enemy=关羽");
        GD.Print("[Debug] Enemy Slot=3, Enemy=张飞");
    }

    private void ApplyBattleDebugPlayerState()
    {
        if (!_battleDebugMode)
        {
            return;
        }

        var character = CharacterDatabase.GetCharacter(_debugCharacterId);
        _player.SetCharacter(character);
        _player.SetDisplayName(Localization.Get("battle.player_name"));
        _player.DebugSetMaxHealth(_debugPlayerMaxHp);
        _player.DebugSetHealth(_debugPlayerHp);
        _player.DebugSetMana(_debugPlayerMana);
        _player.DebugSetInvincible(_debugPlayerInvincible);
    }

    private void RestartBattle()
    {
        StartBattle();
    }

    private void CreateGameOverOverlay()
    {
        if (_battlePresentationArea == null)
        {
            return;
        }

        var overlay = new CenterContainer
        {
            Name = "GameOverOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 95
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);

        var content = new VBoxContainer
        {
            Name = "GameOverContent",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(560, 220)
        };
        content.AddThemeConstantOverride("separation", 18);
        overlay.AddChild(content);

        var title = new Label
        {
            Name = "GameOverTitle",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(560, 72),
            MouseFilter = MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        title.AddThemeColorOverride("font_color", new Color("ffe08a"));
        content.AddChild(title);

        var subtitle = new Label
        {
            Name = "GameOverSubtitle",
            Text = string.Empty,
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(560, 42),
            MouseFilter = MouseFilterEnum.Ignore
        };
        subtitle.AddThemeFontSizeOverride("font_size", 28);
        subtitle.AddThemeColorOverride("font_color", new Color("f4efe0"));
        content.AddChild(subtitle);

        var button = new Button
        {
            Text = Localization.Get("battle.return_map"),
            CustomMinimumSize = new Vector2(180, 52),
            Visible = false
        };
        button.MouseFilter = MouseFilterEnum.Stop;
        button.AddThemeFontSizeOverride("font_size", 24);
        button.ButtonDown += () => GD.Print("[BattleFlow] ReturnToMap button down.");
        button.Pressed += OnGameOverPrimaryActionPressed;
        content.AddChild(button);

        AddChild(overlay);
        _returnToMapButton = button;
    }

    private void ShowGameOverOverlay(string resultText, string subtitleText = "")
    {
        var overlay = GetNodeOrNull<CenterContainer>("GameOverOverlay");
        var title = overlay?.GetNodeOrNull<Label>("GameOverContent/GameOverTitle");
        var subtitle = overlay?.GetNodeOrNull<Label>("GameOverContent/GameOverSubtitle");
        if (overlay == null || title == null || subtitle == null || _returnToMapButton == null)
        {
            return;
        }

        title.Text = resultText;
        subtitle.Text = subtitleText;
        subtitle.Visible = !string.IsNullOrWhiteSpace(subtitleText);
        _returnToMapButton.Text = Localization.Get(_battleDebugMode ? "battle.return_menu" : "battle.return_map");
        _returnToMapButton.Visible = true;
        overlay.Visible = true;
    }

    private void HideGameOverOverlay()
    {
        var overlay = GetNodeOrNull<CenterContainer>("GameOverOverlay");
        if (overlay != null)
        {
            overlay.Visible = false;
        }

        if (_returnToMapButton != null)
        {
            _returnToMapButton.Visible = false;
        }
    }

    private void OnReturnToMapPressed()
    {
        if (_battleFinishedEmitted || _pendingBattleResult == null)
        {
            return;
        }

        _context?.AddTriggerLog("[Flow]");
        _context?.AddTriggerLog("返回地图按钮点击");
        _context?.AddTriggerLog($"BattleFinished -> {(_pendingBattleResult.Value ? "Victory" : "Defeat")}");
        GD.Print($"[BattleFlow] ReturnToMap clicked. result={(_pendingBattleResult.Value ? "Victory" : "Defeat")}, stage={GameManager.CurrentStage}, node={GameManager.CurrentNodeId}");
        GameManager.SaveBattleState(_player.Health, _player.CurrentMana);
        _battleFinishedEmitted = true;
        EmitSignal(SignalName.BattleFinished, _pendingBattleResult.Value);
    }

    private void OnGameOverPrimaryActionPressed()
    {
        if (_battleDebugMode)
        {
            OnReturnToMainMenuPressed();
            return;
        }

        OnReturnToMapPressed();
    }

    private void OnReturnToMainMenuPressed()
    {
        GD.Print($"[BattleFlow] ReturnToMainMenu Clicked. debugMode={_battleDebugMode}, runId={_battleRunId}");
        _context?.AddTriggerLog("[Debug]");
        _context?.AddTriggerLog("ReturnToMainMenu Clicked");
        if (!_battleDebugMode)
        {
            EmitSignal(SignalName.SuspendRunRequested);
            return;
        }

        // 调试战斗也可能通过行动栏溢出选择写入当前Run的牌型移除集合。
        // 返回主菜单意味着这次临时战斗已经结束，必须与正式战斗一样清理Run状态，
        // 否则下一次调试选人会继续继承被移除的卡牌。
        GameManager.EndRun();
        CallDeferred(nameof(EmitReturnToMainMenuRequestedDeferred));
    }

    private void EmitReturnToMainMenuRequestedDeferred()
    {
        GD.Print("[Debug] Emit ReturnToMainMenuRequested");
        EmitSignal(SignalName.ReturnToMainMenuRequested);
    }

    private void ApplyPersistentRuntimeSkills()
    {
        if (_battleDebugMode)
        {
            foreach (var skillId in _debugSkillIds)
            {
                var skill = SkillDatabase.GetSkill(skillId);
                if (skill != null)
                {
                    _player.AddSkill(skill);
                }
            }
            return;
        }

        if (GameManager.CurrentCharacter != null && GameManager.ShouldLoadDefaultCharacterSkills)
        {
            foreach (var skillId in GameManager.CurrentCharacter.SkillIds)
            {
                var skill = SkillDatabase.GetSkill(skillId);
                if (skill != null)
                {
                    _player.AddSkill(skill);
                }
            }
        }

        foreach (var skill in GameManager.AcquiredSkills)
        {
            _player.AddSkill(skill);
        }

        if (GameManager.HasEquipment(EquipmentIds.MoonGem))
        {
            var moonSkill = SkillDatabase.GetSkill(SkillIds.CelestialImpact);
            if (moonSkill != null)
            {
                _player.AddSkill(moonSkill);
            }
        }
    }

    private BattleSnapshot CaptureSnapshot()
    {
        var enemy = GetFirstAliveEnemy();
        return new BattleSnapshot(
            _player.Health,
            _player.CurrentMana,
            enemy?.Health ?? 0,
            enemy?.CurrentMana ?? 0);
    }

    private async Task EnterStartPhase(int runId)
    {
        if (_context == null || IsStaleRun(runId))
        {
            return;
        }

        SetPhase(BattlePhase.StartPhase);
        _inputLocked = true;
        ClearStack();
        RenderActionCards();
        _context.TriggerLogs.Clear();
        ClearCurrentActions();
        SetActionText(Localization.Get("battle.action.start_phase"));
        RefreshUi();

        _triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, _context);

        RefreshUi();

        await Task.CompletedTask;
        if (!IsStaleRun(runId) && !CheckBattleOver())
        {
            EnterBattlePrePhase(runId);
        }
    }

    private void EnterBattlePrePhase(int runId)
    {
        if (_context == null || IsStaleRun(runId))
        {
            return;
        }

        SetPhase(BattlePhase.BattlePrePhase);
        _inputLocked = false;
        ClearStack();
        ClearCurrentActions();
        _triggerManager.RaiseTrigger(TriggerTiming.OnBattlePrePhase, _context);
        if (CheckBattleOver())
        {
            return;
        }

        RenderActionCards();
        if (_player.GuanxingPhase == GuanxingPhase.Recording)
        {
            SetActionText(Localization.Get("battle.action.guanxing_recording"));
        }
        else if (_player.GuanxingPhase == GuanxingPhase.Repeating)
        {
            SetActionText(string.Format(Localization.Get("battle.action.guanxing_repeat_fmt"), _player.GuanxingRepeatsRemaining));
            StartGuanxingRepeatTimer(runId);
        }
        else
        {
            SetActionText(Localization.Get("battle.action.pre_phase"));
        }

        RefreshUi();
    }

    private async Task EnterBattlePhase(BattleAction playerAction, List<EnemyActionEntry> enemyActions, int runId)
    {
        if (_context == null || IsStaleRun(runId))
        {
            return;
        }

        SetPhase(BattlePhase.BattlePhase);
        _inputLocked = true;
        _context.PlayerAction = playerAction;
        _context.ClearEnemyActions();
        foreach (var enemyActionEntry in enemyActions)
        {
            _context.SetActionForEnemy(enemyActionEntry.Enemy, enemyActionEntry.Action);
        }

        var beforeBattle = CaptureSnapshot();
        _triggerManager.RaiseTrigger(TriggerTiming.OnBattleReveal, _context);
        playerAction = _context.PlayerAction ?? playerAction;
        ShowCardRevealPopup(playerAction, enemyActions);
        SetActionText(string.Format(Localization.Get("battle.action.phase_fmt"), playerAction.DisplayName, GetTargetDisplayName(playerAction.Target)));

        _triggerManager.RaiseTrigger(TriggerTiming.OnBattlePhase, _context);

        // 【破军】等“攻击实际命中后立刻追击”的反应必须在本轮普通收尾（OnBattleEnd /
        // BattlePostPhase）之前处理。进入 ReactionMode 后，ActionBar 只按 IReaction 的
        // CardType 渲染，因此这里会只留下那张【破军】卡，而不是普通出牌栏。
        await ProcessReactionQueue(runId);
        if (IsStaleRun(runId) || CheckBattleOver())
        {
            AppendBattleLogEntry();
            return;
        }

        _triggerManager.RaiseTrigger(TriggerTiming.OnBattleEnd, _context);
        PresentBattlePhaseWeaponAndHitEffects(playerAction);
        PresentEnemyAttackAnimations(enemyActions);
        PresentArrowBarrageEffects(playerAction, enemyActions);
        RefreshUi();
        ShowBattlePhasePopups(beforeBattle);
        SetActionText(Localization.Get("battle.action.phase_done"));

        await ShowZhouTaiDiceRollIfPending();

        if (CheckBattleOver())
        {
            AppendBattleLogEntry();
            return;
        }

        if (!IsStaleRun(runId))
        {
            await EnterBattlePostPhase(runId);
        }
    }

    /// <summary>
    /// 周泰【不屈】：若本回合同步的 OnDying 触发链里产生了一次待播放的 1D6 判定结果
    /// （<see cref="BattleContext.PendingDiceRollRequest"/>），在这里异步播放一次通用骰子
    /// 动画（<see cref="DiceRollOverlay"/>），播放完毕后清空请求。伤害/HP/费用等实际后果
    /// 已经在 ZhouTaiBuQuTriggerEffect 的同步 Execute() 里生效，这里只负责表现。
    /// </summary>
    private async Task ShowZhouTaiDiceRollIfPending()
    {
        if (_context?.PendingDiceRollRequest is not { } pendingDice)
        {
            return;
        }

        _inputLocked = true;
        var overlay = new DiceRollOverlay();
        (_battlePresentationArea ?? this).AddChild(overlay);
        await overlay.PlayAsync(
            pendingDice.Result,
            pendingDice.Success,
            Localization.Get("dice.zhoutai_buqu.success_title"),
            Localization.Get("dice.zhoutai_buqu.success_body"),
            Localization.Get("dice.zhoutai_buqu.fail_title"),
            Localization.Get("dice.zhoutai_buqu.fail_body"),
            DeveloperDebugPanel.FastMode);
        overlay.QueueFree();
        _context.PendingDiceRollRequest = null;
        _inputLocked = false;
    }

    /// <summary>
    /// Presentation Framework Phase 2 战斗表现：玩家出攻击牌时在玩家 HUD 附近
    /// 播放一次默认武器挥砍，并让本次被命中的敌人状态卡闪烁一下。
    ///
    /// 只处理这一张卡牌、这一种反馈，不计算伤害、不判断卡牌合法性——伤害此时已经由
    /// 上面的 Trigger 结算完毕，这里只提交 PresentationEvent，具体表现由
    /// PresentationManager/WeaponPresenter/EffectPresenter 决定。
    /// </summary>
    private void PresentBattlePhaseWeaponAndHitEffects(BattleAction playerAction)
    {
        if (!playerAction.IsAttack || _friendlyCards.Count == 0)
        {
            return;
        }

        var cardVisualProfile = CardVisualProfileDatabase.Get(playerAction.Type);
        PresentationManager.PlayWeapon(new PresentationEvent(
            PresentationEventType.WeaponRequested,
            effectId: cardVisualProfile?.EffectId ?? string.Empty,
            payload: _friendlyCards[0]));

        if (playerAction.Target is BattleUnit target)
        {
            var targetAnchor = target is EnemyInstance enemy
                ? GetEnemyStageManaAnchor(enemy) ?? GetEnemyCardForUnit(target)?.AvatarAnchor
                : GetEnemyCardForUnit(target)?.AvatarAnchor;

            if (targetAnchor != null)
            {
                PresentationManager.PlayHit(new PresentationEvent(PresentationEventType.HitResolved, payload: targetAnchor));
            }
        }
    }

    /// <summary>
    /// 本回合所有出了攻击牌的敌人各播放一次扑击动画（舞台模型向玩家方向猛冲再弹回）。
    /// 伤害此时已经由上面的 Trigger 结算完毕，这里只负责表现，不重复判断伤害/命中。
    /// </summary>
    private void PresentEnemyAttackAnimations(List<EnemyActionEntry> enemyActions)
    {
        foreach (var entry in enemyActions)
        {
            if (entry.Action.IsAttack)
            {
                PlayEnemyAttackLunge(entry.Enemy);
            }
        }
    }

    private async Task EnterBattlePostPhase(int runId)
    {
        if (_context == null || IsStaleRun(runId))
        {
            return;
        }

        SetPhase(BattlePhase.BattlePostPhase);
        _inputLocked = true;
        var beforePostPhase = CaptureSnapshot();
        _triggerManager.RaiseTrigger(TriggerTiming.OnBattlePostPhase, _context);
        await ProcessReactionQueue(runId);
        RefreshUi();
        ShowDeltaPopups(beforePostPhase);

        if (CheckBattleOver())
        {
            AppendBattleLogEntry();
            return;
        }

        await Task.CompletedTask;
        if (!IsStaleRun(runId))
        {
            await EnterEndPhase(runId);
        }
    }

    private async Task ProcessReactionQueue(int runId)
    {
        if (_context == null)
        {
            return;
        }

        while (!IsStaleRun(runId) && !_context.GameOver && _context.Reactions.HasPending)
        {
            var reaction = _context.Reactions.Dequeue();
            if (reaction == null)
            {
                return;
            }

            _context.AddTriggerLog($"ReactionWindow：{reaction.Title}");
            var option = await ShowReactionWindow(reaction, runId);
            if (IsStaleRun(runId) || _context.GameOver)
            {
                return;
            }

            option ??= GetFallbackReactionOption(reaction);
            option?.Resolve(_context);
            ResolveDeferredDyingAfterReaction(reaction);
            RefreshUi();
        }
    }

    /// <summary>
    /// 反应选择完成后恢复此前延后的统一濒死流程。
    ///
    /// 不在这里判断任何具体技能；所有需要“先响应、后濒死”的内容都通过
    /// IDeferredDyingReaction 提供原伤害上下文。
    /// </summary>
    private void ResolveDeferredDyingAfterReaction(IReaction reaction)
    {
        if (_context == null
            || reaction is not IDeferredDyingReaction deferred
            || deferred.Target.CurrentHP > 0
            || deferred.Target.IsDead
            || _context.GameOver)
        {
            return;
        }

        var previousDamage = _context.DamageEvent;
        _context.DamageEvent = deferred.TriggeringDamage;
        _context.RaiseOnDying();
        _context.DamageEvent = previousDamage;
    }

    private async Task<ReactionOption?> ShowReactionWindow(IReaction reaction, int runId)
    {
        if (IsStaleRun(runId))
        {
            return null;
        }

        EnterReactionMode(reaction);
        var window = new ReactionWindow();
        UpdateBattlePresentationArea();
        (_battlePresentationArea ?? this).AddChild(window);
        var timeoutTask = window.OpenAsync(reaction);
        var selectionTask = _reactionSelection?.Task;
        if (selectionTask == null)
        {
            return await timeoutTask;
        }

        var completed = await Task.WhenAny(timeoutTask, selectionTask);
        var selected = completed == selectionTask ? selectionTask.Result : null;
        if (completed == selectionTask && !timeoutTask.IsCompleted)
        {
            window.CloseNow();
        }

        if (selected == null)
        {
            _context?.AddTriggerLog("[Reaction]");
            _context?.AddTriggerLog("Timeout");
            _context?.AddTriggerLog("Auto Pass");
        }

        ExitReactionMode();
        return selected;
    }

    private void EnterReactionMode(IReaction reaction)
    {
        _reactionMode = true;
        _activeReaction = reaction;
        _reactionSelection = new TaskCompletionSource<ReactionOption?>();
        _context?.AddTriggerLog("[Reaction]");
        _context?.AddTriggerLog("Enter Reaction Mode");
        SetActionText(string.Format(Localization.Get("battle.action.reaction_prompt_fmt"), reaction.Title));
        RenderReactionCards(reaction);
        RefreshUi();
    }

    private void ExitReactionMode()
    {
        _reactionMode = false;
        _activeReaction = null;
        _reactionSelection = null;
        _reactionOptionsByCard.Clear();
        RenderActionCards();
        RefreshUi();
    }

    private static ReactionOption? GetFallbackReactionOption(IReaction reaction)
    {
        foreach (var option in reaction.Options)
        {
            if (option.Id == "pass" && option.Enabled)
            {
                return option;
            }
        }

        return null;
    }

    private async Task EnterEndPhase(int runId)
    {
        if (_context == null || IsStaleRun(runId))
        {
            return;
        }

        SetPhase(BattlePhase.EndPhase);
        _inputLocked = true;
        var beforeEndPhase = CaptureSnapshot();

        // 观星状态推进：记录回合→存档出牌（不记录观星本身）；重复回合→消耗次数。
        if (_player.GuanxingPhase == GuanxingPhase.Recording && _context.PlayerAction != null
            && _context.PlayerAction.Type != CardType.Guanxing)
        {
            _player.RecordGuanxingAction(_context.PlayerAction.Type, _context.PlayerAction.Count);
            _context.AddTriggerLog("[Guanxing]");
            _context.AddTriggerLog($"玩家记录牌型：{BattleRules.GetCardName(_context.PlayerAction.Type)} ×{_context.PlayerAction.Count}");
            _context.AddTriggerLog($"玩家进入 Repeating 状态：剩余{_player.GuanxingRepeatsRemaining}回合");
        }
        else if (_player.GuanxingPhase == GuanxingPhase.Repeating)
        {
            _player.ConsumeGuanxingRepeat();
            _context.AddTriggerLog("[Guanxing]");
            _context.AddTriggerLog($"玩家消耗一次重复：剩余{_player.GuanxingRepeatsRemaining}回合");
        }

        // 敌人观星状态推进。
        foreach (var enemy in _encounter.Enemies)
        {
            if (enemy.IsDead) continue;
            var enemyEntry = _context.GetEnemyActionEntry(enemy);
            if (enemy.GuanxingPhase == GuanxingPhase.Recording
                && enemyEntry != null
                && enemyEntry.Action.Type != CardType.Guanxing)
            {
                enemy.RecordGuanxingAction(enemyEntry.Action.Type, enemyEntry.Action.Count);
                _context.AddTriggerLog("[Guanxing]");
                _context.AddTriggerLog($"{enemy.DisplayName}记录牌型：{BattleRules.GetCardName(enemyEntry.Action.Type)} ×{enemyEntry.Action.Count}");
                _context.AddTriggerLog($"{enemy.DisplayName}进入 Repeating 状态：剩余{enemy.GuanxingRepeatsRemaining}回合");
            }
            else if (enemy.GuanxingPhase == GuanxingPhase.Repeating)
            {
                enemy.ConsumeGuanxingRepeat();
                _context.AddTriggerLog("[Guanxing]");
                _context.AddTriggerLog($"{enemy.DisplayName}消耗一次重复：剩余{enemy.GuanxingRepeatsRemaining}回合");
            }
        }

        if (_player.HasSkill(SkillIds.Luoshen))
        {
            _player.LuoshenUpdateEndOfTurn();
        }

        _triggerManager.RaiseTrigger(TriggerTiming.OnTurnEnd, _context);

        // 【战场崩坏】首次进入第55回合：屏幕中央提示替代常规的"回合后"文案，
        // ShowBattleMessage 会在 1.5 秒后自己把文案换回正常的阶段提示，
        // 这里不需要额外处理"提示消失后应该显示什么"。
        if (_turnNumber == BattlefieldCollapseEffect.TriggerTurn)
        {
            ShowBattleMessage(Localization.Get("battle.collapse.start"));
        }
        else
        {
            SetActionText(Localization.Get("battle.action.post_phase"));
        }

        RefreshUi();
        ShowDeltaPopups(beforeEndPhase);
        ShowBattlefieldCollapseDamagePopups();
        AppendBattleLogEntry();

        await Task.CompletedTask;
        if (IsStaleRun(runId) || CheckBattleOver())
        {
            return;
        }

        _turnNumber += 1;
        _context.TurnCounter = _turnNumber;
        GameManager.CurrentBattleTurnNumber = _turnNumber;
        _ = EnterStartPhase(runId);
    }

    private bool IsStaleRun(int runId)
    {
        return runId != _battleRunId;
    }

    private void SetPhase(BattlePhase phase)
    {
        _phase = phase;
        if (_context != null)
        {
            _context.Phase = phase;
        }
    }
}
