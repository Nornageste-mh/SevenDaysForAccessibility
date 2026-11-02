using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDaysForAccessibility
{
    /// <summary>
    /// 选项层（轮询式）。游戏把选项按钮 Instantiate 到 DialogueUGUI.optionsGroup 下，
    /// 子物体顺序 = 显示顺序（反编译实查：DialogueUGUI.OnMultipleChoiceRequest 里
    /// `foreach (KeyValuePair<IStatement,int> option in info.options)` 顺序建按钮、
    /// 逐个 SetParent 到 optionsGroup，并把 `cachedButtons.Add(btn, option.Value)`）。
    ///
    /// 数字键不自己做选择逻辑，而是调**那颗按钮自己的 onClick** —— 等价玩家点了一下，
    /// 走游戏自己的 Finalize(info, index)：它会停倒计时协程、把按钮置灰、再调
    /// info.SelectOption(index)。这样不可能出现「我们选的值和游戏以为的不一致」。
    ///
    /// 限时选项：没有补丁就改不了 info.availableTime，所以**不假装能延长**。
    /// 倒计时开始后游戏用 SetMassAlpha 把整组按钮的 CanvasRenderer alpha 从 1 降到 0，
    /// 轮询这个 alpha 就能判定「这是限时选项」，并提示赶紧按数字键；
    /// 沉默键（默认 0）调最后一颗按钮的 onClick = 游戏自己超时会选的那一项
    /// （反编译实查 CountDown：`if (isWaitingChoice) Finalize(info, info.options.Values.Last())`），
    /// 所以也不扩大可达范围。
    /// </summary>
    internal static class Choices
    {
        private const string Tag = "choices";

        private static readonly List<Transform> _buttons = new List<Transform>();
        private static readonly List<string> _texts = new List<string>();
        private static readonly StringBuilder _sb = new StringBuilder(256);

        private static string _key = "";

        // 选中之后 2 秒内不再捕获/播报：游戏在 Finalize 之后会把整组按钮淡出
        // （SetMassAlpha 把 alpha 从 1 降到 0 再隐藏），这期间子物体仍然 active，
        // 不设冷却就会把正在淡出的同一组选项再念一遍，还会被误判成「限时选项」。
        // 实机踩过：选完立刻又念一遍选项，紧接着念「这是限时选项」。
        private static float _suppressUntil;
        private static float _capturedAt;

        // 同一组选项文本 5 秒内只念一次（防游戏重复请求/IP 文案重复）
        private static string _lastTextKey = "";
        private static float _lastTextAt;

        /// <summary>刚刚选过，冷却中：这期间 Watcher 不要重新捕获。</summary>
        internal static bool Suppressed { get { return Time.unscaledTime < _suppressUntil; } }
        private static bool _timed;
        private static bool _timedSaid;

        internal static bool HasContent { get { return _buttons.Count > 0; } }

        /// <summary>当前选项集合的指纹：按钮实例 ID，用来判断「还是不是同一组选项」。</summary>
        internal static string Signature(RectTransform group)
        {
            _sb.Length = 0;
            if (group == null) return "";
            for (int i = 0; i < group.childCount; i++)
            {
                Transform t = group.GetChild(i);
                // 只算「显示出来的」子物体：optionsGroup 里还挂着一颗设计期的占位按钮
                // （文字是「测试用文本」，probe 轮询实测 childCount=1、文本长度 5，
                // 选项弹出后变成 3 个子物体但画面上只有 2 个可选）。
                // 混进来会造成两个后果：编号错位、以及 Update() 的失效自检每帧把整组清掉
                // → 下一帧重新捕获 → 同一组选项被反复朗读（实机踩过）。
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                _sb.Append(t.GetInstanceID()).Append('|');
            }
            return _sb.ToString();
        }

        internal static bool IsSameSet(RectTransform group)
        {
            return _buttons.Count > 0 && _key == Signature(group);
        }

        internal static void Capture(RectTransform group)
        {
            Clear();
            if (group == null) return;

            _key = Signature(group);
            int n = group.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform t = group.GetChild(i);
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                _buttons.Add(t);
                _texts.Add(LabelOf(t, _buttons.Count - 1));
            }
            if (_buttons.Count == 0) return;
            _capturedAt = Time.unscaledTime;

            // 清掉 EventSystem 的选中控件：游戏没有键盘选项导航，但选项按钮会被
            // 自动设为「当前选中」，此时玩家按空格/回车（推进剧情的习惯动作）会
            // 直接把某个选项点掉 —— 实机踩过（选项出现后按空格，第一项被选中）。
            // 清掉之后空格在选项界面不再误触，数字键仍然有效。
            try
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
            catch { }

            if (Plugin.CfgReadChoices != null && !Plugin.CfgReadChoices.Value) return;

            // 同一组文本短时间内只念一次（防游戏重复请求同一组选项）
            string textKey = string.Join("|", _texts.ToArray());
            if (textKey == _lastTextKey && Time.unscaledTime - _lastTextAt < 5f) return;
            _lastTextKey = textKey;
            _lastTextAt = Time.unscaledTime;

            Announce();
        }

        private static string LabelOf(Transform t, int index)
        {
            try
            {
                // 选项文字在按钮子物体上的 UGUI Text（反编译实查：
                // btn.GetComponentInChildren<Text>().text = option.Key.GetLocalizedText(language)）
                var ui = t.GetComponentInChildren<Text>(true);
                if (ui != null && !string.IsNullOrEmpty(ui.text)) return TextProc.Clean(ui.text);
                var tmp = t.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return TextProc.Clean(tmp.text);
            }
            catch { }
            return "选项 " + (index + 1);
        }

        /// <summary>播报整组选项。分隔符只用逗号/句号：读屏对「：」「；」的停顿不稳（aliya 经验）。</summary>
        private static void Announce()
        {
            _sb.Length = 0;
            _sb.Append("共 ").Append(_buttons.Count).Append(" 个选项。");
            for (int i = 0; i < _texts.Count; i++)
            {
                _sb.Append(i + 1).Append('，').Append(_texts[i]).Append('。');
            }
            if (Plugin.CfgChoiceHotkeys == null || Plugin.CfgChoiceHotkeys.Value)
                _sb.Append("按数字键选择。");
            Repeat.Push(_sb.ToString(), Tag, true);
        }

        internal static void Clear()
        {
            if (_buttons.Count > 0) Repeat.Drop(Tag);
            _buttons.Clear();
            _texts.Clear();
            _key = "";
            _timed = false;
            _timedSaid = false;
        }

        internal static void Update()
        {
            if (_buttons.Count == 0) return;
            if (Suppressed) return;

            // 选项收起了 / 按钮被销毁 → 收尾（不播报：游戏自己超时或玩家用鼠标选了）
            for (int i = 0; i < _buttons.Count; i++)
            {
                Transform t = _buttons[i];
                if (t == null || !t.gameObject.activeInHierarchy) { Clear(); return; }
            }

            DetectTimed();

            bool hotkeys = Plugin.CfgChoiceHotkeys == null || Plugin.CfgChoiceHotkeys.Value;
            if (hotkeys)
            {
                int max = Mathf.Min(9, _buttons.Count);
                for (int n = 1; n <= max; n++)
                {
                    if (KeyEdge.DigitPressed(n)) { Activate(n - 1); return; }
                }
            }

            // 沉默键（默认 0）：只在限时选项里当作「按游戏超时会选的那一项」
            if (_timed && Plugin.CfgSilenceKey != null)
            {
                KeyCode k = KeyEdge.Parse(Plugin.CfgSilenceKey.Value);
                if (k != KeyCode.None && KeyEdge.Pressed(k)) Activate(_buttons.Count - 1);
            }
        }

        /// <summary>
        /// 限时检测：游戏倒计时每帧调 SetMassAlpha(optionsGroup, Lerp(1,0,t/T))，
        /// 也就是把按钮下所有 CanvasRenderer 的 alpha 压下去。alpha 掉到 0.97 以下
        /// 就说明这是限时选项（普通选项 alpha 恒为 1）。
        /// </summary>
        private static void DetectTimed()
        {
            if (_timed) return;
            if (Time.unscaledTime - _capturedAt < 0.8f) return;   // 避开淡入淡出动画
            try
            {
                var cr = _buttons[0].GetComponentInChildren<CanvasRenderer>(true);
                if (cr == null) return;
                if (cr.GetAlpha() < 0.97f)
                {
                    _timed = true;
                    if (!_timedSaid)
                    {
                        _timedSaid = true;
                        Repeat.Say("这是限时选项，倒计时结束游戏会自动选择最后一项。请尽快按数字键选择。", false);
                    }
                }
            }
            catch { }
        }

        private static void Activate(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= _buttons.Count) return;
            Transform t = _buttons[displayIndex];
            if (t == null) return;

            var btn = t.GetComponent<Button>();
            if (btn == null) btn = t.GetComponentInChildren<Button>(true);
            if (btn == null || !btn.interactable) return;

            string text = _texts[displayIndex];
            _suppressUntil = Time.unscaledTime + 2f;   // 淡出期间别再捕获
            Clear();                       // 先收掉临时项，避免 Push/Drop 不成对
            Repeat.Say("已选择，" + text + "。", true);
            try { btn.onClick.Invoke(); }
            catch (Exception e)
            {
                if (A11yHost.Log != null) A11yHost.Log.LogError("[choices] 触发选项失败: " + e.Message);
            }
        }
    }
}
