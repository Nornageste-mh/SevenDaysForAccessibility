using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SevenDaysForAccessibility
{
    /// <summary>
    /// 《我与你共度的七日》读屏无障碍补丁 —— 逐作层入口。
    ///
    /// 平台层（Speech / Nvda / Zdsr / Sapi / UiNav / Repeat / Hotkey / KeyEdge / GlobalKeys /
    /// A11yHost）整份拷自 Framework，**一行都没有改**；这个文件负责三件事：
    ///   1) 注册配置项（分「朗读」「界面导航」「其它」三组，组名不要改）
    ///   2) 把配置与钩子填进 A11yHost（契约层）
    ///   3) 每帧驱动 Watcher / Choices / UiNav
    ///
    /// 路线：Mono / BepInEx 5。本作是 Unity 2021.3.31 + Mono，Assembly-CSharp.dll 在
    /// `*_Data\Managed`。它的 Mono BCL 被裁剪过，BepInEx 预加载器需要完整 BCL 才能起来
    /// （doorstop 的 dll_search_path_override 指向 BepInEx\mono-bcl，见 README「构建」）。
    ///
    /// ★ 架构决定：**本补丁不使用 Harmony 补丁**，全部靠每帧轮询游戏自己的公开状态。
    ///   依据是实机取证，不是偏好（详见 Watcher.cs 顶部注释）：
    ///   游戏真实安装路径带中文，在中文路径下 Harmony 对**部分**方法打补丁会抛
    ///   `ExecutionEngineException: String conversion error: Illegal byte sequence`，
    ///   探针实测 10 个目标只有 3 个能挂上；同一份补丁在 ASCII 路径（junction）下全部成功。
    ///   玩家装的游戏就在中文路径下，所以发布版必须不依赖补丁。
    ///
    ///   功能 → 轮询源（反编译实查，sevendays_work\decompiled\，结论见 P0_recon_report.md）
    ///   ---------------------------  ----------------------------------------------
    ///   台词/旁白朗读                 UIManager.LastBacklog()（= Backlog.GetLastData()）。
    ///                                 三条文本路径（OnSubtitlesRequest / FadeTextAction /
    ///                                 AddBacklogTask）全部汇进 Backlog.AddLog，所以
    ///                                 「最后一条变了」= 新的一句出现
    ///   选项朗读 + 数字键选择          DialogueUGUI.optionsGroup 的子物体（顺序=显示顺序），
    ///                                 数字键触发该按钮自己的 onClick（走游戏 Finalize）
    ///   面板开合播报                   SettingController.IsOpen / Config.IsOpen /
    ///                                 Backlog.IsOpen / SharedVariable.SaveLoadOpen /
    ///                                 YesNoDialogue.IsOpen
    ///   存读档槽朗读                   DataCheck（UiNav 标签钩子读 index/HasData/日期/正文）
    ///   历史回看                       Backlog.Open() + BacklogItem（条数反射读 DataList）
    ///   菜单键盘导航                   UiNav 每帧扫描 Selectable
    ///   开机健康提示页                  场景 StartScene + 对象 ConvertedCanvas/Warning
    ///   热键：历史回看 / 存 / 读档      UIManager.OpenBacklog() / OpenSave() / OpenLoad()
    ///
    /// 铁律 1：不许猜方法名和对象名。只能来自 ilspycmd 反编译实查，或探针场景树转储。
    /// </summary>
    [BepInPlugin(Guid, "我与你共度的七日 A11y Reader", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sevendays.a11y.reader";

        // 版本号必须是**纯数字四段**（0.1.0.0）：BepInPlugin 的字符串重载只接受
        // System.Version，`0.1.0a` 会静默跳过插件；而且必须与 csproj 的 <Version> 一致
        // （build.ps1 第三道守卫会查）。
        public const string Version = "0.1.0.0";

        // 统一日志出口。
        internal static ManualLogSource L;

        private bool _updateErrorLogged;
        private bool _hinted;

        // ==================== 配置项 ====================
        //
        // 组名与项名照抄姊妹作（流水线 §6 P2）：玩家在几个补丁之间切换时不用重新学。
        // 中文说明里**写实测数字** —— 那是本补丁的可信度来源。

        // ---- 朗读 ----
        internal static ConfigEntry<bool> CfgSpeakName;
        internal static ConfigEntry<bool> CfgReadChoices;
        internal static ConfigEntry<bool> CfgChoiceHotkeys;
        internal static ConfigEntry<string> CfgSilenceKey;
        internal static ConfigEntry<string> CfgRepeatKey;
        /// <summary>运行中切换语音后端的按键（支持 Alt+Shift+F9 这类写法）。</summary>
        internal static ConfigEntry<string> CfgBackendKey;
        internal static ConfigEntry<string> CfgSpeechBackend;
        internal static ConfigEntry<bool> CfgAnnounceOnSkipStop;

        // ---- 界面导航 ----
        internal static ConfigEntry<bool> CfgMenuNav;
        internal static ConfigEntry<bool> CfgSortByPosition;
        internal static ConfigEntry<bool> CfgVisibleOnly;
        internal static ConfigEntry<bool> CfgQuitConfirm;
        internal static ConfigEntry<string> CfgQuitNames;
        internal static ConfigEntry<string> CfgExcludeNames;

        // ---- 其它 ----
        internal static ConfigEntry<bool> CfgStartupHint;
        internal static ConfigEntry<bool> CfgDiagLog;
        internal static ConfigEntry<string> CfgKeyBacklog;
        internal static ConfigEntry<string> CfgKeySave;
        internal static ConfigEntry<string> CfgKeyLoad;

        private void Awake()
        {
            L = Logger;
            Init();
        }

        private void Update()
        {
            TickOnce();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>公共初始化。顺序不能改：先有日志出口，再注册配置，再填契约。</summary>
        private void Init()
        {
            // ================================================================
            // 1) 先填日志出口 —— 后面每一步失败都要能记下来
            // ================================================================
            A11yHost.Log = L;

            // ================================================================
            // 2) 注册配置项（三组组名固定：「朗读」「界面导航」「其它」）
            // ================================================================
            CfgSpeakName = Config.Bind("朗读", "朗读时带上说话人", true,
                "在台词前加上说话人名字（读作「名字，台词」）。旁白没有名字\n" +
                "（BacklogData.Name 为空），不会加前缀。");

            CfgReadChoices = Config.Bind("朗读", "朗读选项", true,
                "出现选项时，把全部选项一次念完并编号。本作选项文本约 113 条\n" +
                "（本地化表 key 分类统计：编号场景选项 65 + event 选项 21 + 章节选项 15\n" +
                "+ Day 选项 10 + 单点 2）。");

            CfgChoiceHotkeys = Config.Bind("朗读", "数字键选择选项", true,
                "用数字键 1-9 选择对应编号的选项。选中后触发该选项按钮自己的 onClick，\n" +
                "由游戏自己的选择逻辑收尾（不另起选择调用，所以不可能和游戏对不上）。");

            CfgSilenceKey = Config.Bind("朗读", "沉默按键", "0",
                "只在**限时选择**里生效：立刻选择最后一项，也就是游戏自己倒计时到点时会选的那一项\n" +
                "（反编译实查 DialogueUGUI.CountDown：超时 = Finalize(info, info.options.Values.Last())），\n" +
                "同一条剧情路，不扩大可达范围。非限时选择里这个键无动作。留空则关闭。");

            CfgRepeatKey = Config.Bind("朗读", "重读按键", "Backspace",
                "重新朗读当前这一句的按键（有配音的行也可以补读）。填 KeyCode 名称。\n" +
                "提醒：不要填游戏原生占用的键（A 自动、S 快进、Esc 菜单、右键返回、左键推进）。");

            CfgSpeechBackend = Config.Bind("朗读", "语音后端", "自动",
                "用哪个后端朗读。默认「自动」按 Tolk → NVDA → 争渡 → SAPI 挑第一个可用的。\n" +
                "可以填：自动 / Tolk / NVDA / 争渡 / SAPI。\n" +
                "钉死的后端不可用时**不会**回退 —— 这是刻意的，用于排查。");

            CfgBackendKey = Config.Bind("朗读", "切换语音后端按键", "Alt+Shift+F9",
                "运行中切换语音后端的按键。可写修饰键组合，例如 F9、Alt+Shift+F9、Ctrl+Alt+F9。\n" +
                "按一下切到下一个「当前可用」的后端（自动 → Tolk → NVDA → 争渡 → SAPI → 自动），\n" +
                "探测不到的会被跳过并在播报里说明；选择会写进本配置文件，下次启动仍生效。\n" +
                "留空则关闭这个功能键。默认避开游戏占用的键（F9 是读档界面，所以用组合键）。");
            CfgAnnounceOnSkipStop = Config.Bind("朗读", "快进停止时补念当前句", true,
                "快进（游戏里按 S 或按住 Ctrl）停下来的时候，把停在的那一句补念出来。\n" +
                "快进期间本补丁不朗读（否则会刷屏），所以停下来那一刻屏幕上是什么，\n" +
                "读屏用户完全不知道 —— 这一项就是补这个缺口。");

            CfgMenuNav = Config.Bind("界面导航", "菜单键盘导航", true,
                "让标题菜单 / 存读档 / 设置 / 历史回看可以用键盘操作并被朗读。\n" +
                "对话进行中不进入导航（否则回车会既激活控件又推进剧情）；对话里用下面的热键或 Esc。\n" +
                "  Tab              进入 / 退出导航模式\n" +
                "  上 / 下          上一项 / 下一项\n" +
                "  左 / 右          调整滑条、切换开关\n" +
                "  回车 / 空格      激活（按钮点击、开关切换、输入框聚焦）\n" +
                "  Home / End       第一项 / 最后一项\n" +
                "  PageUp/PageDown  切换面板组");

            CfgSortByPosition = Config.Bind("界面导航", "按屏幕位置排序控件", true,
                "按控件在屏幕上的位置排序（先上后下、同一行先左后右），让「第几项」和画面对得上。\n" +
                "关掉则改回按渲染层级（兄弟节点序号）排序。");

            CfgVisibleOnly = Config.Bind("界面导航", "只导航看得见的控件", true,
                "只把画面上真正能看到、能点到的控件纳入导航。\n" +
                "只有在发现正常按钮被误排除时才需要关掉。");

            CfgQuitConfirm = Config.Bind("界面导航", "退出前二次确认", true,
                "在导航模式下激活下面列出的控件会先朗读一次确认，再按一次才真的执行；\n" +
                "按方向键即取消。");

            CfgQuitNames = Config.Bind("界面导航", "退出确认对象名", "Exit",
                "哪些控件需要二次确认，按 Unity 里的对象名**全等**匹配（不是正则）。\n" +
                "名字来自 probe 的运行时场景树转储：标题页「退出游戏」按钮的对象名是 Exit\n" +
                "（ConvertedCanvas/GameUI/State1/Fade1/Buttons/Exit）。\n" +
                "多个名字用逗号分隔；留空则关闭。");

            CfgExcludeNames = Config.Bind("界面导航", "排除的对象名", "",
                "这些对象**及其整棵子树**里的控件不纳入导航（按祖先链匹配）。\n" +
                "用途：把画面上的装饰按钮从导航里摘掉。名字来自 probe 的场景树转储。\n" +
                "多个名字用逗号分隔；留空则关闭。");

            CfgStartupHint = Config.Bind("其它", "启动时播报", true,
                "游戏启动后朗读一句加载提示 + 常用按键。用来确认读屏通路是通的。");

            CfgDiagLog = Config.Bind("其它", "界面诊断日志", false,
                "把每一句被朗读的文本、导航模式扫到的控件写进 LogOutput.log。\n" +
                "排查完请关掉。注意：日志含台词原文，**不要公开**（IP 红线，见 README）。");

            CfgKeyBacklog = Config.Bind("其它", "历史回看按键", "H",
                "打开历史回看的按键。游戏原生只有「鼠标滚轮上滑」能开，键盘没有入口，所以补一个。\n" +
                "填 KeyCode 名称；留空则关闭。");

            CfgKeySave = Config.Bind("其它", "存档界面按键", "F5",
                "打开存档界面的按键（等于 Esc 菜单里点「存档」）。填 KeyCode 名称；留空则关闭。");

            CfgKeyLoad = Config.Bind("其它", "读档界面按键", "F9",
                "打开读档界面的按键（等于 Esc 菜单里点「读档」）。填 KeyCode 名称；留空则关闭。");

            // ================================================================
            // 3) 填契约层 —— 平台层唯一看得见的东西
            //    漏填不会崩，只会静默失效，所以最后一定要 Validate()
            // ================================================================
            A11yHost.CfgSpeechBackend = CfgSpeechBackend;
            A11yHost.CfgDiagLog = CfgDiagLog;
            A11yHost.CfgVisibleOnly = CfgVisibleOnly;
            A11yHost.CfgSortByPosition = CfgSortByPosition;
            A11yHost.CfgExcludeNames = CfgExcludeNames;
            A11yHost.CfgQuitConfirm = CfgQuitConfirm;
            A11yHost.CfgQuitNames = CfgQuitNames;

            // 发布版没有补丁，这两个钩子不会被游戏侧调用；仍然填上，因为 L1 的
            // UiNav 自己也会读它们（退出确认、激活前判断）。Validate() 把
            // BlockGameAdvance 算作必填槽位。
            A11yHost.BlockGameAdvance = () => UiNav.BlockGameAdvance;
            A11yHost.ShouldMuteUnitySubmit = () => UiNav.ShouldMuteUnitySubmit;

            // 逐作层的读屏文案钩子（UiNav.Game.cs 里实现）。
            // 不填也能跑，但标签/分组会退化成对象名与「面板 N」。
            A11yHost.GameLabelOf = UiNav.GameLabelOf;
            A11yHost.GameGroupName = UiNav.GameGroupName;
            A11yHost.GameGroupPriority = UiNav.GameGroupPriority;
            A11yHost.GameTextIsNoise = UiNav.GameTextIsNoise;

            // 每帧入口：轮询 → 选项按键 → 界面导航。
            A11yHost.TickUiNav = () =>
            {
                Watcher.Tick();
                Choices.Update();
                if (CfgMenuNav != null && CfgMenuNav.Value) NavTick();
            };

            A11yHost.Validate();

            // ================================================================
            // 3.5) 静态构造兜底
            //      UiNav 的 NameAlias 是 OrdinalIgnoreCase 字典：别名表里只要有
            //      两个「只差大小写」的键，首次触碰 UiNav 就抛 TypeInitializationException，
            //      真正的 ArgumentException 在 InnerException 里 —— 外层异常不出现在日志，
            //      表现为「导航 100% 失效且零报错」。这里主动跑一次静态构造把原因记下来。
            // ================================================================
            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(UiNav).TypeHandle);
            }
            catch (TypeInitializationException e)
            {
                L.LogError("[a11y] UiNav 静态初始化失败，界面导航会全部失效。InnerException: "
                           + e.InnerException);
            }
            catch (Exception e)
            {
                L.LogError("[a11y] UiNav 静态初始化异常: " + e);
            }

            // ================================================================
            // 4) 初始化语音后端
            // ================================================================
            try
            {
                Speech.Init(A11yHost.Log);
                if (Speech.Current == Speech.Backend.None)
                    L.LogWarning("语音不可用: " + Speech.LastError);
                else
                    L.LogInfo("语音后端就绪: " + Speech.BackendName);
            }
            catch (Exception e)
            {
                L.LogWarning("语音初始化异常: " + e.Message);
            }

            L.LogInfo("[a11y] loaded v" + Version + " (Mono, BepInEx 5, 轮询式，无 Harmony)");
        }

        /// <summary>
        /// 界面导航每帧入口。
        ///
        /// 对话进行中（且没有面板打开）**不跑导航**：没有 Harmony 就拦不住游戏自己的
        /// 「回车推进剧情」，一旦导航也吃回车，按一次回车会既激活控件又翻页。
        /// 对话里改用游戏原生键（空格推进、Esc 菜单）与我们的热键（历史回看/存/读档）。
        /// </summary>
        private static void NavTick()
        {
            try
            {
                var ui = UIManager.Instance;
                // 历史回看有自己的浏览模式（条目不是 Selectable），此时不要跑 UiNav
                if (ui != null && ui.backlog != null && ui.backlog.IsOpen)
                {
                    if (UiNav.Active) UiNav.Toggle();
                    return;
                }
                bool inDialogue = ui != null
                                  && ui.Status == UIManager.UiStatus.MessageWindow
                                  && !AnyPanelOpen(ui);
                if (inDialogue)
                {
                    if (UiNav.Active) UiNav.Toggle();   // 进入对话自动退出导航
                    return;
                }
                UiNav.Update();
            }
            catch (Exception e)
            {
                if (A11yHost.Log != null) A11yHost.Log.LogError("[a11y] 导航异常: " + e.Message);
            }
        }

        /// <summary>是否存在任何打开的界面面板（自己判空，不用 UIManager.HasUiOpen —— 它不判 Config 空）。</summary>
        internal static bool AnyPanelOpen(UIManager ui)
        {
            if (ui == null) return false;
            return (ui.SettingController != null && ui.SettingController.IsOpen)
                   || (ui.Config != null && ui.Config.IsOpen)
                   || (ui.backlog != null && ui.backlog.IsOpen)
                   || (ui.sharedVariable != null && ui.sharedVariable.SaveLoadOpen)
                   || (ui.yesNoDialogue != null && ui.yesNoDialogue.IsOpen);
        }

        /// <summary>每帧入口（Mono 由引擎调）。</summary>
        private void TickOnce()
        {
            try
            {
                if (A11yHost.TickUiNav != null) A11yHost.TickUiNav();

                // 重读键：L1 的 Repeat 是唯一出口（lint_repeat.ps1 拦截直接 Speech.Speak）
                if (CfgRepeatKey != null)
                {
                    KeyCode rk = KeyEdge.Parse(CfgRepeatKey.Value);
                    if (rk != KeyCode.None && KeyEdge.Pressed(rk)) Repeat.Again();
                }

                // 运行中切换语音后端：平台层出结论文本，这里经 Repeat 念出来
                // （L1 唯一的朗读出口；也是 Framework 的约定 —— 平台层自己发声会绕过重读缓冲）。
                if (CfgBackendKey != null && !string.IsNullOrEmpty(CfgBackendKey.Value)
                    && Hotkey.Down(CfgBackendKey.Value))
                {
                    string say = Speech.CycleAvailable();
                    if (!string.IsNullOrEmpty(say)) Repeat.Say(say, true);
                }

                // 快进停止时补念最后一句（快进期间行流只记不念，见 Watcher.Lines）
                Watcher.SkipEdge();

                // 启动提示（等 30 帧，避开加载抖动）
                if (!_hinted && CfgStartupHint != null && CfgStartupHint.Value && Time.frameCount > 30)
                {
                    _hinted = true;
                    Repeat.Say("《我与你共度的七日》无障碍补丁已加载。空格推进对话，退格键重读，"
                               + "H 键历史回看，Esc 打开菜单，选项按数字键。"
                               + "Alt 加 Shift 加 F9 切换语音后端。", true);
                }
            }
            catch (Exception e)
            {
                // 每帧的异常不能刷屏：只记一次
                if (!_updateErrorLogged)
                {
                    _updateErrorLogged = true;
                    L.LogError("[Plugin] Update 异常: " + e);
                }
            }
        }

        /// <summary>卸载：Mono 在 OnDestroy 调。每一步都要单独 try。</summary>
        private void Shutdown()
        {
            try { Speech.Shutdown(); } catch { }
            try { A11yHost.Clear(); } catch { }
        }
    }
}
