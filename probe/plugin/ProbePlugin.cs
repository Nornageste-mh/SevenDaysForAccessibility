using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using GameProject;
using HarmonyLib;
using LoveStoryProject;
using NodeCanvas.DialogueTrees;
using NodeCanvas.DialogueTrees.UI.Examples;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SevenDaysForAccessibilityProbe
{
    /// <summary>
    /// 探针插件：只做只读观测，用来确认
    ///   1) BepInEx 能挂进《我与你共度的七日》
    ///   2) 关键 MonoBehaviour 在运行时到底在不在、叫什么名字
    ///   3) 我们打算挂的补丁点能不能被 Harmony 找到
    /// 输出写到游戏目录下的 BepInEx\probe_The seven days i spent with you.log（纯文本，方便脚本读）。
    /// </summary>
    [BepInPlugin(Guid, "我与你共度的七日 A11y Probe", "0.0.0.1")]
    public class ProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "sevendays.a11y.reader.probe";
        /// <summary>
        /// 是否把场景树里控件上的文字也写进日志。
        /// 默认 false：控件文字很可能就是台词，写进日志再分享出去等于再分发剧本。
        /// 只在确有必要对名字时临时打开，分享日志前先删掉 text= / legacyText=。
        /// </summary>
        internal const bool DumpUiText = false;
        private static ManualLogSource L;
        private static string _dumpPath;

        private void Awake()
        {
            L = Logger;
            _dumpPath = Path.Combine(Paths.GameRootPath, "BepInEx", "probe_The seven days i spent with you.log");
            File.WriteAllText(_dumpPath, "=== 我与你共度的七日 A11y Probe ===\n");
            Say("Awake: BepInEx 已挂载，游戏根目录 = " + Paths.GameRootPath);

            // —— 补丁可用性实验：本作安装路径含非 ASCII 字符时，Harmony 的 MonoMod
            //    导入器在 RuntimeAssembly.get_code_base 上抛 "Illegal byte sequence"。
            //    这里逐个单独尝试，回答两个问题：
            //      1) 是不是「只有第一个补丁能挂上」；
            //      2) 补丁方法签名里出现 NodeCanvas 类型（选项请求）时会不会先炸。
            //    顺序刻意把「选项请求」放第一个。
            try
            {
                ReportPatchTargets();
            }
            catch (Exception e)
            {
                Say("ReportPatchTargets 异常: " + e);
            }
            TryPatch("1-OnMultipleChoiceRequest(NodeCanvas 签名)", typeof(DialogueUGUI), "OnMultipleChoiceRequest", null,
                     new HarmonyMethod(typeof(ProbePatches).GetMethod("OnMultipleChoiceRequest_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("2-Backlog.AddLog(游戏类型签名)", typeof(Backlog), "AddLog", null,
                     new HarmonyMethod(typeof(ProbePatches).GetMethod("AddLog_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("3-DataController.LoadData(int)", typeof(DataController), "LoadData", new Type[] { typeof(int) },
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("LoadData_Pre", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("4-SettingController.Open()", typeof(SettingController), "Open", new Type[0],
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("SettingOpen_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("5-DialogueUGUI.OnLoadSaveData(string,string)", typeof(DialogueUGUI), "OnLoadSaveData", new Type[] { typeof(string), typeof(string) },
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("OnLoadSaveData_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("6-InputUtil.GetKeyDownReturn()", typeof(InputUtil), "GetKeyDownReturn", new Type[0],
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("GetKeyDownReturn_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("7-Backlog.Open()", typeof(Backlog), "Open", new Type[0],
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("BacklogOpen_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("8-DataController.Open()", typeof(DataController), "Open", new Type[0],
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("DataOpen_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("9-YesNoDialogue.ShowYesNo(string,Action,Action)", typeof(YesNoDialogue), "ShowYesNo", new Type[] { typeof(string), typeof(Action), typeof(Action) },
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("ShowYesNo_Post", BindingFlags.Static | BindingFlags.NonPublic)));
            TryPatch("10-DataController.LoadDataWithPath(string,string)", typeof(DataController), "LoadDataWithPath", new Type[] { typeof(string), typeof(string) },
                     null, new HarmonyMethod(typeof(ProbePatches).GetMethod("LoadDataWithPath_Pre", BindingFlags.Static | BindingFlags.NonPublic)));

            StartCoroutine(DumpLater());
        }

        private static float _nextInv;
        private static string _lastInv = "";
        private static string _uiPath;

        /// <summary>
        /// 周期扫描「画面上真实存在的 Selectable」并登记 —— 只为拿对象名与标签对名字，
        /// 供 UiNav.Game.cs 的别名表使用（铁律：名字只能来自实查，不许猜）。
        /// F9 触发一次完整场景树转储。选项按钮的文字属于剧本，一律不记。
        /// </summary>
        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F9)) DumpScenes();
                if (Time.unscaledTime >= _nextInv)
                {
                    _nextInv = Time.unscaledTime + 4f;
                    DumpSelectables();
                }
                PollState();
            }
            catch { }
        }

        private static void DumpSelectables()
        {
            if (_uiPath == null)
                _uiPath = Path.Combine(Paths.GameRootPath, "BepInEx", "probe_ui.log");
            var all = Resources.FindObjectsOfTypeAll<Selectable>();
            var lines = new List<string>();
            var seen = new HashSet<int>();
            foreach (var s in all)
            {
                if (s == null) continue;
                var go = s.gameObject;
                if (!go.scene.IsValid()) continue;              // 排除 prefab 资产
                if (!seen.Add(go.GetInstanceID())) continue;
                if (!go.activeInHierarchy) continue;
                string path = PathOf(s.transform);
                var rt = s.transform as RectTransform;
                bool onScreen = true;
                string extra = "";
                if (rt != null)
                {
                    var c = new Vector3[4];
                    rt.GetWorldCorners(c);
                    var cam = CanvasCam(rt);
                    Vector3 bl = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
                    Vector3 tr = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
                    onScreen = tr.x > 0 && tr.y > 0 && bl.x < Screen.width && bl.y < Screen.height;
                    extra += $" rect={Mathf.RoundToInt(tr.x - bl.x)}x{Mathf.RoundToInt(tr.y - bl.y)}";
                }
                float alpha = AlphaOf(s.transform);
                if (!onScreen || alpha <= 0.01f) continue;
                string label = "";
                bool isOption = path.ToLowerInvariant().Contains("option");
                if (isOption) label = "(选项，按纪律不记)";
                else
                {
                    var tmp = s.GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null && !string.IsNullOrEmpty(tmp.text)) label = Clean(tmp.text);
                    else
                    {
                        var ui = s.GetComponentInChildren<Text>(true);
                        if (ui != null && !string.IsNullOrEmpty(ui.text)) label = Clean(ui.text);
                    }
                }
                string kind = s.GetType().Name;
                string state = kind == "Toggle" ? " on=" + ((Toggle)s).isOn
                             : kind == "Slider" ? " val=" + ((Slider)s).value.ToString("0.00")
                             : " act=" + s.interactable;
                lines.Add($"  {kind} | {path} | label=\"{label}\"{state}{extra}");
            }
            lines.Sort(StringComparer.Ordinal);
            string body = "#### Selectable 清单 " + DateTime.Now.ToString("HH:mm:ss") + " ####\n" + string.Join("\n", lines);
            if (body == _lastInv) return;
            _lastInv = body;
            try { File.AppendAllText(_uiPath, body + "\n\n", Encoding.UTF8); } catch { }
            Say($"---- UI 清单变化：{lines.Count} 个可见可点控件（详见 probe_ui.log）----");
        }

        private static string Clean(string s)
        {
            s = s.Replace("\r", " ").Replace("\n", "\\n");
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            int guard = 0;
            while (t != null && guard++ < 12) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Camera CanvasCam(RectTransform rt)
        {
            var cv = rt.GetComponentInParent<Canvas>();
            if (cv == null) return null;
            if (cv.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return cv.worldCamera;
        }

        private static float AlphaOf(Transform t)
        {
            float a = 1f;
            int guard = 0;
            while (t != null && guard++ < 12)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) a *= cg.alpha;
                t = t.parent;
            }
            return a;
        }

        internal static void Say(string msg)
        {
            L?.LogInfo(msg);
            try { File.AppendAllText(_dumpPath, msg + "\n", Encoding.UTF8); } catch { }
        }

        private IEnumerator DumpLater()
        {
            // 等游戏把标题场景初始化完；后面几档覆盖到进游戏后的界面
            yield return new WaitForSeconds(8f);
            DumpScenes();
            yield return new WaitForSeconds(12f);
            DumpScenes();
            yield return new WaitForSeconds(35f);
            DumpScenes();
            yield return new WaitForSeconds(30f);
            DumpScenes();
        }

        /// <summary>
        /// 挂载点存在性报告：逐个确认本作补丁目标（P0 反编译实查的签名）
        /// 在运行时确实存在。类型名/方法名是互操作事实，可以写进日志。
        /// </summary>
        private static void ReportPatchTargets()
        {
            var gameAsm = typeof(DataController).Assembly;                              // Assembly-CSharp
            var ncAsm = typeof(MultipleChoiceRequestInfo).Assembly;                     // NodeCanvas.dll
            Say("---- 挂载点存在性（gameAsm=" + gameAsm.GetName().Name + "）----");
            Check(gameAsm, "LoveStoryProject.Backlog", "AddLog", "Open", "Close", "GetLastData", "ClearLog");
            Check(gameAsm, "LoveStoryProject.BacklogData", null);
            Check(gameAsm, "NodeCanvas.DialogueTrees.UI.Examples.DialogueUGUI",
                "OnSubtitlesRequest", "OnMultipleChoiceRequest", "Finalize", "OnLoadSaveData", "TriggerInput");
            Check(gameAsm, "DataController",
                "LoadData", "LoadDataWithPath", "Open", "Close", "OpenSave", "OpenLoad", "SaveData");
            Check(gameAsm, "InputUtil", "GetKeyDownReturn");
            Check(gameAsm, "SettingController", "Open", "Close", "ToggleMenu");
            Check(gameAsm, "GameProject.YesNoDialogue", "ShowYesNo");
            Check(gameAsm, "PlayEndTrigger", "LoadDataClick");
            Check(ncAsm, "NodeCanvas.DialogueTrees.MultipleChoiceRequestInfo", null);
            Check(ncAsm, "NodeCanvas.DialogueTrees.SubtitlesRequestInfo", null);
            Check(ncAsm, "NodeCanvas.DialogueTrees.Statement", "GetLocalizedText");
        }

        private static void Check(Assembly asm, string typeName, params string[] methods)
        {
            var t = asm.GetType(typeName);
            if (t == null) { Say($"[type] {typeName}: 未找到"); return; }
            if (methods == null || methods.Length == 0 || methods[0] == null)
            {
                Say($"[type] {typeName}: OK（仅查类型存在）");
                return;
            }
            var ms = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                      .Select(m => m.Name).Distinct().OrderBy(x => x);
            var set = new HashSet<string>(ms);
            foreach (var m in methods)
                Say($"[type] {typeName}.{m}: {(set.Contains(m) ? "OK" : "!! 缺失")}");
        }

        private static void DumpScenes()
        {
            Say("");
            Say("################ 场景树转储 " + DateTime.Now.ToString("HH:mm:ss") + " ################");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                Say($"===== Scene[{i}] {sc.name} (rootObjects={sc.rootCount}) =====");
                // 本作 Unity 版本的 Scene.GetRootGameObjects 只有 List 缓冲重载
                var roots = new List<GameObject>();
                sc.GetRootGameObjects(roots);
                foreach (var go in roots)
                    Walk(go.transform, 0);
            }
            var all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            var byType = all.Where(m => m != null)
                            .GroupBy(m => m.GetType().Name)
                            .OrderByDescending(g => g.Count())
                            .Take(60);
            Say("---- 活着的 MonoBehaviour 类型（Top60）----");
            foreach (var g in byType) Say($"  {g.Key} x{g.Count()}");
        }

        private static void Walk(Transform t, int depth)
        {
            if (depth > 6) return;
            var go = t.gameObject;
            var sb = new StringBuilder();
            sb.Append(new string(' ', depth * 2));
            sb.Append(go.activeSelf ? "+ " : "- ");
            sb.Append(t.name);
            var comps = go.GetComponents<Component>()
                           .Where(c => c != null)
                           .Select(c => c.GetType().Name)
                           .Where(n => n != "Transform" && n != "RectTransform" && n != "CanvasRenderer");
            string cs = string.Join(",", comps);
            if (cs.Length > 0) sb.Append("  [" + cs + "]");
            if (DumpUiText)
            {
                // 控件上的文字很可能就是台词 —— 默认不写，原因见 DumpUiText 的说明。
                var tmp = go.GetComponent<TMP_Text>();
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                {
                    string txt = tmp.text.Replace("\n", "\\n");
                    if (txt.Length > 60) txt = txt.Substring(0, 60) + "…";
                    sb.Append("  text=\"" + txt + "\"");
                }
                var ui = go.GetComponent<Text>();
                if (ui != null && !string.IsNullOrEmpty(ui.text))
                {
                    string txt = ui.text.Replace("\n", "\\n");
                    if (txt.Length > 60) txt = txt.Substring(0, 60) + "…";
                    sb.Append("  legacyText=\"" + txt + "\"");
                }
            }
            Say(sb.ToString());
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1);
        }

        // ---- 轮询观测：验证「不依赖 Harmony 的轮询读屏」数据源是否可靠 ----
        private static float _nextPoll;
        private static string _lastPollSig = "";
        private static UIManager _ui;
        private static DialogueUGUI _dugui;

        /// <summary>
        /// 每 0.5s 采样一次「轮询读屏」要用的公开 API，只有状态变化才写一行。
        /// 只记长度/哈希/标志，不记台词原文（IP 红线）。
        /// </summary>
        private void PollState()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;
            var sb = new StringBuilder();
            sb.Append("scene=").Append(SceneManager.GetActiveScene().name);
            if (_ui == null) _ui = UnityEngine.Object.FindObjectOfType<UIManager>();
            if (_dugui == null) _dugui = UnityEngine.Object.FindObjectOfType<DialogueUGUI>();
            sb.Append(" ui=").Append(_ui != null ? "Y" : "N");
            if (_ui != null)
            {
                var sc = _ui.SettingController;
                sb.Append(" menu=").Append(sc != null && sc.IsOpen ? 1 : 0);
                var cfg = _ui.Config;
                sb.Append(" cfg=").Append(cfg != null && cfg.IsOpen ? 1 : 0);
                sb.Append(" backlog=").Append(_ui.backlog != null && _ui.backlog.IsOpen ? 1 : 0);
                sb.Append(" saveload=").Append(_ui.sharedVariable != null && _ui.sharedVariable.SaveLoadOpen ? 1 : 0);
                sb.Append(" yesno=").Append(_ui.yesNoDialogue != null && _ui.yesNoDialogue.IsOpen ? 1 : 0);
                sb.Append(" status=").Append(_ui.Status);
                var last = _ui.LastBacklog();
                if (last == null) sb.Append(" line=NONE");
                else sb.Append(" line=n").Append(last.Name == null ? 0 : last.Name.Length)
                        .Append("/t").Append(last.Text == null ? 0 : last.Text.Length)
                        .Append("/v").Append(string.IsNullOrEmpty(last.VoiceFileName) ? 0 : 1)
                        .Append("/h").Append(Hash(last.Text ?? ""));
            }
            if (_dugui != null)
            {
                sb.Append(" dlg_auto=").Append(_dugui.autoMode ? 1 : 0)
                  .Append(" skip=").Append(_dugui.skipMode ? 1 : 0);
                var og = _dugui.optionsGroup;
                if (og != null)
                {
                    sb.Append(" opt=").Append(og.gameObject.activeInHierarchy ? 1 : 0).Append("/").Append(og.childCount);
                    var cg = og.GetComponent<CanvasGroup>();
                    if (cg != null) sb.Append("/a").Append(Mathf.Round(cg.alpha * 100f));
                    for (int i = 0; i < og.childCount && i < 10; i++)
                    {
                        var t = og.GetChild(i).GetComponentInChildren<Text>();
                        sb.Append("/L").Append(t == null || t.text == null ? -1 : t.text.Length);
                    }
                }
            }
            var warn = GameObject.Find("ConvertedCanvas/Warning");
            sb.Append(" warn=").Append(warn != null && warn.activeInHierarchy ? 1 : 0);
            string sig = sb.ToString();
            if (sig != _lastPollSig)
            {
                _lastPollSig = sig;
                Say("[poll] " + sig);
            }
        }

        private static string Hash(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
            return h.ToString("x8");
        }
        private static int _patchIdx;

        private static void TryPatch(string label, Type declaring, string method, Type[] args,
                                     HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            _patchIdx++;
            try
            {
                var candidates = declaring.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                MethodBase target = null;
                foreach (var mi in candidates)
                {
                    if (mi.Name != method) continue;
                    if (args != null)
                    {
                        var ps = mi.GetParameters();
                        if (ps.Length != args.Length) continue;
                        bool same = true;
                        for (int i = 0; i < ps.Length; i++) if (ps[i].ParameterType != args[i]) { same = false; break; }
                        if (!same) continue;
                    }
                    target = mi;
                    break;
                }
                if (target == null) { Say($"[patch-test] {label}: 找不到方法"); return; }
                var h = new Harmony(Guid + ".t" + _patchIdx);
                h.Patch(target, prefix, postfix);
                Say($"[patch-test] {label}: OK（已挂载）");
            }
            catch (Exception e)
            {
                var msg = e.ToString();
                if (msg.Length > 500) msg = msg.Substring(0, 500) + "…";
                Say($"[patch-test] {label}: FAIL {e.GetType().Name}: {msg}");
            }
        }
    }

    internal static class ProbePatches
    {
        // 探针只记录**结构信息**（类型、参数个数、有没有配音、说话人长度），
        // 绝不把台词原文写进日志：
        //   · 台词是游戏剧本，属于著作权人的文字资产，落到日志文件里再被
        //     提交进版本库就等于再分发；
        //   · 验证「有没有配音」「朗读有没有被触发」根本不需要原文，
        //     只需要长度和标志位。
        // 同理，选项文字也只记条数，不记内容。
        // 场景树遍历（ProbePlugin.Walk）里的控件文字是唯一的例外，由
        // ProbePlugin.DumpUiText 控制，默认关闭。
        private static string Shape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(空)";
            return "(len=" + s.Length + ")";
        }

        // —— 台词收口：全部台词/旁白/任务文本都走 Backlog.AddLog ——
        [HarmonyPatch(typeof(Backlog), "AddLog")]
        [HarmonyPostfix]
        private static void AddLog_Post(BacklogData data)
        {
            ProbePlugin.Say($"[hook] Backlog.AddLog name={Shape(data?.Name)} text={Shape(data?.Text)} "
                + $"voiced={!string.IsNullOrEmpty(data?.VoiceFileName)}");
        }

        [HarmonyPatch(typeof(DialogueUGUI), "OnSubtitlesRequest")]
        [HarmonyPostfix]
        private static void OnSubtitlesRequest_Post(SubtitlesRequestInfo info)
        {
            ProbePlugin.Say($"[hook] OnSubtitlesRequest name={Shape(info?.actor?.name)} "
                + $"text={Shape(info?.statement?.text)} audio={(info?.statement?.audio != null)}");
        }

        // —— 选项：请求/最终选择 ——
        [HarmonyPatch(typeof(DialogueUGUI), "OnMultipleChoiceRequest")]
        [HarmonyPostfix]
        private static void OnMultipleChoiceRequest_Post(MultipleChoiceRequestInfo info)
        {
            ProbePlugin.Say($"[hook] OnMultipleChoiceRequest options={info?.options?.Count} "
                + $"availableTime={info?.availableTime} showLastStatement={info?.showLastStatement}");
        }

        [HarmonyPatch(typeof(DialogueUGUI), "Finalize")]
        [HarmonyPostfix]
        private static void Finalize_Post(MultipleChoiceRequestInfo info, int index)
        {
            ProbePlugin.Say($"[hook] Finalize index={index} options={info?.options?.Count}");
        }

        // —— 读档重显当前行 ——
        [HarmonyPatch(typeof(DialogueUGUI), "OnLoadSaveData")]
        [HarmonyPostfix]
        private static void OnLoadSaveData_Post(string characterName, string characterText)
        {
            ProbePlugin.Say($"[hook] OnLoadSaveData name={Shape(characterName)} text={Shape(characterText)}");
        }

        // —— 存读档流程 ——
        [HarmonyPatch(typeof(DataController), "LoadData")]
        [HarmonyPrefix]
        private static void LoadData_Pre(int index)
        {
            ProbePlugin.Say($"[hook] DataController.LoadData index={index}");
        }

        [HarmonyPatch(typeof(DataController), "LoadDataWithPath")]
        [HarmonyPrefix]
        private static void LoadDataWithPath_Pre(string path, string dataName)
        {
            // path 是玩家本机路径，不记；dataName 是槽位名（saveData{N}）
            ProbePlugin.Say($"[hook] LoadDataWithPath dataName={dataName ?? "(null)"}");
        }

        // —— 推进确认（只在返回 true 时记，避免每帧刷屏）——
        [HarmonyPatch(typeof(InputUtil), "GetKeyDownReturn")]
        [HarmonyPostfix]
        private static void GetKeyDownReturn_Post(bool __result)
        {
            if (__result) ProbePlugin.Say("[hook] GetKeyDownReturn=true（玩家推进）");
        }

        // —— 面板开合（界面导航播报点）——
        [HarmonyPatch(typeof(SettingController), "Open")]
        [HarmonyPostfix]
        private static void SettingOpen_Post() => ProbePlugin.Say("[hook] SettingController.Open");

        [HarmonyPatch(typeof(SettingController), "Close")]
        [HarmonyPostfix]
        private static void SettingClose_Post() => ProbePlugin.Say("[hook] SettingController.Close");

        [HarmonyPatch(typeof(DataController), "Open")]
        [HarmonyPostfix]
        private static void DataOpen_Post() => ProbePlugin.Say("[hook] DataController.Open（存/读档面板）");

        [HarmonyPatch(typeof(DataController), "Close")]
        [HarmonyPostfix]
        private static void DataClose_Post() => ProbePlugin.Say("[hook] DataController.Close");

        [HarmonyPatch(typeof(Backlog), "Open")]
        [HarmonyPostfix]
        private static void BacklogOpen_Post() => ProbePlugin.Say("[hook] Backlog.Open（历史回看）");

        [HarmonyPatch(typeof(Backlog), "Close")]
        [HarmonyPostfix]
        private static void BacklogClose_Post() => ProbePlugin.Say("[hook] Backlog.Close");

        [HarmonyPatch(typeof(YesNoDialogue), "ShowYesNo")]
        [HarmonyPostfix]
        private static void ShowYesNo_Post(string message)
        {
            ProbePlugin.Say($"[hook] YesNoDialogue.ShowYesNo message={Shape(message)}");
        }
    }
}
