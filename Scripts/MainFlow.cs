//////////////////////////////////////////////////////////
// 文件：Scripts/MainFlow.cs
//
// 模块：Application Flow
//
// 职责：
// 1. 承载主流程切换、场景编排与全局入口相关代码。
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Application Flow 的公开类：MainFlow。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class MainFlow : Control
{
    private readonly PackedScene _mainMenuScene = GD.Load<PackedScene>("res://Scenes/MainMenu.tscn");
    private readonly PackedScene _codexScene = GD.Load<PackedScene>("res://Scenes/Codex.tscn");
    private readonly PackedScene _characterSelectScene = GD.Load<PackedScene>("res://Scenes/CharacterSelect.tscn");
    private readonly PackedScene _mapScene = GD.Load<PackedScene>("res://Scenes/Map.tscn");
    private readonly PackedScene _battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
    private readonly PackedScene _eventScene = GD.Load<PackedScene>("res://Scenes/Event.tscn");
    private readonly PackedScene _runResultScene = GD.Load<PackedScene>("res://Scenes/RunResult.tscn");
    private readonly PackedScene _inventoryScene = GD.Load<PackedScene>("res://Scenes/Inventory.tscn");
    private readonly PackedScene _shopScene = GD.Load<PackedScene>("res://Scenes/Shop.tscn");
    private readonly PackedScene _initialEventScene = GD.Load<PackedScene>("res://Scenes/InitialEvent.tscn");
    private readonly PackedScene _tutorialSelectScene = GD.Load<PackedScene>("res://Scenes/TutorialSelectScene.tscn");
    private readonly PackedScene _metaTutorialScene = GD.Load<PackedScene>("res://Scenes/MetaTutorialScene.tscn");

    private Control? _sceneHost;
    private Label? _devModeLabel;
    private bool _transitioning;
    private bool _debugBattleSelection;
    private RewardData? _pendingRewardData;
    private BattleManager? _currentBattleManager;
    private Control? _suspendedRunScreen;
    private ConfirmationDialog? _suspendRunDialog;
    private DeveloperDebugPanel? _debugPanel;
    private bool _showTryMetaTutorialHintOnNextMenu;
    private Button? _tutorialSkipButton;
    private readonly Queue<(ChoiceRequest Request, Action<ChoiceResult> OnCompleted)> _factionFateChoiceQueue = new();
    private ChoicePanel? _activeFactionFateChoicePanel;

    /// <summary>
    /// 场景内唯一的 MainFlow 实例。仅供静态 Manager（例如
    /// <see cref="FactionFateManager.TryTriggerEquipmentReselect"/>）在装备获得等
    /// 非UI上下文里，弹出一个复用既有 ChoicePanel 机制的选择框——不新增第二套弹窗系统。
    /// </summary>
    private static MainFlow? Instance { get; set; }

    /// <summary>
    /// 供静态 Manager 调用：复用 MainFlow 既有的 ChoicePanel 展示机制弹出一个选择框。
    /// 不在场景中时（Instance 为空，理论上不应该发生，因为整个游戏流程内 MainFlow 常驻）安全跳过。
    /// </summary>
    public static bool TryShowFactionFateChoicePanel(ChoiceRequest request, Action<ChoiceResult> onCompleted)
    {
        if (Instance == null || !IsInstanceValid(Instance) || !Instance.IsInsideTree())
        {
            return false;
        }

        Instance.EnqueueFactionFateChoicePanel(request, onCompleted);
        return true;
    }

    private void EnqueueFactionFateChoicePanel(ChoiceRequest request, Action<ChoiceResult> onCompleted)
    {
        _factionFateChoiceQueue.Enqueue((request, onCompleted));
        ShowNextFactionFateChoicePanel();
    }

    private void ShowNextFactionFateChoicePanel()
    {
        if (_activeFactionFateChoicePanel != null || _factionFateChoiceQueue.Count == 0)
        {
            return;
        }

        var pending = _factionFateChoiceQueue.Dequeue();
        var panel = new ChoicePanel
        {
            Name = "FactionFateChoicePanel",
            ZIndex = 4000,
            ZAsRelative = false
        };
        _activeFactionFateChoicePanel = panel;
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var handled = false;
        panel.ChoiceCompleted += result =>
        {
            if (handled)
            {
                return;
            }

            handled = true;
            if (panel.GetParent() == this)
            {
                RemoveChild(panel);
            }
            panel.QueueFree();
            _activeFactionFateChoicePanel = null;

            pending.OnCompleted(result);
            ShowNextFactionFateChoicePanel();
        };
        panel.Configure(pending.Request);
    }

    /// <summary>
    /// 供任意静态 Manager（GameManager/RunBuffManager/Player/FactionFateManager/
    /// InventoryManager等，本身都不持有场景节点引用）调用：把一次性提示挂到
    /// MainFlow 这个常驻实例上展示，同一思路复用 ShowFactionFateChoicePanel。
    /// </summary>
    public static void TryShowFirstTimeHint(string hintId)
    {
        if (Instance != null)
        {
            FirstTimeHintManager.TryShow(hintId, Instance);
        }
    }

    /// <summary>
    /// Application Flow 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        Instance = this;
        Localization.Initialize();
        DeveloperModeManager.Initialize();
        DamagePreviewSettings.Initialize();
        TutorialProgress.Initialize();
        TutorialIntegratedProgress.Initialize();
        FirstTimeHintManager.Initialize();
        CodexService.Initialize();
        BattleRules.ValidateCombatRelations();
        LocalizationValidator.RunIfDeveloperMode();
        _sceneHost = GetNode<Control>("SceneHost");
        CreateDevModeLabel();
        CreateTutorialSkipButton();
        CreateDebugPanel();
        GameManager.InitialEventQualityUpgradeTriggered += OnInitialEventQualityUpgradeTriggered;
        ShowMainMenu();
    }

    /// <summary>
    /// Application Flow 的公开入口：_UnhandledInput。
    ///
    /// 只负责 F8 开关 Developer Progress Debug Panel；面板本身不监听输入。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            && CanSuspendCurrentRun())
        {
            RequestSuspendRunToMainMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 } && DeveloperModeManager.IsDeveloperMode)
        {
            ToggleDebugPanel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void CreateDebugPanel()
    {
        _debugPanel = new DeveloperDebugPanel
        {
            GetActiveBattleManager = () => _currentBattleManager,
            DebugSelectNode = nodeId =>
            {
                if (!string.IsNullOrEmpty(nodeId))
                {
                    OnNodeSelected(nodeId);
                }
            },
            OpenTutorialSelectRequested = ShowTutorialSelect,
            OpenCombatTutorialRequested = ShowCombatTutorial,
            OpenMetaTutorialRequested = ShowMetaTutorial
        };
        _debugPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _debugPanel.ZIndex = 500;
        AddChild(_debugPanel);
    }

    private void ToggleDebugPanel()
    {
        if (_debugPanel == null)
        {
            return;
        }

        if (_debugPanel.Visible)
        {
            _debugPanel.Visible = false;
        }
        else
        {
            _debugPanel.RefreshAndShow();
        }
    }

    /// <summary>
    /// Application Flow 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        DeveloperModeManager.ModeChanged -= OnDevModeChanged;
        Localization.LanguageChanged -= RefreshDevModeLabelText;
        GameManager.InitialEventQualityUpgradeTriggered -= OnInitialEventQualityUpgradeTriggered;

        // 清空静态引用，避免场景卸载后静态 Manager（例如 FactionFateManager.TryTriggerEquipmentReselect）
        // 还持有一个已销毁节点的引用。
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void CreateDevModeLabel()
    {
        _devModeLabel = new Label
        {
            Text = Localization.Get("settings.developer_mode"),
            AnchorLeft = 1f, AnchorRight = 1f,
            AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -210, OffsetRight = -12,
            OffsetTop = -36, OffsetBottom = -8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 200,
            Visible = DeveloperModeManager.IsDeveloperMode
        };
        _devModeLabel.AddThemeFontSizeOverride("font_size", 16);
        _devModeLabel.AddThemeColorOverride("font_color", new Color(0.22f, 0.90f, 0.52f));
        AddChild(_devModeLabel);
        DeveloperModeManager.ModeChanged += OnDevModeChanged;
        Localization.LanguageChanged += RefreshDevModeLabelText;
    }

    private void OnDevModeChanged()
    {
        if (_devModeLabel != null)
            _devModeLabel.Visible = DeveloperModeManager.IsDeveloperMode;
    }

    private void RefreshDevModeLabelText()
    {
        if (_devModeLabel != null)
            _devModeLabel.Text = Localization.Get("settings.developer_mode");
    }

    /// <summary>
    /// 整合式教程专用的"跳过教程"按钮：常驻在 MainFlow 自身（不是某个具体教学
    /// 屏幕的子节点），跟随 IntegratedTutorialFlow.IsActive 显隐，因此可以在
    /// 角色选择/地图/战斗/商店/背包/事件任意一个教学屏幕上都点得到，不需要在
    /// 每个屏幕里各自重复实现一次。
    /// </summary>
    private void CreateTutorialSkipButton()
    {
        _tutorialSkipButton = new Button
        {
            Text = Localization.Get("tutorial.integrated.skip_button"),
            AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = 16f, OffsetTop = 16f, OffsetRight = 190f, OffsetBottom = 56f,
            ZIndex = 300,
            Visible = false
        };
        _tutorialSkipButton.AddThemeFontSizeOverride("font_size", 18);
        _tutorialSkipButton.Pressed += OnTutorialSkipPressed;
        AddChild(_tutorialSkipButton);
    }

    private void SetTutorialSkipButtonVisible(bool visible)
    {
        if (_tutorialSkipButton != null) _tutorialSkipButton.Visible = visible;
    }

    private void OnTutorialSkipPressed()
    {
        var confirm = new ConfirmationDialog
        {
            DialogText = Localization.Get("tutorial.integrated.skip_confirm")
        };
        AddChild(confirm);
        confirm.Confirmed += () =>
        {
            confirm.QueueFree();
            IntegratedTutorialFlow.Skip();
            SetTutorialSkipButtonVisible(false);

            // 跳过时若已经选好角色（教程流程已经推进到地图/战斗/商店/背包/事件
            // 任意一步），直接放行到正式第一章，并保留跳过前已经拿到的金币/装备；
            // 若还停在角色选择这一步（还没有 CurrentCharacterId），则回到一个
            // 非教学模式的普通角色选择界面，让玩家正常开始。
            if (string.IsNullOrEmpty(GameManager.CurrentCharacterId))
            {
                var select = _characterSelectScene.Instantiate<CharacterSelectController>();
                select.DebugModeSelection = false;
                select.CharacterChosen += OnCharacterChosen;
                select.ReturnToMenuRequested += OnCharacterSelectReturnToMenuRequested;
                SwitchTo(select);
            }
            else
            {
                GameManager.BuildDefaultMap();
                ShowMap();
            }
        };
        confirm.Canceled += () => confirm.QueueFree();
        confirm.PopupCentered();
    }

    private void OnInitialEventQualityUpgradeTriggered(
        EquipmentDefinition original,
        EquipmentDefinition replacement)
    {
        var toast = new PanelContainer
        {
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 28f,
            OffsetRight = 650f,
            OffsetTop = 92f,
            OffsetBottom = 220f,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 600
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        toast.AddChild(margin);

        var label = new Label
        {
            Text = Localization.GetFmt(
                "initial_event.quality_upgrade.triggered",
                Localization.GetName(original),
                Localization.GetName(replacement)),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(0.34f, 0.92f, 0.94f));
        margin.AddChild(label);
        AddChild(toast);

        var tween = CreateTween();
        tween.TweenInterval(3.2);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.45)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(toast.QueueFree));
    }

    private void ShowMainMenu()
    {
        GD.Print("[MainFlow] ShowMainMenu");
        _transitioning = false;
        var hasSuspendedRun = HasSuspendedRun();
        if (!hasSuspendedRun)
        {
            GameManager.EnterMainMenu();
        }
        // 图鉴：返回主菜单是明确要求的安全落盘节点之一。
        CodexService.SaveIfDirty();
        var menu = _mainMenuScene.Instantiate<MainMenuController>();
        menu.StartGameRequested += OnStartGameRequested;
        menu.ContinueGameRequested += ResumeSuspendedRun;
        menu.TutorialRequested += ShowTutorial;
        menu.CodexRequested += ShowCodex;
        menu.DebugModeRequested += OnDebugModeRequested;
        menu.ExitGameRequested += OnExitGameRequested;
        menu.SetContinueAvailable(hasSuspendedRun);
        SwitchTo(menu);

        if (TutorialIntegratedProgress.TryMarkFirstEntryRecommendationShown())
        {
            menu.ShowTutorialRecommendation();
        }

        if (_showTryMetaTutorialHintOnNextMenu)
        {
            _showTryMetaTutorialHintOnNextMenu = false;
            ShowTryMetaTutorialHint();
        }
    }

    /// <summary>
    /// 战斗教程完成后的一次性提示："要不要试试战斗外教程"。复用
    /// OnInitialEventQualityUpgradeTriggered 同款的轻量 Toast 手法（左上角浮层，
    /// 停留几秒后自动淡出销毁），不新增任何弹窗系统。
    /// </summary>
    private void ShowTryMetaTutorialHint()
    {
        var toast = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = 28f, OffsetRight = 650f, OffsetTop = 92f, OffsetBottom = 160f,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 600
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        toast.AddChild(margin);

        var label = new Label
        {
            Text = Localization.Get("tutorial.hint.try_meta"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        margin.AddChild(label);
        AddChild(toast);

        var tween = CreateTween();
        tween.TweenInterval(3.2);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.45)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(toast.QueueFree));
    }

    private void OnExitGameRequested()
    {
        GetTree().Quit();
    }

    private void ShowCodex()
    {
        var codex = _codexScene.Instantiate<CodexController>();
        codex.ReturnToMenuRequested += ShowMainMenu;
        SwitchTo(codex);
    }

    /// <summary>
    /// 主菜单【新手教程】按钮的响应入口：现在改为先进入教程二级选择界面，
    /// 而不是直接进入战斗教程（MainMenuController.TutorialRequested 信号本身未变）。
    /// </summary>
    private void ShowTutorial()
    {
        ShowTutorialSelect();
    }

    private void ShowTutorialSelect()
    {
        var select = _tutorialSelectScene.Instantiate<TutorialSelectController>();
        select.CombatTutorialRequested += ShowCombatTutorial;
        select.MetaTutorialRequested += ShowMetaTutorial;
        select.IntegratedTutorialRequested += ShowIntegratedTutorialFromMenu;
        select.ReturnToMenuRequested += ShowMainMenu;
        SwitchTo(select);
    }

    /// <summary>
    /// 主菜单"重新体验基础教程"入口：不依赖 completed/skipped 状态，强制重新
    /// 走一遍整合式教程（对应用户§14："允许玩家在设置或主菜单中重新体验"）。
    /// </summary>
    private void ShowIntegratedTutorialFromMenu()
    {
        IntegratedTutorialFlow.Reset();
        StartIntegratedTutorial();
    }

    /// <summary>
    /// 整合式教程真正的起点：角色选择（教学高亮模式）。
    /// 仅由教程菜单手动进入，普通开始游戏不自动触发。
    /// </summary>
    private void StartIntegratedTutorial()
    {
        DiscardSuspendedRunReference();
        IntegratedTutorialFlow.Start();
        SetTutorialSkipButtonVisible(true);

        GameManager.BeginNewRun();
        var select = _characterSelectScene.Instantiate<CharacterSelectController>();
        select.DebugModeSelection = false;
        select.CharacterChosen += OnCharacterChosen;
        select.ReturnToMenuRequested += OnIntegratedTutorialCharacterSelectReturnRequested;
        SwitchTo(select);
    }

    private void OnIntegratedTutorialCharacterSelectReturnRequested()
    {
        // 教学模式下角色选择界面的"返回主菜单"：视为一次跳过（不强行阻止玩家
        // 离开），但不弹二次确认——玩家还没选角色，不存在"已经拿到的奖励"需要
        // 保留，直接按跳过处理最省心。
        IntegratedTutorialFlow.Skip();
        SetTutorialSkipButtonVisible(false);
        ShowMainMenu();
    }

    /// <summary>战斗教程：原 ShowTutorial() 的完整流程，内容/行为不变，仅方法改名。</summary>
    private void ShowCombatTutorial()
    {
        DiscardSuspendedRunReference();
        TutorialManager.ResetForNewRun();
        TutorialManager.StartCombatTutorial();

        var battle = _battleScene.Instantiate<BattleManager>();
        battle.ConfigureDebugMode(CharacterIds.ZhaoYun);
        battle.ReturnToMainMenuRequested += ShowMainMenu;
        battle.GoToMapRequested += ShowMainMenu;
        battle.BattleFinished += playerWon =>
        {
            // 防止"手滑打错牌导致真的输掉训练假人"把玩家直接踢回主菜单、
            // 教程半途而废：真实败局改成重开整场教学战斗，而不是终止教程。
            // 教学保护（拦截致命伤害）已经取消，脚本化课程和综合练习环节的
            // 真实败局都统一靠这里重开整场教学战斗兜底。
            if (!playerWon)
            {
                ShowCombatTutorial();
                return;
            }

            // 战斗教程完成后回到主菜单时，如果战斗外教程还没体验过，顺带提示一次
            // （只是一句话的提示，复用既有 OnInitialEventQualityUpgradeTriggered
            // 同款轻量 Toast 手法，不新增一整套弹窗系统）。
            _showTryMetaTutorialHintOnNextMenu = TutorialProgress.IsCombatCompleted && !TutorialProgress.IsMetaCompleted;
            ShowMainMenu();
        };

        var overlay = new TutorialOverlay();
        overlay.ReturnToMenuRequested += ShowMainMenu;
        battle.AddChild(overlay);

        SwitchTo(battle);
    }

    private void ShowMetaTutorial()
    {
        DiscardSuspendedRunReference();
        TutorialManager.ResetForNewRun();
        TutorialManager.StartMetaTutorial();

        var metaTutorial = _metaTutorialScene.Instantiate<MetaTutorialController>();
        metaTutorial.ReturnToSelectRequested += ShowTutorialSelect;
        SwitchTo(metaTutorial);
    }

    private void OnStartGameRequested()
    {
        DiscardSuspendedRunReference();
        // 开始游戏始终进入普通流程；不改变教程完成/跳过记录。
        IntegratedTutorialFlow.Reset();
        TutorialManager.ResetForNewRun();
        SetTutorialSkipButtonVisible(false);

        GameManager.BeginNewRun();
        var select = _characterSelectScene.Instantiate<CharacterSelectController>();
        select.DebugModeSelection = false;
        select.CharacterChosen += OnCharacterChosen;
        select.ReturnToMenuRequested += OnCharacterSelectReturnToMenuRequested;
        SwitchTo(select);
    }

    private void OnDebugModeRequested()
    {
        GD.Print("[MainFlow] DebugModeRequested");
        DiscardSuspendedRunReference();
        // Developer Battle 不调用 SelectCharacter，因此不能依赖角色选择阶段顺带重置Run。
        // 每次进入调试选人都显式开启干净Run，避免上一次调试战斗移除的行动牌泄漏。
        GameManager.BeginNewRun();
        _debugBattleSelection = true;
        var select = _characterSelectScene.Instantiate<CharacterSelectController>();
        select.DebugModeSelection = true;
        select.CharacterChosen += OnCharacterChosen;
        select.ReturnToMenuRequested += OnCharacterSelectReturnToMenuRequested;
        SwitchTo(select);
    }

    private void OnCharacterSelectReturnToMenuRequested()
    {
        _debugBattleSelection = false;
        ShowMainMenu();
    }

    private async void OnCharacterChosen(string characterId)
    {
        if (_debugBattleSelection)
        {
            GD.Print($"[MainFlow] Enter BattleDebugScene with character={characterId}");
            var battle = _battleScene.Instantiate<BattleManager>();
            battle.ConfigureDebugMode(characterId);
            battle.ReturnToMainMenuRequested += ShowMainMenu;
            battle.GoToMapRequested += ShowMap;
            _debugBattleSelection = false;
            SwitchTo(battle);
            return;
        }

        GameManager.SelectCharacter(characterId);

        // 先切到地图场景再播放揭示动画：老虎机是挂在 Main 根节点下的全屏 overlay，
        // 与 SceneHost 里的地图是两个独立节点，谁先谁后不影响 overlay 能否盖住地图；
        // 调整为先进地图再弹出，是为了匹配"选完角色→先看到地图→再跳出命运揭示"的
        // 演出节奏（此前是角色选择界面还没消失就弹出，观感是在选人画面里跳出）。
        if (IntegratedTutorialFlow.IsActive)
        {
            ShowIntegratedTutorialMap();
        }
        else
        {
            ShowMap();
        }

        await PlayFactionFateRevealAnimation();
        await PlayPendingFactionDiceAnimation();
    }

    // ════════════════════════════════════════════════════════════════════
    // 整合式教程：角色选择之后的宏观屏幕顺序（教学地图→教学战斗→奖励→
    // 教学商店→背包装备→回教学地图→教学事件→完成→正式第一章）。
    // 每一步都复用真实的 MapController/BattleManager/RewardController/
    // ShopController/InventoryController/EventController，本节只负责编排
    // 顺序与最小必要的胶水代码，不重新实现任何屏幕。
    // ════════════════════════════════════════════════════════════════════

    private bool _tutorialMapLegendShown;

    private void ShowIntegratedTutorialMap()
    {
        _transitioning = false;
        var firstTimeOnTutorialMap = GameManager.Nodes.Count == 0 || GameManager.GetNode(IntegratedTutorialFlow.TutorialBattleNodeId) == null;
        IntegratedTutorialFlow.AdvanceTo(IntegratedTutorialStage.Map);
        GameManager.BuildTutorialMap();
        GameManager.SetRunState(RunState.Map);

        var map = _mapScene.Instantiate<MapController>();
        map.NodeSelected += OnNodeSelected;
        map.ReturnToMenuRequested += OnTutorialSkipPressed;
        map.InventoryRequested += ShowInventory;
        SwitchTo(map);

        // 节点类型图例 + 节点/电量说明：只在教学地图第一次出现时展示一遍纯文字
        // 讲解（不高亮具体节点——节点渲染在 SubViewport 内部，从 MainFlow 这层
        // 直接对其内部控件取屏幕矩形并不可靠，这里退化为纯文字说明，Godot内
        // 实际渲染效果仍需人工验证）。之后每次返回教学地图（商店/背包教学
        // 完成后）不再重复弹出。
        if (firstTimeOnTutorialMap && !_tutorialMapLegendShown)
        {
            _tutorialMapLegendShown = true;
            ShowTutorialMapLegend(map);
        }
    }

    private void ShowTutorialMapLegend(Control host)
    {
        var layer = new TutorialHighlightLayer();
        host.AddChild(layer);

        layer.ShowStep(
            Localization.Get("tutorial.integrated.map.node_types.title"),
            Localization.Get("tutorial.integrated.map.node_types.desc"),
            string.Empty,
            string.Empty,
            showContinueButton: true);

        layer.ContinueRequested += () =>
        {
            layer.ShowStep(
                Localization.Get("tutorial.integrated.map.node.title"),
                Localization.Get("tutorial.integrated.map.node.desc") + "\n\n" + Localization.Get("tutorial.integrated.map.power.desc"),
                Localization.Get("tutorial.integrated.map.node.objective"),
                string.Empty,
                showContinueButton: true);

            // 第二次点"继续"之后关闭说明层，把真实节点交给玩家点击——这里只
            // 需要隐藏教学层，不需要再监听 ContinueRequested 第三次触发。
            void HideOnce()
            {
                layer.HideAll();
                layer.QueueFree();
            }
            layer.ContinueRequested += HideOnce;
        };
    }

    private void ShowIntegratedTutorialBattle()
    {
        IntegratedTutorialFlow.AdvanceTo(IntegratedTutorialStage.Battle);
        TutorialManager.ResetForNewRun();
        TutorialManager.StartCombatTutorial();

        var battle = _battleScene.Instantiate<BattleManager>();
        battle.ConfigureDebugMode(GameManager.CurrentCharacterId);
        battle.ReturnToMainMenuRequested += OnTutorialSkipPressed;
        battle.GoToMapRequested += ShowIntegratedTutorialMap;
        // 真实战斗胜利检测和 TutorialController 的逐帧敌人血量检测是两条独立
        // 路径（后者驱动 TutorialManager.TutorialCompleted，见下方 overlay 订阅）；
        // 这里只处理防御性的"意外落败"分支，重开教学战斗，避免卡死在败局画面。
        battle.BattleFinished += playerWon =>
        {
            if (!playerWon) ShowIntegratedTutorialBattle();
        };

        var overlay = new TutorialOverlay();
        overlay.ReturnToMenuRequested += OnTutorialSkipPressed;
        overlay.IntegratedFlowBattleWon += OnIntegratedTutorialBattleWon;
        battle.AddChild(overlay);

        SwitchTo(battle);
    }

    private void OnIntegratedTutorialBattleWon()
    {
        GameManager.RecoverHealthAfterBattle();
        ShowIntegratedTutorialReward();
    }

    private void ShowIntegratedTutorialReward()
    {
        IntegratedTutorialFlow.AdvanceTo(IntegratedTutorialStage.Reward);

        // 一次性标记防止中途返回/重进导致重复发放（对应用户§15/§18.18）。
        if (!TutorialIntegratedProgress.RewardGranted)
        {
            var equipmentDef = EquipmentDatabase.GetEquipment(IntegratedTutorialFlow.RewardEquipmentId);
            var reward = new RewardData { Gold = IntegratedTutorialFlow.RewardGold };
            reward.EquipmentIds.Add(IntegratedTutorialFlow.RewardEquipmentId);
            reward.EquipmentNames.Add(equipmentDef == null ? IntegratedTutorialFlow.RewardEquipmentId : Localization.GetName(equipmentDef));

            var rewardView = new RewardController
            {
                TitleText = Localization.Get("tutorial.integrated.reward.title"),
                Reward = reward,
                RewardAlreadyClaimed = false
            };
            rewardView.RewardClaimed += () =>
            {
                if (!TutorialIntegratedProgress.RewardGranted)
                {
                    RewardManager.ApplyReward(reward);
                    TutorialIntegratedProgress.RewardGranted = true;
                }
                ShowIntegratedTutorialShop();
            };
            SwitchTo(rewardView);
            return;
        }

        ShowIntegratedTutorialShop();
    }

    private void ShowIntegratedTutorialShop()
    {
        IntegratedTutorialFlow.AdvanceTo(IntegratedTutorialStage.Shop);
        var shop = _shopScene.Instantiate<ShopController>();
        shop.ExitShopRequested += ShowIntegratedTutorialMap;
        AttachShopChipChoice(shop);
        SwitchTo(shop);
    }

    private void OnIntegratedTutorialEventClosed()
    {
        TutorialIntegratedProgress.EventDone = true;
        ShowIntegratedTutorialComplete();
    }

    private void ShowIntegratedTutorialComplete()
    {
        IntegratedTutorialFlow.AdvanceTo(IntegratedTutorialStage.Complete);
        SetTutorialSkipButtonVisible(false);

        var root = new Control { MouseFilter = MouseFilterEnum.Stop };
        root.SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = new Color(0.07f, 0.08f, 0.10f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.10f, 0.13f, 0.98f),
            BorderColor = new Color(1f, 0.85f, 0.3f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 32, ContentMarginRight = 32,
            ContentMarginTop = 28, ContentMarginBottom = 28
        });
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        var title = new Label
        {
            Text = Localization.Get("tutorial.integrated.complete.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(title);

        var desc = new Label
        {
            Text = Localization.Get("tutorial.integrated.complete.desc"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        desc.AddThemeFontSizeOverride("font_size", 18);
        desc.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.92f));
        vbox.AddChild(desc);

        var startButton = new Button
        {
            Text = Localization.Get("tutorial.integrated.complete.start_button"),
            CustomMinimumSize = new Vector2(0, 56)
        };
        startButton.AddThemeFontSizeOverride("font_size", 22);
        startButton.Pressed += () =>
        {
            IntegratedTutorialFlow.Complete();
            GameManager.BuildDefaultMap();
            ShowMap();
        };
        vbox.AddChild(startButton);

        SwitchTo(root);
    }

    private void ShowMap()
    {
        _transitioning = false;
        // 图鉴：返回地图覆盖了战斗结束/事件结束/商店离开等绝大多数"回合之间的安全间隙"，
        // 作为统一的落盘检查点，比在每个具体流程各自补一次调用更不容易漏掉。
        CodexService.SaveIfDirty();

        // 初始事件②：自动销毁5件装备后，显示传奇装备二选一
        if (GameManager.InitialEventAutoDestroyRewardPending)
        {
            GameManager.InitialEventAutoDestroyRewardPending = false;
            ShowEquipmentChoice(
                CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Legendary }, 2),
                Localization.Get("mainflow.ie02_reward.title"),
                Localization.Get("mainflow.ie02_reward.desc"),
                Localization.Get("ui.skip"),
                equipmentId =>
                {
                    if (!string.IsNullOrEmpty(equipmentId))
                        GameManager.AddEquipment(equipmentId);
                    ShowMap();
                },
                rarities: new[] { EquipmentRarity.Legendary });
            return;
        }

        // 初始事件⑭：前3场战斗0伤害奖励
        if (GameManager.InitialEventNoDamageRewardPending)
        {
            GameManager.InitialEventNoDamageRewardPending = false;
            ShowEquipmentChoice(
                CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Epic }, 3),
                Localization.Get("mainflow.nodamage_reward.title"),
                Localization.Get("mainflow.nodamage_reward.desc"),
                Localization.Get("ui.skip"),
                equipmentId =>
                {
                    if (!string.IsNullOrEmpty(equipmentId))
                        GameManager.AddEquipment(equipmentId);
                    ShowMap();
                },
                rarities: new[] { EquipmentRarity.Epic });
            return;
        }

        // 初始事件㉑【财富契约】：三件专属装备三选一（固定候选，不走随机池）
        if (GameManager.InitialEventFortuneContractPending)
        {
            GameManager.InitialEventFortuneContractPending = false;
            var fortuneChoices = new List<EquipmentDefinition>();
            foreach (var id in new[] { EquipmentIds.TycoonArmor, EquipmentIds.SpeculatorBlade, EquipmentIds.Reaper })
            {
                var def = EquipmentDatabase.GetEquipment(id);
                if (def != null) fortuneChoices.Add(def);
            }

            ShowEquipmentChoice(
                fortuneChoices,
                Localization.Get("initial_event.title"),
                Localization.Get("mainflow.ie21_reward.desc"),
                Localization.Get("ui.skip"),
                equipmentId =>
                {
                    if (!string.IsNullOrEmpty(equipmentId))
                        GameManager.AddEquipment(equipmentId);
                    ShowMap();
                });
            return;
        }

        // 初始事件⑥：稀有装备三选一
        if (GameManager.InitialEventIe06RarePending)
        {
            GameManager.InitialEventIe06RarePending = false;
            ShowEquipmentChoice(
                CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Rare }, 3),
                Localization.Get("initial_event.title"),
                Localization.Get("mainflow.ie06_reward.desc"),
                Localization.Get("ui.skip"),
                equipmentId =>
                {
                    if (!string.IsNullOrEmpty(equipmentId))
                        GameManager.AddEquipment(equipmentId);
                    ShowMap();
                },
                rarities: new[] { EquipmentRarity.Rare });
            return;
        }

        GameManager.SetRunState(RunState.Map);
        var map = _mapScene.Instantiate<MapController>();
        map.NodeSelected += OnNodeSelected;
        map.ReturnToMenuRequested += RequestSuspendRunToMainMenu;
        map.InventoryRequested += ShowInventory;
        SwitchTo(map);

        // 自由探索：整局仅首次进入时弹一次提示，在地图屏幕已经显示之后再弹出。
        if (ContinueExploreManager.ShouldShowTip)
        {
            ContinueExploreManager.ConsumeTip();
            ShowContinueExploreTipDialog();
        }
    }

    private void ShowContinueExploreTipDialog()
    {
        var dialog = new AcceptDialog
        {
            Title = Localization.Get("map.continueexplore.tip.title"),
            DialogText = Localization.Get("map.continueexplore.tip.body")
        };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    private void ShowEquipmentReforgeResult(string message)
    {
        var dialog = new AcceptDialog
        {
            Title = Localization.Get("equipment.reforge.result_title"),
            DialogText = message
        };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    private void ShowInventory()
    {
        var inventory = _inventoryScene.Instantiate<InventoryController>();
        inventory.ReturnToMapRequested += ShowMap;
        SwitchTo(inventory);
    }

    private void ShowInitialEvent()
    {
        var view = _initialEventScene.Instantiate<InitialEventController>();
        view.InitialEventCompleted += OnInitialEventCompleted;
        SwitchTo(view);
    }

    private void OnInitialEventCompleted()
    {
        if (FactionFateManager.TryConsumeExtraInitialEventChoice())
        {
            ShowInitialEvent();
            return;
        }

        if (GameManager.InitialEventAttackTrickChoicePending)
        {
            GameManager.InitialEventAttackTrickChoicePending = false;
            var choices = new CardChoiceProvider
            {
                Count = 3,
                Randomize = false,
                IncludeCharacterExclusive = true,
                CardPool = new[] { CardType.ArrowBarrage, CardType.NanmanInvasion, CardType.IronChain }
            }.CreateChoices();
            ShowChoicePanel(new ChoiceRequest
            {
                Title = Localization.Get("initial_event.ie_22.title"),
                Description = Localization.Get("initial_event.ie_22.choice_desc"),
                Footer = Localization.Get("mainflow.choice_footer"),
                AllowCancel = false,
                Options = choices
            }, result =>
            {
                if (!result.Cancelled && TryGetInitialEventAttackTrick(result, out var cardType))
                    GameManager.TryAddInitialEventAttackTrick(cardType);
                ShowMap();
            });
            return;
        }

        ShowMap();
    }

    /// <summary>
    /// 初始事件㉒的卡牌选择兼容 ChoicePanel 的强类型 Payload 与基于 Id 的恢复路径。
    /// 这样即使界面在场景切换/存档恢复后只保留了选项 Id，所选攻击锦囊也仍会写入本局出牌栏。
    /// </summary>
    private static bool TryGetInitialEventAttackTrick(ChoiceResult result, out CardType cardType)
    {
        if (result.Payload is CardType payloadType
            && payloadType is CardType.ArrowBarrage or CardType.NanmanInvasion or CardType.IronChain)
        {
            cardType = payloadType;
            return true;
        }

        if (System.Enum.TryParse<CardType>(result.Id, out var parsedType)
            && parsedType is CardType.ArrowBarrage or CardType.NanmanInvasion or CardType.IronChain)
        {
            cardType = parsedType;
            return true;
        }

        cardType = default;
        return false;
    }

    private void OnNodeSelected(string nodeId)
    {
        var node = GameManager.GetNode(nodeId);
        if (node == null)
        {
            ShowMap();
            return;
        }

        // MapNodeVisual disables completed nodes, but its selection signal is
        // deferred to avoid freeing a SubViewport during input picking.  Recheck
        // here as the authoritative flow boundary so a stale click can never
        // replay a completed battle and incorrectly end the run on a later loss.
        if (GameManager.IsNodeCleared(nodeId))
        {
            GD.Print($"[MainFlow] Ignored selection of completed map node: {nodeId}");
            ShowMap();
            return;
        }

        // 整合式教程的两个固定节点：完全独立的窄分支，不进入下面任何一段正式
        // 章节逻辑（真实随机遭遇/真实事件候选池选取），只借用真实的电量扣除。
        if (IntegratedTutorialFlow.IsActive)
        {
            if (nodeId == IntegratedTutorialFlow.TutorialBattleNodeId)
            {
                GameManager.PowerBeforeLastNode = GameManager.Power;
                if (!GameManager.TrySpendPower(GameManager.GetNodeEnergyCost(node)))
                {
                    ShowIntegratedTutorialMap();
                    return;
                }
                GameManager.SetCurrentNode(nodeId);
                ShowIntegratedTutorialBattle();
                return;
            }

            if (nodeId == IntegratedTutorialFlow.TutorialEventNodeId)
            {
                GameManager.PowerBeforeLastNode = GameManager.Power;
                if (!GameManager.TrySpendPower(GameManager.GetNodeEnergyCost(node)))
                {
                    ShowIntegratedTutorialMap();
                    return;
                }
                GameManager.SetCurrentNode(nodeId);
                IntegratedTutorialFlow.AdvanceTo(IntegratedTutorialStage.Event);
                var tutorialEventView = _eventScene.Instantiate<EventController>();
                tutorialEventView.NodeId = nodeId;
                tutorialEventView.EventClosed += OnIntegratedTutorialEventClosed;
                SwitchTo(tutorialEventView);
                return;
            }
        }

        // 初始抉择节点：特殊处理，不走普通事件系统
        if (nodeId == "initial_event")
        {
            GameManager.SetCurrentNode(nodeId);
            ShowInitialEvent();
            return;
        }

        // 电量：进入节点时实时扣除（地图层已经用 CanAffordPower 挡住了买不起的节点，
        // 这里的 TrySpendPower 只是最后一道防线；Boss/初始抉择等固定0电量的节点
        // 直接返回true，不受影响）。扣费前先记录扣费前的电量，供地图HUD返回时对比、
        // 播放增/减脉冲动画（地图屏幕本身展示期间电量不会变化，只能靠前后对比触发）。
        // 消耗必须走 GameManager.GetNodeEnergyCost 这一个正式接口——和地图预览、
        // 可负担性检查完全同源，不允许在这里另算一遍。
        // 必须在修改 CurrentNode/CurrentStage 之前计算且只计算一次。部分事件条件会读取
        // 当前阶段；若先切换状态再重新估价，FixedEventId 可能失效并被换成另一品质事件，
        // 造成地图显示20但实际按15放行。
        var entryPowerCost = GameManager.GetNodeEnergyCost(node);
        GameManager.PowerBeforeLastNode = GameManager.Power;
        if (!GameManager.TrySpendPower(entryPowerCost))
        {
            ShowMap();
            return;
        }

        GameManager.SetCurrentNode(nodeId);

        if (node.Type is MapNodeType.Battle or MapNodeType.Elite or MapNodeType.Boss)
        {
            if (node.Type == MapNodeType.Elite)
            {
                FirstTimeHintManager.TryShow(FirstTimeHintManager.HintIds.FirstElite, this);
            }

            // 品质跃迁从“已选择”进入“监听正式奖励”的唯一生命周期边界。
            // 放在扣费成功之后，避免仅点击节点或扣费失败也被当成正式进入1-1。
            GameManager.ArmInitialEventUpgradeFirstEquip(StageDatabase.GetStageIdForNode(node));
            var battle = _battleScene.Instantiate<BattleManager>();
            battle.BattleFinished += OnBattleFinished;
            battle.SuspendRunRequested += RequestSuspendRunToMainMenu;
            battle.ReturnToMainMenuRequested += ShowMainMenu;
            battle.GoToMapRequested += ShowMap;
            SwitchTo(battle);
            return;
        }

        // 直接商店节点（MapNodeType.Shop）：跳过 EventController，直接进入商店；
        // 退出时标记节点完成并解锁下一节点。
        if (node.Type == MapNodeType.Shop)
        {
            ShowShopFromMapNode();
            return;
        }

        var eventView = _eventScene.Instantiate<EventController>();
        eventView.NodeId = nodeId;
        eventView.EventClosed += ShowMap;
        eventView.ShopRequested += ShowShop;
        eventView.VehicleShopRequested += ShowVehicleShop;
        eventView.WitchShopRequested += ShowWitchShop;
        eventView.BlackMarketShopRequested += ShowBlackMarketShop;
        eventView.SpecialBattleRequested += ShowSpecialBattle;
        eventView.EpicEquipmentChoiceRequested += ShowEpicEquipmentChoice;
        eventView.LegendaryEquipmentChoiceRequested += ShowLegendaryEquipmentChoice;
        eventView.LegendaryWeaponChoiceRequested += ShowLegendaryWeaponChoice;
        eventView.RareOrEpicEquipmentChoiceRequested += ShowRareOrEpicEquipmentChoice;
        eventView.ChipDoubleChoiceRequested += ShowChipDoubleChoice;
        eventView.ChipChoiceRequested += ShowSingleChipChoice;
        eventView.SkillChoiceRequested += ShowEventSkillChoice;
        eventView.SacrificeEquipmentForUpgradeRequested += ShowSacrificeEquipmentForUpgrade;
        eventView.ElfElementChoiceRequested += ShowElfElementChoice;
        eventView.ElfEquipmentReforgeRequested += ShowElfEquipmentReforge;
        eventView.ModificationShopWeaponReforgeRequested += ShowModificationShopWeaponReforge;
        SwitchTo(eventView);
    }

    private void ShowSpecialBattle()
    {
        var battle = _battleScene.Instantiate<BattleManager>();
        battle.BattleFinished += OnBattleFinished;
        battle.SuspendRunRequested += RequestSuspendRunToMainMenu;
        battle.ReturnToMainMenuRequested += ShowMainMenu;
        battle.GoToMapRequested += ShowMap;
        SwitchTo(battle);
    }

    // 从地图商店节点进入：退出时标记节点完成。
    private void ShowShopFromMapNode()
    {
        var shop = _shopScene.Instantiate<ShopController>();
        shop.ExitShopRequested += OnMapShopExited;
        AttachShopChipChoice(shop);
        SwitchTo(shop);
    }

    private void OnMapShopExited()
    {
        GameManager.MarkCurrentNodeCleared();
        ShowMap();
    }

    // 从事件触发进入商店（EventController.ShopRequested）：节点完成已由事件系统处理。
    private void ShowShop()
    {
        var shop = _shopScene.Instantiate<ShopController>();
        shop.ExitShopRequested += ShowMap;
        AttachShopChipChoice(shop);
        SwitchTo(shop);
    }

    // 从事件触发进入载具商店（EventController.VehicleShopRequested）。
    private void ShowVehicleShop()
    {
        var shop = _shopScene.Instantiate<ShopController>();
        shop.ShopType = ShopType.Vehicle;
        shop.ExitShopRequested += ShowMap;
        AttachShopChipChoice(shop);
        SwitchTo(shop);
        FirstTimeHintManager.TryShow(FirstTimeHintManager.HintIds.FirstVehicleShop, this);
    }

    private void ShowWitchShop()
    {
        var shop = _shopScene.Instantiate<ShopController>();
        shop.ShopType = ShopType.Witch;
        shop.ExitShopRequested += ShowMap;
        AttachShopChipChoice(shop);
        SwitchTo(shop);
    }

    // 从事件触发进入黑市（EventController.BlackMarketShopRequested，酒馆"黑市交易"）。
    private void ShowBlackMarketShop()
    {
        var shop = _shopScene.Instantiate<ShopController>();
        shop.ShopType = ShopType.BlackMarket;
        shop.ExitShopRequested += ShowMap;
        AttachShopChipChoice(shop);
        SwitchTo(shop);
    }

    private void OnBattleFinished(bool playerWon)
    {
        if (_transitioning)
        {
            return;
        }

        GD.Print($"[MainFlow] OnBattleFinished received. playerWon={playerWon}, stage={GameManager.CurrentStage}, node={GameManager.CurrentNodeId}");
        _transitioning = true;

        if (GameManager.ActiveSpecialBattleId == "treasure_pavilion")
        {
            HandleTreasurePavilionBattleFinished(playerWon);
            return;
        }

        if (GameManager.ActiveSpecialBattleId == "manzu_camp")
        {
            HandleManZuCampBattleFinished(playerWon);
            return;
        }

        if (GameManager.ActiveSpecialBattleId == "chasing_pursuers")
        {
            HandleChasingPursuersBattleFinished(playerWon);
            return;
        }

        if (GameManager.ActiveSpecialBattleId == "qixingtan")
        {
            HandleQiXingTanBattleFinished(playerWon);
            return;
        }

        if (GameManager.ActiveSpecialBattleId == "rat_king_event")
        {
            HandleRatKingEventBattleFinished(playerWon);
            return;
        }

        if (playerWon)
        {
            // Map progression is earned by winning the battle, not by the visual
            // reward screen completing its deferred callback.  Keeping this at
            // the battle-flow boundary prevents a claimed/reopened reward panel
            // from leaving the player on the already-cleared node.
            GameManager.MarkCurrentStageCleared();
            ShowReward();
            return;
        }

        var canContinue = GameManager.HandleBattleLoss();
        if (canContinue)
        {
            ShowMap();
            return;
        }

        GameManager.EndRun();
        ShowMainMenu();
    }

    private void HandleTreasurePavilionBattleFinished(bool playerWon)
    {
        // 特殊战斗不走普通关卡奖励池；清掉战斗系统按敌人Reward写入的临时掉落，避免泄漏到下一场普通奖励。
        _ = GameManager.ConsumePendingBattleGold();
        _ = GameManager.ConsumePendingBattleDrops();

        if (playerWon)
        {
            ShowEpicEquipmentChoice(0);
            return;
        }

        var canContinue = GameManager.HandleTreasurePavilionBattleLoss();
        _transitioning = false;
        if (canContinue)
        {
            ShowMap();
            return;
        }

        GameManager.EndRun();
        ShowMainMenu();
    }

    private void HandleManZuCampBattleFinished(bool playerWon)
    {
        _ = GameManager.ConsumePendingBattleGold();
        _ = GameManager.ConsumePendingBattleDrops();

        GameManager.CompleteSpecialBattle();

        if (playerWon)
        {
            GameManager.AddEquipment(EquipmentIds.BarbarianTooth);
            FinishSpecialBattleVictory();
            return;
        }

        var canContinue = GameManager.HandleBattleLoss();
        _transitioning = false;
        if (canContinue)
        {
            ShowMap();
            return;
        }

        GameManager.EndRun();
        ShowMainMenu();
    }

    private void HandleChasingPursuersBattleFinished(bool playerWon)
    {
        _ = GameManager.ConsumePendingBattleGold();
        _ = GameManager.ConsumePendingBattleDrops();

        GameManager.CompleteSpecialBattle();

        if (playerWon)
        {
            // 【追兵】①"杀出去"胜利：保留【遗忘之石】/【雕像核心】（若拥有），获得100金币。
            GameManager.AddGold(100);
            FinishSpecialBattleVictory();
            return;
        }

        var canContinue = GameManager.HandleBattleLoss();
        _transitioning = false;
        if (canContinue)
        {
            ShowMap();
            return;
        }

        GameManager.EndRun();
        ShowMainMenu();
    }

    private void HandleQiXingTanBattleFinished(bool playerWon)
    {
        _ = GameManager.ConsumePendingBattleGold();
        _ = GameManager.ConsumePendingBattleDrops();

        GameManager.CompleteSpecialBattle();

        if (playerWon)
        {
            GameManager.GrantQiXingTanVictoryReward();
            RunBuffManager.ConsumeBattleVictory();
            _transitioning = false;
            ShowMap();
            return;
        }

        var canContinue = GameManager.HandleBattleLoss();
        _transitioning = false;
        if (canContinue)
        {
            ShowMap();
            return;
        }

        GameManager.EndRun();
        ShowMainMenu();
    }

    private void HandleRatKingEventBattleFinished(bool playerWon)
    {
        _ = GameManager.ConsumePendingBattleGold();
        _ = GameManager.ConsumePendingBattleDrops();
        GameManager.CompleteSpecialBattle();

        if (playerWon)
        {
            if (GameManager.RatKingEventPeacefulResolved)
            {
                // 和平解决：3桃触发，获得瘟疫权杖，不给金币和史诗装备
                GameManager.AddEquipment(EquipmentIds.PlagueStaff);
                FinishSpecialBattleVictory();
            }
            else
            {
                // 正常击败：400金 + 随机史诗装备×1
                GameManager.AddGold(400);
                var epic = GameManager.CreateRandomEpicEquipmentChoices(1);
                if (epic.Count > 0) GameManager.AddEquipment(epic[0].Id);
                FinishSpecialBattleVictory();
            }
            return;
        }

        var canContinue = GameManager.HandleBattleLoss();
        _transitioning = false;
        if (canContinue)
        {
            ShowMap();
            return;
        }

        GameManager.EndRun();
        ShowMainMenu();
    }

    private void FinishSpecialBattleVictory()
    {
        GameManager.RecoverHealthAfterBattle();
        RunBuffManager.ConsumeBattleVictory();
        _transitioning = false;
        ShowMap();
    }

    private void ShowReward()
    {
        var node = GameManager.GetNode(GameManager.CurrentNodeId);
        if (node == null)
        {
            GameManager.RecoverHealthAfterBattle();
            GameManager.MarkCurrentStageCleared();
            ShowMap();
            return;
        }

        var reward = RewardManager.GetRewardForNode(node);

        // 仙丹术士“问路/解惑”的延迟装备奖励在此处结算（每个节点仅结算一次），随后随同常规奖励一并展示与发放，
        // 不在界面上暴露内部记录状态，玩家只会看到“装备 +xxx”这类正常奖励文本。
        if (!GameManager.HasClaimedReward(node.Id))
        {
            // 初始事件⑪只作用于第一章真正的主Boss：常规金币与装备奖励改为一枚扩容芯片。
            // 由其它事件承诺、延迟到Boss结算时发放的装备不属于Boss基础奖励，仍需保留。
            var isInitialEventExpansionReplacement = node.Type == MapNodeType.Boss
                && !node.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal)
                && GameManager.CurrentChapter == 1
                && GameManager.InitialEventCh1BossExpansionChip;

            // 吴·百工夺赏：仅真正的主Boss触发；它是额外掉落，不替换普通金币、装备、
            // 扩容芯片或章节特殊奖励。随机装备在本方法加入 RewardData，以便领取界面可见。
            var isMainBossNode = node.Type == MapNodeType.Boss && !node.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal);
            var wuBossBonusActive = isMainBossNode
                && FactionFateManager.CurrentFateId == FactionFateIds.WuBossRewardReplace
                && GameManager.CurrentChapter is 1 or 2 or 3;
            if (isInitialEventExpansionReplacement)
            {
                reward.Gold = 0;
                reward.EquipmentIds.Clear();
                reward.EquipmentNames.Clear();
            }
            if (node.Type == MapNodeType.Boss)
            {
                // 魏·双线征伐 的额外Boss节点（Id 形如 "boss_extra_ch1"）不算"本章主Boss被击败"，
                // 只有真正的主Boss节点才应该置位这个跨系统的章节进度标记（供 EventSystem 等处的
                // "本章Boss是否已击败"判定使用）。不排除的话，玩家如果先打额外Boss、后打主Boss，
                // 会导致"本章Boss已击败"提前误判为 true。
                if (!node.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal))
                {
                    GameManager.MarkChapterBossDefeated(GameManager.CurrentChapter);
                }

                // 始终消费这些"待发放"队列，避免残留到下一次结算。
                // 这里的装备来自其它事件（例如仙丹术士"解惑"）承诺的延迟奖励，不属于百工夺赏
                // 所以必须始终发放，不能因其它 Boss 奖励规则而丢失。
                var bossEquipment = GameManager.ConsumePendingChapterBossEquipmentReward(GameManager.CurrentChapter);
                if (bossEquipment != null)
                {
                    reward.EquipmentIds.Add(bossEquipment.Id);
                    reward.EquipmentNames.Add(bossEquipment.Name);
                }

                // 第二章Boss章节特殊奖励：固定获得【扩容芯片】，不进入随机芯片池，
                // 直接体现在本次奖励结算里（走既有的 EquipmentIds→AddEquipment 流程），不额外弹窗。
                if (GameManager.CurrentChapter == 2)
                {
                    var expansionChipDef = EquipmentDatabase.GetEquipment(EquipmentIds.ExpansionChip);
                    reward.EquipmentIds.Add(EquipmentIds.ExpansionChip);
                    reward.EquipmentNames.Add(expansionChipDef is null ? EquipmentIds.ExpansionChip : Localization.GetName(expansionChipDef));
                }
            }

            var bonusEquipment = GameManager.ConsumePendingNextBattleEquipmentReward();
            if (bonusEquipment != null)
            {
                reward.EquipmentIds.Add(bonusEquipment.Id);
                reward.EquipmentNames.Add(bonusEquipment.Name);
            }

            // 战斗金币：来自各敌方按 EnemyRewardConfig 统一区间随机之和（BattleManager 胜利时写入）；存在时覆盖 RewardManager 的基础值。
            var pendingBattleGold = GameManager.ConsumePendingBattleGold();
            if (pendingBattleGold > 0 && !isInitialEventExpansionReplacement)
            {
                reward.Gold = pendingBattleGold;
            }

            // 概率掉落装备：已在 BattleManager 完成随机；在此直接追加进奖励列表。
            var pendingDropIds = GameManager.ConsumePendingBattleDrops();
            foreach (var dropId in pendingDropIds)
            {
                var dropDef = EquipmentDatabase.GetEquipment(dropId);
                if (dropDef != null && !isInitialEventExpansionReplacement)
                {
                    reward.EquipmentIds.Add(dropDef.Id);
                    reward.EquipmentNames.Add(dropDef.Name);
                }
            }

            if (isInitialEventExpansionReplacement)
            {
                var expansionChipDef = EquipmentDatabase.GetEquipment(EquipmentIds.ExpansionChip);
                reward.EquipmentIds.Add(EquipmentIds.ExpansionChip);
                reward.EquipmentNames.Add(expansionChipDef is null
                    ? EquipmentIds.ExpansionChip
                    : Localization.GetName(expansionChipDef));
            }

            if (wuBossBonusActive)
            {
                var bonus = FactionFateManager.GetBossRewardBonus(GameManager.CurrentChapter);
                if (bonus != null)
                {
                    reward.EquipmentIds.Add(bonus.Id);
                    reward.EquipmentNames.Add(Localization.GetName(bonus));
                    GD.Print($"[WuBossRewardBonus] 第{GameManager.CurrentChapter}章主Boss额外掉落【{Localization.GetName(bonus)}】。");
                }
            }
        }

        _pendingRewardData = reward;

        var rewardView = new RewardController
        {
            TitleText = Localization.Get("battle.victory.title"),
            Reward = reward,
            RewardAlreadyClaimed = GameManager.HasClaimedReward(node.Id)
        };
        rewardView.RewardClaimed += OnRewardClaimed;
        SwitchTo(rewardView);
    }

    private void OnRewardClaimed()
    {
        var node = GameManager.GetNode(GameManager.CurrentNodeId);
        var isFirstClaim = node != null && !GameManager.HasClaimedReward(node.Id);
        var isBossNode = node != null && node.Type == MapNodeType.Boss;
        var isChapterOneBoss = isBossNode
            && !node!.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal)
            && GameManager.CurrentChapter == 1;
        // 按章节分发 Boss 章节特殊奖励：第一章→芯片三选一，第三章→史诗装备三选一。
        // 第二章的【扩容芯片】已经在 ShowReward() 里随常规奖励一起结算，这里不需要再处理。
        var shouldShowChapterOneBossChipChoice =
            isChapterOneBoss && isFirstClaim;
        var shouldShowChapterThreeBossEpicChoice =
            isBossNode && GameManager.CurrentChapter == 3 && isFirstClaim;

        if (node != null && isFirstClaim)
        {
            var equipmentSource = isBossNode
                ? EquipmentGainSource.BossReward
                : EquipmentGainSource.BattleReward;
            var result = RewardManager.ApplyReward(
                _pendingRewardData ?? RewardManager.GetRewardForNode(node),
                equipmentSource);
            if (!result.Succeeded)
            {
                GD.PushError(
                    $"[Reward] 节点 {node.Id} 的装备奖励未能加入背包：{string.Join(", ", result.FailedEquipmentIds)}");
                _transitioning = false;
                return;
            }
            GameManager.MarkRewardClaimed(node.Id);
        }

        _pendingRewardData = null;

        // 初始事件④：每场战斗胜利后+N最大HP
        if (GameManager.InitialEventBattleEndMaxHpGain > 0)
        {
            GameManager.AddMaxHp(GameManager.InitialEventBattleEndMaxHpGain);
        }

        GameManager.RecoverHealthAfterBattle();
        GameManager.CheckInitialEventNoDamageBattleEnd();
        RunBuffManager.ConsumeBattleVictory();
        GameManager.MarkCurrentStageCleared();
        _transitioning = false;

        // 初始事件③：击败第一章Boss后随机获得当前Boss拥有的技能；如果Boss的技能已经
        // 全部拥有（GetRandomSkillIdFromCurrentBoss 返回null），改为随机获得一个稀有技能，
        // 避免这个效果在技能收集到一定程度后直接失效、什么都不给。
        if (isChapterOneBoss && GameManager.InitialEventCh1BossSkillPick)
        {
            GameManager.InitialEventCh1BossSkillPick = false;
            var bossSkillId = GameManager.GetRandomSkillIdFromCurrentBoss() ?? GameManager.GetRandomRareSkillId();
            if (bossSkillId != null)
            {
                GameManager.AddAcquiredSkill(bossSkillId);
            }
        }

        if (isChapterOneBoss && isFirstClaim && FactionFateManager.TryGrantShuMuNiuLiuMaAfterFirstBoss())
        {
            GD.Print("[ShuMuNiuLiuMa] 第一章主Boss奖励追加【木牛流马】。");
        }

        if (shouldShowChapterOneBossChipChoice)
        {
            ShowChipChoice(
                () =>
                {
                    _transitioning = false;
                    AfterBossChapterComplete(node);
                },
                Localization.Get("mainflow.boss_reward.ch1.title"),
                Localization.Get("mainflow.boss_reward.ch1.desc"));
            return;
        }

        if (shouldShowChapterThreeBossEpicChoice)
        {
            ShowEquipmentChoice(
                CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Epic }, 3),
                Localization.Get("mainflow.boss_reward.ch3.title"),
                Localization.Get("mainflow.boss_reward.ch3.desc"),
                Localization.Get("ui.continue"),
                equipmentId =>
                {
                    if (!string.IsNullOrEmpty(equipmentId))
                    {
                        GameManager.AddEquipment(equipmentId);
                    }

                    AfterBossChapterComplete(node);
                },
                rarities: new[] { EquipmentRarity.Epic });
            return;
        }

        AfterBossChapterComplete(node);
    }

    private void AfterBossChapterComplete(MapNode? node)
    {
        if (node != null && node.Type == MapNodeType.Boss && GameManager.AllStagesCleared())
        {
            ShowChapterComplete();
            return;
        }

        ShowMap();
    }

    private void ShowChapterComplete()
    {
        if (GameManager.IsFinalChapter)
        {
            var victoryView = new ChapterCompleteController
            {
                TitleText = Localization.Get("run.victory.title"),
                SubtitleText = string.Format(Localization.Get("chapter.clear_fmt"), GameManager.GetChapterDisplayName(GameManager.CurrentChapter)),
                PrimaryButtonText = Localization.Get("ui.main_menu")
            };
            victoryView.PrimaryAction += OnReturnToMenuFromVictory;
            SwitchTo(victoryView);
            return;
        }

        var nextChapter = GameManager.CurrentChapter + 1;
        var chapterCompleteView = new ChapterCompleteController
        {
            TitleText = string.Format(Localization.Get("chapter.clear_fmt"), GameManager.GetChapterDisplayName(GameManager.CurrentChapter)),
            PrimaryButtonText = string.Format(Localization.Get("chapter.enter_fmt"), GameManager.GetChapterDisplayName(nextChapter))
        };
        chapterCompleteView.PrimaryAction += OnAdvanceChapterRequested;
        SwitchTo(chapterCompleteView);
    }

    private async void OnAdvanceChapterRequested()
    {
        GameManager.AdvanceToNextChapter();
        await PlayPendingFactionDiceAnimation();

        // 初始事件①：进入第二章时技能三选一
        if (GameManager.InitialEventCh2SkillPick)
        {
            GameManager.InitialEventCh2SkillPick = false;
            ShowInitialEventSkillPick();
            return;
        }

        ShowMap();
    }

    private void ShowInitialEventSkillPick()
    {
        var choices = new SkillChoiceProvider
        {
            Count = 3,
            Rarities = new[] { SkillRarity.Common, SkillRarity.Rare },
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = true
        }.CreateChoices();

        if (choices.Count == 0)
        {
            ShowMap();
            return;
        }

        ShowChoicePanel(
            new ChoiceRequest
            {
                Title = Localization.Get("mainflow.forbidden_protocol.title"),
                Description = Localization.Get("mainflow.forbidden_protocol.desc"),
                Footer = Localization.Get("mainflow.choice_footer"),
                AllowCancel = false,
                Options = choices,
                RerollOption = FactionFateManager.CurrentFateId == FactionFateIds.Reroll
                    ? (current, allIds) =>
                    {
                        FactionFateManager.TryRerollChoiceOption(
                            current,
                            allIds,
                            exclude => new SkillChoiceProvider
                            {
                                Count = 1,
                                Rarities = new[] { SkillRarity.Common, SkillRarity.Rare },
                                IncludeBossSkills = false,
                                IncludeOtherCharacterExclusiveSkills = true,
                                ExcludedSkillIds = exclude
                            }.CreateChoices().FirstOrDefault(),
                            out var replacement);
                        return replacement;
                    }
                    : null
            },
            result =>
            {
                if (!result.Cancelled && !string.IsNullOrWhiteSpace(result.Id))
                {
                    GameManager.AddAcquiredSkill(result.Id);
                }

                ShowMap();
            });
    }

    private void OnReturnToMenuFromVictory()
    {
        GameManager.EndRun();
        ShowMainMenu();
    }

    private void ShowEpicEquipmentChoice(int bonusGold)
    {
        ShowEquipmentChoice(
            CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Epic }, 3),
            Localization.Get("mainflow.treasury.epic.title"),
            bonusGold > 0 ? string.Format(Localization.Get("mainflow.treasury.bonus_gold_fmt"), bonusGold) : Localization.Get("mainflow.treasury.title"),
            Localization.Get("ui.back_to_map"),
            equipmentId =>
            {
                if (!string.IsNullOrEmpty(equipmentId))
                {
                    GameManager.AddEquipment(equipmentId);
                }

                if (bonusGold > 0)
                {
                    GameManager.AddGold(bonusGold);
                }

        if (GameManager.HasActiveSpecialBattle)
        {
            GameManager.RecoverHealthAfterBattle();
            RunBuffManager.ConsumeBattleVictory();
        }

                GameManager.CompleteSpecialBattle();
                _transitioning = false;
                ShowMap();
            },
            rarities: new[] { EquipmentRarity.Epic });
    }

    private void ShowLegendaryEquipmentChoice()
    {
        ShowEquipmentChoice(
            CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Legendary }, 3),
            Localization.Get("mainflow.treasury.legendary.title"),
            Localization.Get("mainflow.forge.fusion.title"),
            Localization.Get("ui.back_to_map"),
            equipmentId =>
            {
                if (!string.IsNullOrEmpty(equipmentId))
                    GameManager.AddEquipment(equipmentId);
                _transitioning = false;
                ShowMap();
            },
            rarities: new[] { EquipmentRarity.Legendary });
    }

    private void ShowLegendaryWeaponChoice()
    {
        ShowEquipmentChoice(
            CreateEquipmentDefinitionChoices(
                new[] { EquipmentRarity.Legendary },
                3,
                new[] { EquipmentSlotCategory.Weapon }),
            Localization.Get("mainflow.treasury.legendary.title"),
            Localization.Get("event.forbidden_library.option5.desc"),
            Localization.Get("ui.back_to_map"),
            equipmentId =>
            {
                if (!string.IsNullOrEmpty(equipmentId))
                    GameManager.AddEquipment(equipmentId);
                _transitioning = false;
                ShowMap();
            },
            rarities: new[] { EquipmentRarity.Legendary },
            slotCategories: new[] { EquipmentSlotCategory.Weapon });
    }

    private void ShowRareOrEpicEquipmentChoice()
    {
        ShowEquipmentChoice(
            CreateEquipmentDefinitionChoices(new[] { EquipmentRarity.Rare, EquipmentRarity.Epic }, 3),
            Localization.Get("mainflow.treasury.any.title"),
            Localization.Get("mainflow.forge.treasure.title"),
            Localization.Get("ui.back_to_map"),
            equipmentId =>
            {
                if (!string.IsNullOrEmpty(equipmentId))
                    GameManager.AddEquipment(equipmentId);
                _transitioning = false;
                ShowMap();
            },
            rarities: new[] { EquipmentRarity.Rare, EquipmentRarity.Epic });
    }

    private void ShowSacrificeEquipmentForUpgrade()
    {
        ShowInventoryEquipmentChoice(
            new InventoryChoiceProvider
            {
                Scope = InventoryChoiceScope.All,
                RequireUpgradeCandidate = true
            }.CreateChoices(),
            Localization.Get("mainflow.forge.recast.title"),
            Localization.Get("mainflow.forge.recast.desc"),
            Localization.Get("ui.back_to_map"),
            result =>
            {
                string? reforgeMessage = null;
                if (!result.Cancelled && result.Payload is OwnedEquipment target)
                {
                    var original = target.Definition;
                    var candidates = RewardManager.GetEquipmentUpgradeCandidates(original);
                    if (candidates.Count == 0)
                    {
                        reforgeMessage = Localization.Get("equipment.reforge.no_candidate");
                    }
                    else
                    {
                        var replacement = candidates[System.Random.Shared.Next(candidates.Count)];
                        reforgeMessage = InventoryManager.ReforgeWithoutSelling(target.InstanceId, replacement)
                            ? Localization.GetFmt(
                                "equipment.reforge.success_fmt",
                                Localization.GetName(original),
                                Localization.GetName(replacement))
                            : Localization.Get("equipment.reforge.add_failed");
                    }
                }

                _transitioning = false;
                ShowMap();
                if (!string.IsNullOrWhiteSpace(reforgeMessage))
                {
                    ShowEquipmentReforgeResult(reforgeMessage);
                }
            },
            allowCancel: false);
    }

    private void ShowElfElementChoice()
    {
        ShowChoicePanel(
            new ChoiceRequest
            {
                Title = Localization.Get("mainflow.element.title"),
                Description = Localization.Get("mainflow.element.desc"),
                Footer = Localization.Get("mainflow.element.footer"),
                AllowCancel = false,
                Options = new ElementChoiceProvider().CreateChoices()
            },
            result =>
            {
                if (!result.Cancelled && result.Payload is string buffId)
                {
                    RunBuffManager.AddStacks(buffId, 1);
                }

                _transitioning = false;
                ShowMap();
            });
    }

    private void ShowElfEquipmentReforge()
    {
        ShowInventoryEquipmentChoice(
            new InventoryChoiceProvider
            {
                Scope = InventoryChoiceScope.All
            }.CreateChoices(),
            Localization.Get("mainflow.sprite_coerce.title"),
            Localization.Get("mainflow.sprite_coerce.desc"),
            Localization.Get("ui.back_to_map"),
            result =>
            {
                string? reforgeMessage = null;
                if (!result.Cancelled && result.Payload is OwnedEquipment item)
                {
                    reforgeMessage = InventoryManager.ReforgeEquipment(item.InstanceId).Message;
                }

                _transitioning = false;
                ShowMap();
                if (!string.IsNullOrWhiteSpace(reforgeMessage))
                {
                    ShowEquipmentReforgeResult(reforgeMessage);
                }
            });
    }

    private void ShowModificationShopWeaponReforge()
    {
        var choices = new InventoryChoiceProvider
        {
            Scope = InventoryChoiceScope.All,
            SlotCategories = new[] { EquipmentSlotCategory.Weapon },
            Count = 3,
            Randomize = true,
            RequireReforgeCandidate = true
        }.CreateChoices();

        ShowChoicePanel(
            new ChoiceRequest
            {
                Title = Localization.Get("event.modification_shop.reforge.title"),
                Description = Localization.Get("event.modification_shop.reforge.desc"),
                Footer = Localization.Get("mainflow.choice_footer"),
                EmptyText = Localization.Get("event.modification_shop.no_weapon"),
                AllowCancel = false,
                Options = choices
            },
            result =>
            {
                string? reforgeMessage = null;
                if (result.Payload is OwnedEquipment item)
                {
                    reforgeMessage = InventoryManager.ReforgeEquipment(item.InstanceId).Message;
                }

                _transitioning = false;
                ShowMap();
                if (!string.IsNullOrWhiteSpace(reforgeMessage))
                {
                    ShowEquipmentReforgeResult(reforgeMessage);
                }
            });
    }

    private void ShowChipDoubleChoice()
    {
        ShowChipChoice(() => ShowChipChoice(() =>
        {
            _transitioning = false;
            ShowMap();
        }));
    }

    private void ShowSingleChipChoice()
    {
        ShowChipChoice(() =>
        {
            _transitioning = false;
            ShowMap();
        });
    }

    /// <summary>事件专用技能二选一；只响应事件的选择请求，不影响其它技能获取来源。</summary>
    private void ShowEventSkillChoice()
    {
        var choices = new SkillChoiceProvider
        {
            Count = 2,
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = true,
            ExcludeOwnedSkills = true
        }.CreateChoices();

        if (choices.Count == 0)
        {
            _transitioning = false;
            ShowMap();
            return;
        }

        ShowChoicePanel(
            new ChoiceRequest
            {
                Title = Localization.Get("mainflow.event_skill_choice.title"),
                Description = Localization.Get("mainflow.event_skill_choice.desc"),
                Footer = Localization.Get("mainflow.choice_footer"),
                AllowCancel = false,
                Options = choices
            },
            result =>
            {
                if (!result.Cancelled && !string.IsNullOrWhiteSpace(result.Id))
                {
                    GameManager.AddAcquiredSkill(result.Id);
                }

                _transitioning = false;
                ShowMap();
            });
    }

    private void AttachShopChipChoice(ShopController shop)
    {
        shop.ChipChoicePurchaseRequested += () => ShowShopChipChoice(shop);
    }

    /// <summary>
    /// 商店购买芯片后，把统一 ChoicePanel 叠加在现有商店上方，而不是切换场景。
    /// 这样商品栏、刷新次数与当前商店状态都能原样保留。
    /// </summary>
    private void ShowShopChipChoice(ShopController shop)
    {
        var panel = new ChoicePanel();
        var handled = false;
        panel.ChoiceCompleted += result =>
        {
            if (handled)
            {
                return;
            }

            handled = true;
            if (!result.Cancelled && result.Payload is ChipChoiceType chipType)
            {
                ApplyChipType(chipType);
            }

            shop.RemoveChild(panel);
            panel.QueueFree();
            shop.RefreshAfterExternalChoice();
        };
        panel.Configure(CreateChipChoiceRequest(
            Localization.Get("shop.chip_choice.title"),
            Localization.Get("shop.chip_choice.desc")));
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        shop.AddChild(panel);
    }

    private void ShowChipChoice(System.Action onComplete, string? title = null, string? description = null)
    {
        ShowChoicePanel(
            CreateChipChoiceRequest(title, description),
            result =>
            {
                if (!result.Cancelled && result.Payload is ChipChoiceType chipType)
                {
                    ApplyChipType(chipType);
                }

                onComplete();
            });
    }

    private static ChoiceRequest CreateChipChoiceRequest(string? title = null, string? description = null)
    {
        var baseExpansionChance = GameManager.InitialEventChipChoiceEnhanced
            ? GameManager.InitialEventEnhancedChipExpansionChance
            : ChipChoiceProvider.ExpansionReplacementChanceDefault;
        var baseSkillChipChance = GameManager.InitialEventChipChoiceEnhanced
            ? GameManager.InitialEventEnhancedSkillChipChance
            : ChipChoiceProvider.SkillChipReplacementChanceDefault;
        var choices = new ChipChoiceProvider
        {
            Count = 3,
            ChipTypes = new[]
            {
                ChipChoiceType.Attack,
                ChipChoiceType.Knowledge,
                ChipChoiceType.Defense
            },
            IncludeMysteriousChip = GameManager.InitialEventChipChoiceEnhanced,
            MysteriousChipReplacementChance = GameManager.InitialEventChipChoiceEnhanced
                ? GameManager.InitialEventMysteriousChipChance
                : 0,
            ExpansionReplacementChance = FactionFateManager.GetChipSpecialReplacementChance(baseExpansionChance),
            SkillChipReplacementChance = FactionFateManager.GetChipSpecialReplacementChance(baseSkillChipChance),
            ForcedBaseChipType = FactionFateManager.QunForcedChipType
        }.CreateChoices();

        return new ChoiceRequest
        {
            Title = title ?? Localization.Get("mainflow.chip.title"),
            Description = description ?? Localization.Get("mainflow.chip.desc"),
            Footer = Localization.Get("mainflow.choice_footer"),
            AllowCancel = false,
            Options = choices
        };
    }

    private static void ApplyChipType(ChipChoiceType chipType)
    {
        switch (chipType)
        {
            case ChipChoiceType.Attack:
                GameManager.IncrementAttackChipCount();
                break;
            case ChipChoiceType.Defense:
                // HP 加成已经内置在 IncrementDefenseChipCount 里，不需要在这里重复调用 AddMaxHp/AddCurrentHp。
                GameManager.IncrementDefenseChipCount();
                break;
            case ChipChoiceType.Knowledge:
                GameManager.IncrementKnowledgeChipCount();
                break;
            case ChipChoiceType.Expansion:
                GameManager.IncrementExpansionChipCount();
                break;
            case ChipChoiceType.Mysterious:
                GameManager.AddEquipment(EquipmentIds.MysteriousChip, EquipmentGainSource.ChoiceReward);
                break;
            case ChipChoiceType.Skill:
                GameManager.AddEquipment(EquipmentIds.SkillChip, EquipmentGainSource.ChoiceReward);
                break;
        }
    }

    /// <summary>
    /// 阵营命运效果抽取演出：角色选择确认、进入地图之前播放一次全屏"老虎机"揭示动画，
    /// 展示本局（整局固定，选完角色那一刻就已经由 GameManager.SelectCharacter() 内部的
    /// FactionFateManager.RollFateIfEligible() 决定好）生效的阵营命运。本方法只读取
    /// FactionFateManager.CurrentFateId，绝不重新随机，动画只是纯展示。
    /// </summary>
    private async Task PlayFactionFateRevealAnimation()
    {
        var fate = FactionFateDatabase.Get(FactionFateManager.CurrentFateId);
        if (fate == null)
        {
            return;
        }

        var (factionLabel, factionColor, factionBadgeText) = GetFactionDisplayInfo(fate.Faction);
        var rollCandidateNames = new List<string>();
        foreach (var candidate in FactionFateDatabase.AllFates)
        {
            rollCandidateNames.Add(Localization.Get(candidate.NameKey));
        }

        // 命运揭示属于屏幕级演出，必须脱离地图 SceneHost 的布局坐标。
        // CanvasLayer 让它始终以当前视口为参照，不会被地图缩放、容器尺寸或相机位置带偏。
        var presentationLayer = new CanvasLayer
        {
            Name = "FactionFateRevealLayer",
            Layer = 100
        };
        AddChild(presentationLayer);

        var presentationRoot = new Control
        {
            Name = "FactionFateRevealPresentation",
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 0
        };
        presentationLayer.AddChild(presentationRoot);
        // 必须在加入场景树后应用 FullRect。此前在 AddChild 前计算布局，根控件没有
        // 有效父级矩形，导致内部 0.5 中心锚点实际以左上角的零尺寸区域为参照。
        presentationRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var overlay = new FactionFateRevealOverlay();
        presentationRoot.AddChild(overlay);
        try
        {
            await overlay.PlayAsync(
                rollCandidateNames,
                Localization.Get(fate.NameKey),
                Localization.Get(fate.DescriptionKey),
                factionLabel,
                factionColor,
                factionBadgeText,
                DeveloperDebugPanel.FastMode);
        }
        finally
        {
            presentationLayer.QueueFree();
        }
    }

    private static (string Label, Color Color, string BadgeText) GetFactionDisplayInfo(Faction faction)
    {
        return faction switch
        {
            Faction.Wei => (Localization.Get("char.faction.wei"), new Color(0.20f, 0.42f, 0.78f), "魏"),
            Faction.Shu => (Localization.Get("char.faction.shu"), new Color(0.18f, 0.62f, 0.32f), "蜀"),
            Faction.Wu => (Localization.Get("char.faction.wu"), new Color(0.80f, 0.24f, 0.20f), "吴"),
            Faction.Qun => (Localization.Get("char.faction.qun"), new Color(0.68f, 0.62f, 0.20f), "群"),
            _ => (string.Empty, new Color(0.5f, 0.5f, 0.5f), "?")
        };
    }

    private async Task PlayPendingFactionDiceAnimation()
    {
        if (!FactionFateManager.TryConsumePendingDicePresentation(out var request) || request == null)
        {
            return;
        }

        // DiceRollOverlay 自身拥有屏幕级 CanvasLayer。这里不再重复创建表现层，避免调用方
        // 的地图 SceneHost 尺寸或额外父级布局再次成为中心坐标基准。
        var overlay = new DiceRollOverlay();
        AddChild(overlay);

        var body = request.ResultMessage;
        if (!string.IsNullOrWhiteSpace(request.LegendaryMessage))
        {
            body += $"\n\n{request.LegendaryMessage}";
        }

        var title = string.Format(
            Localization.Get("factionfate.dice_chapter_title_fmt"),
            request.Chapter);
        try
        {
            await overlay.PlayAsync(
                string.Join(" / ", request.Rolls),
                true,
                title,
                body,
                title,
                body,
                DeveloperDebugPanel.FastMode,
                resultHoldSeconds: 2.0);
        }
        finally
        {
            overlay.QueueFree();
        }
    }

    private void ShowEquipmentChoice(
        List<EquipmentDefinition> choices,
        string titleText,
        string subtitleText,
        string emptyButtonText,
        System.Action<string?> onChosen,
        IReadOnlyCollection<EquipmentRarity>? rarities = null,
        IReadOnlyCollection<EquipmentSlotCategory>? slotCategories = null)
    {
        var options = new List<ChoiceOption>();
        foreach (var equipment in choices)
        {
            options.Add(EquipmentChoiceProvider.ToChoiceOption(equipment));
        }

        ShowChoicePanel(
            new ChoiceRequest
            {
                Title = titleText,
                Description = subtitleText,
                Footer = Localization.Get("mainflow.choice_footer"),
                EmptyText = Localization.Get("mainflow.no_equip_available"),
                AllowCancel = true,
                CancelText = emptyButtonText,
                Options = options,
                // 阵营命运·改命：装备三选一是需求原文明确列出的最低必做范围。
                // 只有命中"改命"这个命运、且调用方提供了 rarities（决定重抽池）时才附上委托，
                // 避免其它命运/无命运角色的面板，或未指定 rarities 的次要装备选择流程多出无效的刷新按钮。
                RerollOption = (FactionFateManager.CurrentFateId == FactionFateIds.Reroll && rarities != null)
                    ? (current, allIds) =>
                    {
                        FactionFateManager.TryRerollChoiceOption(
                            current,
                            allIds,
                            exclude => new EquipmentChoiceProvider
                            {
                                Count = 1,
                                Rarities = rarities,
                                SlotCategories = slotCategories,
                                ExcludedEquipmentIds = exclude
                            }.CreateChoices().FirstOrDefault(),
                            out var replacement);
                        return replacement;
                    }
                    : null
            },
            result =>
            {
                onChosen(result.Cancelled ? null : result.Id);
            });
    }

    private static List<EquipmentDefinition> CreateEquipmentDefinitionChoices(
        IReadOnlyCollection<EquipmentRarity> rarities,
        int count,
        IReadOnlyCollection<EquipmentSlotCategory>? slotCategories = null)
    {
        var options = new EquipmentChoiceProvider
        {
            Count = count,
            Rarities = rarities,
            SlotCategories = slotCategories
        }.CreateChoices();

        var result = new List<EquipmentDefinition>();
        foreach (var option in options)
        {
            if (option.Payload is EquipmentDefinition definition)
            {
                result.Add(definition);
            }
        }

        return result;
    }

    private void ShowInventoryEquipmentChoice(
        IReadOnlyList<ChoiceOption> choices,
        string titleText,
        string subtitleText,
        string emptyButtonText,
        System.Action<ChoiceResult> onChosen,
        bool allowCancel = true)
    {
        ShowChoicePanel(
            new ChoiceRequest
            {
                Title = titleText,
                Description = subtitleText,
                Footer = Localization.Get("mainflow.choice_footer"),
                EmptyText = Localization.Get("mainflow.no_equip_available"),
                AllowCancel = allowCancel,
                CancelText = emptyButtonText,
                Options = choices
            },
            onChosen);
    }

    private void ShowChoicePanel(ChoiceRequest request, System.Action<ChoiceResult> onCompleted)
    {
        var panel = new ChoicePanel();
        var handled = false;
        panel.ChoiceCompleted += result =>
        {
            if (handled)
            {
                return;
            }

            handled = true;
            onCompleted(result);
        };
        panel.Configure(request);
        SwitchTo(panel);
    }

    private void ShowRunResult(string title, string subtitle, System.Action onFinished)
    {
        // 图鉴：Run结束（战败/粮草耗尽）是明确要求的安全落盘节点之一。
        CodexService.SaveIfDirty();
        var result = _runResultScene.Instantiate<RunResultController>();
        result.TitleText = title;
        result.SubtitleText = subtitle;
        result.Finished += () => onFinished();
        SwitchTo(result);
    }

    private void SwitchTo(Control next)
    {
        if (_sceneHost == null)
        {
            return;
        }

        foreach (var child in _sceneHost.GetChildren())
        {
            // 浏览图鉴、教程选择等菜单页面时，不能释放暂存的战局。
            // 正式开始新游戏/教程会先清除暂存引用，再由这里释放旧场景。
            if (child == _suspendedRunScreen) continue;
            _sceneHost.RemoveChild(child);
            child.QueueFree();
        }

        next.SetAnchorsPreset(LayoutPreset.FullRect);
        _sceneHost.AddChild(next);
        _currentBattleManager = next as BattleManager;
    }

    private bool HasSuspendedRun()
    {
        return _suspendedRunScreen != null
            && IsInstanceValid(_suspendedRunScreen)
            && _suspendedRunScreen.GetParent() == _sceneHost;
    }

    private bool CanSuspendCurrentRun()
    {
        if (_sceneHost == null || HasSuspendedRun() || _suspendRunDialog != null)
        {
            return false;
        }

        if (GameManager.CurrentRunState is not (RunState.Map or RunState.Battle))
        {
            return false;
        }

        return _sceneHost.GetChildren().OfType<Control>().Any(screen => screen is MapController or BattleManager);
    }

    private void RequestSuspendRunToMainMenu()
    {
        if (!CanSuspendCurrentRun())
        {
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = Localization.Get("pause.return.title"),
            DialogText = Localization.Get("pause.return.body"),
            Exclusive = true,
            Unresizable = true
        };
        _suspendRunDialog = dialog;
        AddChild(dialog);
        dialog.GetOkButton().Text = Localization.Get("pause.return.confirm");
        dialog.GetCancelButton().Text = Localization.Get("pause.return.cancel");
        dialog.Confirmed += () =>
        {
            CloseSuspendDialog();
            SuspendCurrentRunAndShowMenu();
        };
        dialog.Canceled += CloseSuspendDialog;
        dialog.PopupCentered(new Vector2I(620, 260));
    }

    private void CloseSuspendDialog()
    {
        if (_suspendRunDialog == null)
        {
            return;
        }

        var dialog = _suspendRunDialog;
        _suspendRunDialog = null;
        if (IsInstanceValid(dialog))
        {
            dialog.QueueFree();
        }
    }

    private void SuspendCurrentRunAndShowMenu()
    {
        if (!CanSuspendCurrentRun() || _sceneHost == null)
        {
            return;
        }

        var activeScreen = _sceneHost.GetChildren().OfType<Control>()
            .FirstOrDefault(screen => screen is MapController or BattleManager);
        if (activeScreen == null)
        {
            return;
        }

        _suspendedRunScreen = activeScreen;
        activeScreen.Visible = false;
        activeScreen.ProcessMode = ProcessModeEnum.Disabled;
        _currentBattleManager = null;
        ShowMainMenu();
    }

    private void ResumeSuspendedRun()
    {
        if (!HasSuspendedRun() || _sceneHost == null || _suspendedRunScreen == null)
        {
            return;
        }

        foreach (var menu in _sceneHost.GetChildren().OfType<MainMenuController>().ToList())
        {
            _sceneHost.RemoveChild(menu);
            menu.QueueFree();
        }

        _suspendedRunScreen.ProcessMode = ProcessModeEnum.Inherit;
        _suspendedRunScreen.Visible = true;
        _currentBattleManager = _suspendedRunScreen as BattleManager;
        _suspendedRunScreen = null;
    }

    private void DiscardSuspendedRunReference()
    {
        // 新开局会通过 SwitchTo 统一释放旧界面；此处仅取消“继续游戏”的引用。
        _suspendedRunScreen = null;
    }
}
