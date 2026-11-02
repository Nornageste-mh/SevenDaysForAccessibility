using System;
using System.Collections.Generic;
using LoveStoryProject;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDaysForAccessibility
{
    /// <summary>
    /// UiNav 的**逐作区**（partial class）。
    ///
    /// 平台层 platform\UiNav.cs 里是 `internal static partial class UiNav`，本文件是同一个
    /// 类的另一半：引擎侧扫描/播报逻辑在平台层，这里只放「只有本作才知道」的数据——
    /// 对象名别名、输入框角色、标签/分组钩子。
    ///
    /// 纪律：
    ///   · 平台层一行都不要改；所有游戏类型引用都只能出现在这里、Watcher.cs 或 Plugin.cs。
    ///   · 播报一律走 Repeat.Say（不要直接调 Speech.Speak）——否则玩家按重读键听不到
    ///     刚念的内容（tools\lint_repeat.ps1 会机械拦住）。
    ///   · 别名/钩子每一条都要有依据（反编译实查或 probe 运行时转储），不凭感觉加。
    /// </summary>
    internal static partial class UiNav
    {
        /// <summary>
        /// 对象名 → 中文。**只在控件自己一个字都没有时才会用到**
        /// （平台层顺序：子树文字 → GameLabelOf → 别名表 → 同行文字 → 原始名）。
        ///
        /// 本作实查结论（probe_ui.log，标题页 6 个按钮）：
        ///   ConvertedCanvas/GameUI/State1/Fade1/Buttons/{Rewrite, StartGame, Continue,
        ///   Gallery, Config, Exit} 每颗都有子对象 `Text (TMP)`（TextMeshProUGUI +
        ///   OnLangaugeHanddle），标签是本地化中文「重写此生 / 新的游戏 / 继续游戏 /
        ///   美术鉴赏 / 游戏设置 / 退出游戏」，宽度 291×81。
        ///   → 平台层的「子树文字」这一级已经能念对，**标题页不需要别名**。
        ///
        /// 目前留空：图标型按钮（GameScene 左上齿轮、书本图标，右上 SKIP/AUTO）的
        /// 对象名还没有从场景树里实查到，等实查后按证据补，不猜。
        ///
        /// ★ 字典是 OrdinalIgnoreCase：只差大小写的两个键会在静态构造时抛
        ///   ArgumentException → TypeInitializationException，表现为**导航 100% 失效
        ///   且没有任何日志**。新增/改名后先按忽略大小写查重
        ///   （tools\compile_check.ps1 模式 B 会解析本初始化器并报出重复键行号）。
        /// </summary>
        private static readonly Dictionary<string, string> NameAlias =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
        };

        // ================= 输入框角色 =================
        //
        // 平台层对 TMP_InputField 会调这里问「这个框是干什么的」（念作「姓氏，输入框」）。
        // 本作没有输入框：反编译的 Assembly-CSharp 里没有姓名/昵称输入界面，
        // 标题页/存读档/设置页都没有 TMP_InputField 参与交互（probe 场景转储里
        // 只有 Button / Toggle / Slider）。所以直接返回空串，平台层退回通用朗读。
        // 如果哪天游戏加了输入框，这里再按实查结果补。
        private static string InputFieldRoleName(TMP_InputField inf)
        {
            return "";
        }

        // ================= 标签钩子 =================

        /// <summary>
        /// 给没有「自己的文字」的控件补可读标签。返回空串 = 不覆盖，照平台层往下走。
        ///
        /// 本作两类控件需要它（对象结构来自反编译实查）：
        ///
        ///   1) 存读档槽：`DataController` 里的 `DataCheck`
        ///        public int index; public string fileName;
        ///        public TextMeshProUGUI noText / contentText / dayText / hourText;
        ///        public RawImage thumbnail; public bool HasData => checker.HasValue;
        ///      槽位本体是缩略图 + 几段文字，读屏要的是「存档槽 3，第 2 天 12:00，
        ///      正文开头…」一句话，而不是把子树里每段文字拆成好几个控件念。
        ///
        ///   2) 历史回看条目：`LoveStoryProject.BacklogItem`
        ///        public TextMeshProUGUI nameLabel / textLabel;
        ///        public Button voiceButton; public int Index; public BacklogData Data;
        ///      条目本体是两段 TMP 文本（念它自己就够了），只有**播放语音按钮**
        ///      是纯图标——那一个必须靠这里给出「播放第 3 条语音」。
        /// </summary>
        internal static string GameLabelOf(Selectable s)
        {
            if (s == null) return "";
            try
            {
                // 1) 存读档槽：一个槽里有 3 个按钮（空槽按钮 / 槽按钮 / 删除按钮），
                //    实查 DataCheck 反编译：私有字段 emptyButton、button、deleteButton。
                //    必须区分，否则三个都念成同一句「存档槽 N」。
                DataCheck slot = s.GetComponentInParent<DataCheck>();
                if (slot != null) return Watcher.SlotLabel(slot, s);

                // 2) 设置界面的滑条：没有自己的文字，只能靠字段身份对名字
                string cfgLabel = Watcher.ConfigLabelOf(s);
                if (cfgLabel.Length > 0) return cfgLabel;

                // 3) 历史回看的语音按钮（BacklogItem 里唯一的图标控件）
                BacklogItem item = s.GetComponentInParent<BacklogItem>();
                if (item != null && item.voiceButton == s)
                    return Watcher.BacklogLabel(item, true);
            }
            catch
            {
                // 钩子不能把异常抛给平台层；读不到就不覆盖
            }
            return "";
        }

        // ================= 分组钩子 =================
        //
        // 平台层把控件按「最顶层 Canvas 祖先」分组，一组念一次「面板 N，共 M 项」。
        // 本作会用到分组的地方：标题页、存读档页、设置页、历史回看。
        // 暂时都用默认分组（「面板 N」）——还没实机确认这些面板的 Canvas 结构，
        // 等听到「面板2」这种沉默说法再按场景树结果补名字，不猜。

        internal static string GameGroupName(Transform root)
        {
            return "";
        }

        /// <summary>组的播报顺序（小的先念）。默认 100 = 平台默认顺序。</summary>
        internal static int GameGroupPriority(Transform root)
        {
            return 100;
        }

        // ================= 噪音文字 =================
        //
        // 返回 true 的文字不念（平台层在「子树文字」阶段就会过滤）。
        // 本作实查到的文本全部是有意义内容（本地化表 2067 条 + 界面 ui_* 6 条），
        // 没有纯装饰性文字，所以这里不拦。实测发现噪音再补，并注明依据。

        internal static bool GameTextIsNoise(string text)
        {
            return false;
        }
    }
}
