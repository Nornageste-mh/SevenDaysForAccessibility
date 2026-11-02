using System;
using System.Collections.Generic;
using System.Reflection;
using GameProject;
using LoveStoryProject;
using NodeCanvas.DialogueTrees.UI.Examples;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SevenDaysForAccessibility
{
    /// <summary>
    /// 轮询式逐作层：所有「游戏里发生了什么」都靠每帧读游戏自己的公开状态，不依赖 Harmony 补丁。
    ///
    /// 为什么不用补丁（这是本作最关键的实现决定，依据是实机取证，不是偏好）：
    ///   · 本作真实安装路径带中文（Steam 库在 `...\我与你共度的七日\game`），而本作 Mono
    ///     运行时的 BCL 是裁剪过的，注入完整 BCL 后 BepInEx 5 才能起来；在中文路径下，
    ///     Harmony 对**部分**方法打补丁会抛
    ///     `System.ExecutionEngineException: String conversion error: Illegal byte sequence`，
    ///     探针实测 10 个目标里只有 3 个能挂上（Backlog.AddLog / DataController.LoadData(int) /
    ///     DialogueUGUI.OnLoadSaveData），其余 7 个全失败，且同一个补丁在 ASCII 路径（junction）
    ///     下反而全部成功 —— 说明失败与安装路径编码有关，不能靠改代码消除。
    ///   · 用户装的游戏就在中文路径下，所以**发布版必须不依赖补丁**。
    ///   取证：sevendays_work\P0_recon_report.md、probe 的 [patch-test] 日志。
    ///
    /// 轮询源（全部是反编译实查的公开成员）：
    ///   UIManager.Instance        单例：Status / backlog / saveLoad / yesNoDialogue /
    ///                             sharedVariable / SettingController / Config / LastBacklog()
    ///   LoveStoryProject.Backlog  IsOpen、GetLastData()、AddLog 是全部台词/旁白/任务文本的
    ///                             唯一汇合点（OnSubtitlesRequest、FadeTextAction、AddBacklogTask
    ///                             三条路最后都进 Backlog.AddLog）
    ///   DialogueUGUI              在 Assembly-CSharp 内（NodeCanvas.DialogueTrees.UI.Examples）：
    ///                             optionsGroup（选项按钮的父节点）、skipMode、autoMode
    /// </summary>
    internal static class Watcher
    {
        // ---- 台词 ----
        private static BacklogData _lastLine;

        // ---- 对话界面 ----
        private static DialogueUGUI _dlg;
        private static float _dlgRetryAt;
        private static bool _skipWas;

        // ---- 历史回看专用浏览（条目本身不是 Selectable，UiNav 扫不到，只能自己做） ----
        private static int _blIndex = -1;
        private static string _skipLine;

        // ---- 面板开合（上一帧状态）----
        private static bool _pMenu, _pCfg, _pBacklog, _pSaveLoad, _pYesNo;
        private static string _lastYesNo = "";
        private static int _lastSavePage = -1;
        private static bool _lastIsSave;

        // ---- 场景 / 开机健康提示页 ----
        private static string _sceneName = "";
        private static bool _warnAnnounced;

        // ---- 热键（只解析一次）----
        private static bool _keysReady;
        private static KeyCode _keyBacklog = KeyCode.None;
        private static KeyCode _keySave = KeyCode.None;
        private static KeyCode _keyLoad = KeyCode.None;

        /// <summary>历史回看条数（Backlog.DataList 是私有自动属性，只能反射取）。</summary>
        private static PropertyInfo _piBacklogList;
        private static PropertyInfo _piDataIsSave;
        private static FieldInfo _fiDataIsSave;
        private static bool _fiDataIsSaveChecked;
        private static bool _piDataIsSaveChecked;
        private static FieldInfo _fiSaveItems;

        internal static bool InDialogue
        {
            get
            {
                var ui = UIManager.Instance;
                return ui != null && ui.Status == UIManager.UiStatus.MessageWindow;
            }
        }

        internal static void Tick()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;

            try { RefreshDialogue(ui); } catch (Exception e) { LogOnce("RefreshDialogue", e); }
            try { Lines(ui); } catch (Exception e) { LogOnce("Lines", e); }
            try { ChoicesTick(ui); } catch (Exception e) { LogOnce("Choices", e); }
            try { Panels(ui); } catch (Exception e) { LogOnce("Panels", e); }
            try { BacklogKeys(ui); } catch (Exception e) { LogOnce("BacklogKeys", e); }
            try { Hotkeys(ui); } catch (Exception e) { LogOnce("Hotkeys", e); }
            try { SceneBits(ui); } catch (Exception e) { LogOnce("SceneBits", e); }
        }

        private static void LogOnce(string where, Exception e)
        {
            // 每帧路径按模块 try/catch；同一条异常只记一次，避免把日志刷爆（G18）。
            if (A11yHost.Log != null)
            {
                string key = where + "|" + e.GetType().Name;
                if (_logged.Add(key)) A11yHost.Log.LogError("[watcher] " + where + " 失败: " + e);
            }
        }

        private static readonly HashSet<string> _logged = new HashSet<string>();

        // ------------------------------------------------------------------ 对话界面缓存
        /// <summary>
        /// UIManager.dialogueUGUI 每次访问都会 FindObjectOfType，绝不能每帧调；
        /// 这里自己缓存，失效（切场景/对象销毁）时重找，并且最多每 2 秒重试一次。
        /// </summary>
        private static void RefreshDialogue(UIManager ui)
        {
            if (_dlg != null) return;
            if (Time.unscaledTime < _dlgRetryAt) return;
            _dlgRetryAt = Time.unscaledTime + 2f;
            _dlg = UnityEngine.Object.FindObjectOfType<DialogueUGUI>();
        }

        // ------------------------------------------------------------------ 台词朗读
        /// <summary>
        /// 台词漏斗。Backlog.AddLog 是三条文本路径（OnSubtitlesRequest / FadeTextAction /
        /// AddBacklogTask）的唯一汇合点，所以「Backlog 里最后一条变了」就等于「新的一句出现」。
        /// 用引用比较判断是不是新对象：BacklogData 每句都是新实例。
        /// </summary>
        private static void Lines(UIManager ui)
        {
            if (ui.backlog == null) return;
            BacklogData data = ui.LastBacklog();
            if (data == null)
            {
                if (_lastLine != null) _lastLine = null;
                return;
            }
            if (ReferenceEquals(data, _lastLine)) return;
            _lastLine = data;

            string text = TextProc.Clean(data.Text);
            if (!TextProc.ShouldRead(text)) return;

            string name = TextProc.Speaker(data.Name);
            bool voiced = !string.IsNullOrEmpty(data.VoiceFileName);

            bool withName = Plugin.CfgSpeakName != null && Plugin.CfgSpeakName.Value;
            string line = (withName && name.Length > 0) ? name + "，" + text : text;


            // 快进（S / Ctrl）期间不朗读，只记下来；停止时补念最后一句。
            // 为什么用「记下这一句」而不是停止时调 Repeat.Again()：Again 念的是缓冲区
            // 最后一条，而同一帧里新的一句也可能已经进来并被朗读 —— 两个出口会撞车，
            // 实机表现是停快进时同一句念 2-3 遍。这里只留一个出口（_skipLine）。
            bool skipping = IsSkipping(ui);
            if (skipping)
            {
                Repeat.Note(line);
                _skipLine = line;
                return;
            }
            _skipLine = null;

            // 有配音的行：只记不念。语音已经在播，再让 TTS 念一遍就是叠音
            // （Framework 的 Repeat 规则：有配音的行不自动朗读，但必须进重读缓冲区，
            // 否则读屏用户永远听不到这句台词 —— 按重读键可以补读）。
            if (voiced)
            {
                Repeat.Note(line);
                return;
            }
            Repeat.Say(line, true);
        }

        // ------------------------------------------------------------------ 选项
        /// <summary>
        /// 选项在 DialogueUGUI.OnMultipleChoiceRequest 里 Instantiate 到 optionsGroup 下，
        /// 显示顺序=子物体顺序（反编译实查 DialogueUGUI.cs:681-700）。没有补丁后拿不到
        /// info.options 的 Value，也不需要：数字键直接触发按钮自己的 onClick，
        /// 让游戏走它自己的 Finalize 路径（自带防双选、自带 index 映射）。
        /// </summary>
        private static void ChoicesTick(UIManager ui)
        {
            RectTransform group = _dlg != null ? _dlg.optionsGroup : null;
            bool shown = group != null && group.gameObject.activeInHierarchy && group.childCount > 0;

            if (!shown)
            {
                if (Choices.HasContent) Choices.Clear();
                return;
            }

            if (Choices.HasContent && Choices.IsSameSet(group)) return;
            if (Choices.Suppressed) return;   // 刚选完，等淡出结束
            Choices.Capture(group);
        }

        // ------------------------------------------------------------------ 面板开合播报
        private static void Panels(UIManager ui)
        {
            bool menu = ui.SettingController != null && ui.SettingController.IsOpen;
            bool cfg = ui.Config != null && ui.Config.IsOpen;
            bool backlog = ui.backlog != null && ui.backlog.IsOpen;
            bool saveload = ui.sharedVariable != null && ui.sharedVariable.SaveLoadOpen;
            bool yesno = ui.yesNoDialogue != null && ui.yesNoDialogue.IsOpen;

            if (menu != _pMenu)
            {
                _pMenu = menu;
                Say(menu ? "菜单已打开。" : "菜单已关闭。");
            }
            if (cfg != _pCfg)
            {
                _pCfg = cfg;
                Say(cfg ? "游戏设置已打开。" : "游戏设置已关闭。");
            }
            if (backlog != _pBacklog)
            {
                _pBacklog = backlog;
                if (backlog)
                {
                    int n = BacklogCount(ui.backlog);
                    _blIndex = n > 0 ? n - 1 : -1;
                    Say(n > 0 ? "历史回看已打开，共 " + n + " 条。上下方向键逐条朗读，回车播放该条语音。"
                              : "历史回看已打开，还没有记录。");
                }
                else
                {
                    _blIndex = -1;
                    Say("历史回看已关闭。");
                }
            }
            if (saveload != _pSaveLoad)
            {
                _pSaveLoad = saveload;
                if (saveload)
                {
                    Say(SaveLoadIntro(ui));
                    _lastSavePage = SavePage(ui);   // 开场播报里已经含页码，别再补一遍
                }
                else
                {
                    _lastSavePage = -1;
                    Say("已关闭存读档界面。");
                }
            }
            if (saveload && _lastSavePage != SavePage(ui))
            {
                _lastSavePage = SavePage(ui);
                Say("第 " + (_lastSavePage + 1) + " 页，共 " + SaveMaxPage(ui) + " 页。");
            }
            if (yesno)
            {
                string msg = YesNoText(ui);
                if (msg != _lastYesNo)
                {
                    _lastYesNo = msg;
                    Say(msg.Length > 0 ? "确认对话框，" + msg : "确认对话框。");
                }
            }
            else if (!yesno && _lastYesNo.Length > 0)
            {
                _lastYesNo = "";
                Say("确认对话框已关闭。");
            }
        }

        private static string SaveLoadIntro(UIManager ui)
        {
            bool isSave;
            try { isSave = IsSaveMode(ui.saveLoad); } catch { isSave = false; }
            _lastIsSave = isSave;
            int max = SaveMaxPage(ui);
            string head = isSave ? "存档界面已打开。" : "读档界面已打开。";
            string hint = isSave ? "用 Tab 选存档槽，回车保存。" : "用 Tab 选存档槽，回车读取。";
            return head + "第 1 页，共 " + max + " 页。" + hint;
        }

        private static int SavePage(UIManager ui)
        {
            try { return ui.saveLoad != null ? ui.saveLoad.currentPage : 0; } catch { return 0; }
        }

        private static int SaveMaxPage(UIManager ui)
        {
            try { return ui.saveLoad != null ? ui.saveLoad.maxPage : 1; } catch { return 1; }
        }

        private static bool IsSaveMode(DataController dc)
        {
            if (dc == null) return false;
            // 反编译实查：DataController 里是 `protected bool isSave;`（**字段**，不是属性）。
            // 之前按属性取，反射永远拿不到 → 存/读档界面一律被念成「读档界面」。
            if (_fiDataIsSave == null && !_fiDataIsSaveChecked)
            {
                _fiDataIsSaveChecked = true;
                _fiDataIsSave = typeof(DataController).GetField("isSave",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            object v = _fiDataIsSave != null ? _fiDataIsSave.GetValue(dc) : null;
            if (v is bool) return (bool)v;
            if (_piDataIsSave == null && !_piDataIsSaveChecked)
            {
                _piDataIsSaveChecked = true;
                _piDataIsSave = typeof(DataController).GetProperty("isSave",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            object p = _piDataIsSave != null ? _piDataIsSave.GetValue(dc, null) : null;
            return p is bool && (bool)p;
        }

        private static int BacklogCount(Backlog backlog)
        {
            if (backlog == null) return 0;
            try
            {
                if (_piBacklogList == null)
                {
                    _piBacklogList = typeof(Backlog).GetProperty("DataList",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                var list = _piBacklogList != null ? _piBacklogList.GetValue(backlog, null) as System.Collections.ICollection : null;
                return list != null ? list.Count : 0;
            }
            catch { return 0; }
        }

        private static string YesNoText(UIManager ui)
        {
            try
            {
                var t = ui.yesNoDialogue != null ? ui.yesNoDialogue.contentText : null;
                return t != null ? TextProc.Clean(t.text) : "";
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------------ 历史回看专用浏览
        /// <summary>
        /// 历史回看面板里的每一条都只是一段 TMP 文本，没有 Selectable（探针实查：
        /// 该面板里 UiNav 只扫到「关闭按钮 + 滚动条」两个控件），所以逐条朗读必须自己做。
        /// 数据源仍是游戏自己的 Backlog.DataList（私有自动属性，反射读），
        /// 播语音则点该条自己的 voiceButton —— 等于玩家点了那一条的语音按钮。
        /// </summary>
        private static void BacklogKeys(UIManager ui)
        {
            if (ui.backlog == null || !ui.backlog.IsOpen) { _blIndex = -1; return; }
            if (KeyEdge.Pressed(KeyCode.Escape)) { ui.backlog.Close(); _blIndex = -1; return; }
            var list = BacklogList(ui.backlog);
            if (list == null || list.Count == 0) return;
            if (_blIndex < 0 || _blIndex >= list.Count) _blIndex = list.Count - 1;

            if (KeyEdge.Pressed(KeyCode.Home)) { _blIndex = 0; ReadBacklogEntry(list); return; }
            if (KeyEdge.Pressed(KeyCode.End)) { _blIndex = list.Count - 1; ReadBacklogEntry(list); return; }
            if (KeyEdge.Pressed(KeyCode.UpArrow))
            {
                if (_blIndex <= 0) Say("已经是最早一条。");
                else { _blIndex--; ReadBacklogEntry(list); }
                return;
            }
            if (KeyEdge.Pressed(KeyCode.DownArrow))
            {
                if (_blIndex >= list.Count - 1) Say("已经是最新一条。");
                else { _blIndex++; ReadBacklogEntry(list); }
                return;
            }
            if (KeyEdge.Pressed(KeyCode.Return) || KeyEdge.Pressed(KeyCode.KeypadEnter))
            {
                PlayBacklogVoice(ui, list);
            }
        }

        private static void ReadBacklogEntry(System.Collections.IList list)
        {
            var d = list[_blIndex] as BacklogData;
            if (d == null) return;
            string name = TextProc.Speaker(d.Name);
            string text = TextProc.Clean(d.Text);
            string head = "第 " + (_blIndex + 1) + " 条，" + (name.Length > 0 ? name + "，" : "");
            Repeat.Say(head + text, true);
        }

        private static void PlayBacklogVoice(UIManager ui, System.Collections.IList list)
        {
            var d = list[_blIndex] as BacklogData;
            if (d == null || string.IsNullOrEmpty(d.VoiceFileName))
            {
                Say("这一条没有配音。");
                return;
            }
            var items = ui.backlog.GetComponentsInChildren<BacklogItem>(true);
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it == null || it.Index != _blIndex) continue;
                if (it.voiceButton == null) break;
                it.voiceButton.onClick.Invoke();
                return;
            }
            Say("没有找到这一条的语音按钮。");
        }

        private static System.Collections.IList BacklogList(Backlog backlog)
        {
            if (backlog == null) return null;
            try
            {
                if (_piBacklogList == null)
                {
                    _piBacklogList = typeof(Backlog).GetProperty("DataList",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                return _piBacklogList != null ? _piBacklogList.GetValue(backlog, null) as System.Collections.IList : null;
            }
            catch { return null; }
        }

        /// <summary>存读档槽的朗读标签，供 UiNav.Game.cs 的 GameLabelOf 调用。</summary>
        internal static string SlotLabel(DataCheck dc) { return SlotLabel(dc, null); }

        /// <summary>
        /// 一个存档槽里其实有三个可点按钮（反编译 DataCheck 实查：私有字段 emptyButton、
        /// 槽按钮 button、deleteButton，后两个只在有数据时启用），必须分开念，
        /// 否则导航时同一个槽会连着念三遍完全一样的话。
        /// </summary>
        internal static string SlotLabel(DataCheck dc, Selectable me)
        {
            if (dc == null) return "";
            try
            {
                if (me != null)
                {
                    if (SameButton(dc, me, "deleteButton")) return "删除存档 " + (dc.index + 1);
                    if (SameButton(dc, me, "button") || SameButton(dc, me, "emptyButton"))
                        return Head(dc) + Body(dc) + (IsSaveMode(dc) ? "，回车保存到这里" : "，回车读取");
                }
            }
            catch { }
            return Head(dc) + Body(dc);
        }

        private static bool IsSaveMode(DataCheck dc)
        {
            try
            {
                var ui = UIManager.Instance;
                return ui != null && IsSaveMode(ui.saveLoad);
            }
            catch { return false; }
        }

        private static string Head(DataCheck dc)
        {
            string kind = IsSaveMode(dc) ? "存档槽 " : "读档槽 ";
            return kind + (dc.index + 1);
        }

        private static string Body(DataCheck dc)
        {
            if (!dc.HasData) return "，空";
            try
            {
                string day = dc.dayText != null ? TextProc.Clean(dc.dayText.text) : "";
                string hour = dc.hourText != null ? TextProc.Clean(dc.hourText.text) : "";
                string body = dc.contentText != null ? TextProc.Clean(dc.contentText.text) : "";
                body = body.Replace(": ", "，").TrimEnd('。', '，', ' ');
                string when = ((day.Length > 0 ? day + " " : "") + hour).Trim();
                string s = "，" + (when.Length > 0 ? when + "，" : "") + body;
                return s.TrimEnd('，');
            }
            catch { return ""; }
        }

        private static bool SameButton(DataCheck dc, Selectable me, string fieldName)
        {
            try
            {
                var f = typeof(DataCheck).GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                return f != null && ReferenceEquals(f.GetValue(dc) as Selectable, me);
            }
            catch { return false; }
        }

        /// <summary>
        /// 设置界面里 6 个滑条没有自己的文字，平台层只会念「未命名滑条」。
        /// 这里按**对象身份**把它们对上游戏自己的字段（反编译实查 Config 的
        /// protected Slider 字段），中文名取自设置页截图上的行标签，不靠位置猜。
        /// </summary>
        internal static string ConfigLabelOf(Selectable s)
        {
            if (s == null) return "";
            try
            {
                var ui = UIManager.Instance;
                var cfg = ui != null ? ui.Config : null;
                if (cfg == null) return "";
                var sl = s as Slider;
                if (sl == null) return "";
                return SliderName(cfg, sl);
            }
            catch { return ""; }
        }

        private static string SliderName(Config cfg, Slider sl)
        {
            string[] fields = { "sliderMessageSpeed", "sliderAutoBrPageSpeed", "sliderSoundMasterVolume",
                                "sliderBgmVolume", "sliderSeVolume", "sliderVoiceVolume" };
            string[] names = { "文本显示速度", "自动翻页速度", "全局音量", "音乐音量", "音效音量", "语音音量" };
            for (int i = 0; i < fields.Length; i++)
            {
                var f = typeof(Config).GetField(fields[i],
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f == null) continue;
                if (ReferenceEquals(f.GetValue(cfg) as Slider, sl)) return names[i];
            }
            return "";
        }

        /// <summary>历史回看条目的朗读标签。</summary>
        internal static string BacklogLabel(BacklogItem item, bool isVoiceButton)
        {
            if (item == null) return "";
            try
            {
                if (isVoiceButton) return "播放第 " + (item.Index + 1) + " 条语音";
                var d = item.Data;
                if (d == null) return "第 " + (item.Index + 1) + " 条";
                string name = TextProc.Speaker(d.Name);
                string text = TextProc.Clean(d.Text);
                string head = "第 " + (item.Index + 1) + " 条";
                return (name.Length > 0 ? head + "，" + name + "，" + text : head + "，" + text);
            }
            catch { return ""; }
        }

        // ------------------------------------------------------------------ 热键（补游戏没有键盘入口的地方）
        /// <summary>
        /// 本作的原生键盘入口：空格/回车推进、Esc 菜单、S 快进、A 自动。
        /// 缺的是：历史回看只有「鼠标滚轮上滑」、存读档只能点菜单按钮。
        /// 这里补三个热键直接调 UIManager 的公开方法（等于玩家自己点了菜单里那一项，
        /// 不改变游戏行为、不扩大可达范围）。
        /// </summary>
        private static void Hotkeys(UIManager ui)
        {
            if (!_keysReady)
            {
                _keysReady = true;
                _keyBacklog = KeyEdge.Parse(Plugin.CfgKeyBacklog != null ? Plugin.CfgKeyBacklog.Value : "");
                _keySave = KeyEdge.Parse(Plugin.CfgKeySave != null ? Plugin.CfgKeySave.Value : "");
                _keyLoad = KeyEdge.Parse(Plugin.CfgKeyLoad != null ? Plugin.CfgKeyLoad.Value : "");
                if (A11yHost.Log != null)
                    A11yHost.Log.LogInfo("[hotkey] 历史回看=" + _keyBacklog + " 存档=" + _keySave
                        + " 读档=" + _keyLoad + " 存读档对象=" + (ui.saveLoad != null ? ui.saveLoad.name : "null")
                        + " 场景=" + SceneManager.GetActiveScene().name);
            }

            bool anyOpen = (ui.SettingController != null && ui.SettingController.IsOpen)
                           || (ui.Config != null && ui.Config.IsOpen)
                           || (ui.backlog != null && ui.backlog.IsOpen)
                           || (ui.sharedVariable != null && ui.sharedVariable.SaveLoadOpen)
                           || (ui.yesNoDialogue != null && ui.yesNoDialogue.IsOpen);

            // 历史回看：只在对话进行中、且没有别的面板时打开（与游戏自身的滚轮入口条件一致）。
            if (_keyBacklog != KeyCode.None && !anyOpen && InDialogue
                && KeyEdge.Pressed(_keyBacklog) && ui.backlog != null && !ui.backlog.IsOpen)
            {
                ui.OpenBacklog();
                return;
            }

            // 存/读档界面里 Esc 关闭（游戏里只能点返回按钮或右键，读屏用户摸不到）
            if (ui.sharedVariable != null && ui.sharedVariable.SaveLoadOpen
                && KeyEdge.Pressed(KeyCode.Escape) && ui.saveLoad != null)
            {
                ui.saveLoad.Close();
                return;
            }

            // 存/读档界面只在游戏场景里、且没有别的面板时打开。
            string sceneName = SceneManager.GetActiveScene().name;
            bool wantSave = _keySave != KeyCode.None && KeyEdge.Pressed(_keySave);
            bool wantLoad = _keyLoad != KeyCode.None && KeyEdge.Pressed(_keyLoad);
            if (wantSave || wantLoad)
            {
                if (A11yHost.Log != null)
                    A11yHost.Log.LogInfo("[hotkey] 按键命中 存档=" + wantSave + " 读档=" + wantLoad
                        + " anyOpen=" + anyOpen + " 场景=" + sceneName
                        + " 存读档对象=" + (ui.saveLoad != null ? ui.saveLoad.name : "null"));
            }
            if (anyOpen || sceneName != "GameScene" || ui.saveLoad == null) return;
            if (wantSave) ui.OpenSave();
            else if (wantLoad) ui.OpenLoad();
        }

        // ------------------------------------------------------------------ 场景 / 开机健康提示页
        /// <summary>
        /// 开机第一屏是「内容适合 14 岁以上」的提示页，**没有任何可聚焦控件**，
        /// 只能按空格或点鼠标进入（反编译实查 WarningAnim.Update：
        /// `if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButton(0))`，两段式）。
        /// 读屏用户看不到「点击屏幕任意位置」这行字，所以这里必须主动播报怎么过。
        /// 播报的是我们自己的操作指引，不抄游戏原文（IP 红线）。
        /// </summary>
        private static void SceneBits(UIManager ui)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene != _sceneName)
            {
                _sceneName = scene;
                _warnAnnounced = false;
                _lastLine = null;
                _dlg = null;
            }
            if (_warnAnnounced || scene != "StartScene") return;

            var warn = GameObject.Find("ConvertedCanvas/Warning");
            if (warn == null || !warn.activeInHierarchy) return;

            _warnAnnounced = true;
            Say("健康提示页，连按两次空格键进入游戏。");
        }

        // ------------------------------------------------------------------ 小工具
        internal static void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Repeat.Say(text, true);
        }

        /// <summary>
        /// 现在是不是「快进」状态。三种触发方式都要认（反编译实查 DialogueUGUI.Update）：
        ///   · 右上角 SKIP 按钮 → skipMode = true
        ///   · 共享变量 IsSkip（自动/快进开关）
        ///   · **按住左/右 Ctrl**（游戏里按住 Ctrl 就是快进，这不会改 skipMode）
        /// 漏掉 Ctrl 的表现是：按住 Ctrl 时每一句都在朗读（实机踩过），
        /// 本来应该只记不念、松开 Ctrl 时补念停住的那句。
        /// </summary>
        internal static bool IsSkipping(UIManager ui)
        {
            try
            {
                if (_dlg != null && _dlg.skipMode) return true;
                if (ui != null && ui.sharedVariable != null && ui.sharedVariable.IsSkip) return true;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return true;
                if (Input.GetKey(KeyCode.S)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>快进开始/停止的边沿检测（在 Plugin.Update 里调用，早于台词）。</summary>
        internal static void SkipEdge()
        {
            bool skipping = IsSkipping(UIManager.Instance);
            if (_skipWas && !skipping)
            {
                // 唯一出口：把快进期间停住的那一句念一次，然后清空（防抖：只念一次）
                string pending = _skipLine;
                _skipLine = null;
                if (pending != null && (Plugin.CfgAnnounceOnSkipStop == null || Plugin.CfgAnnounceOnSkipStop.Value))
                    Repeat.Say(pending, true);
            }
            _skipWas = skipping;
        }
    }
}
