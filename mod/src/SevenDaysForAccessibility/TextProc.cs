using System.Text.RegularExpressions;

namespace SevenDaysForAccessibility
{
    /// <summary>
    /// 原始文本 → 可朗读文本（《我与你共度的七日》专用）。
    ///
    /// === 本作形态依据（D:\DSHWorkSpace\sevendays_work\text\text_inventory.md §4）===
    ///
    /// 本地化表 StringLanguage（zh）共 2067 条，值的形态统计（未抄台词原文）：
    ///   - <color> 0 条、<b>/<i> 0 条、{N} 占位符 0 条、「」0 条
    ///   - &lt;br&gt; 3 条（换行式："……<br>……"）
    ///   - &lt;size=52&gt;…&lt;/size&gt; 3 条（强调式："……？<标注><size=52>……</size>"）
    ///   - 自定义尖括号标注 20 条（&lt;我&gt;×16、&lt;汐萝&gt;×1 等，角色/术语/短语标记，
    ///     **非 Unity 合法富文本标签**，游戏渲染意图尚未实机验证）
    ///   - 真实换行 7 条；【】1 条
    ///
    /// 纪律：正则只处理上表实查到的形态，不做「通用富文本剥离」；
    /// 每条规则注明依据；拿不准的形态先输出原文，宁可多念也不要漏念。
    /// </summary>
    internal static class TextProc
    {
        /// <summary>
        /// &lt;br&gt; → 停顿。依据：text_inventory §4「换行式」3 条（zh）。
        /// 处理在最前，避免 &lt;br&gt; 被下面的尖括号标注规则吃成「br」。
        /// </summary>
        private static readonly Regex BrRe = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// &lt;size=…&gt; / &lt;/size&gt; → 删除（保留内部文字）。
        /// 依据：text_inventory §4「强调式」3 条 &lt;size=52&gt;…&lt;/size&gt;。
        /// </summary>
        // 实机踩过：确认对话框正文里有 `</size>` 这种**没有 = 值**的闭合标签，
        // 旧写法 `</?size=[^>]*>` 匹配不到，标签原样念给用户（日志里出现「…记忆</size>」）。
        // 所以等号段必须是可选的。
        private static readonly Regex SizeTagRe = new Regex(@"</?size(\s*=[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 自定义尖括号标注 → 去掉尖括号、保留词干（&lt;我&gt; →「我」）。
        /// 依据：text_inventory §4 自定义标注 20 条（&lt;我&gt;×16、&lt;汐萝&gt;×1 等）。
        /// 名字部分限定为「不含 = 与 / 的连续字符」，这样 &lt;size=52&gt; 与 &lt;/size&gt;
        /// 不会被这条规则误伤（它们分别含 = 与前导 /）。
        /// 渲染意图未实机验证 —— 尚未实机复测，按「保留词干」处理（多念不漏念）。
        /// </summary>
        private static readonly Regex AngleAnnotRe = new Regex("<([^<>=/][^<>=/]*)>", RegexOptions.Compiled);

        private static readonly Regex MultiSpaceRe = new Regex(@"[ \t\u3000]+", RegexOptions.Compiled);

        // ==================== 对外入口 ====================

        /// <summary>
        /// 说话人：本作名字是干净的（BacklogData.Name = actor.name，旁白为空串），
        /// 不做包装剥离，只修剪空白。遮蔽名/多人同框本作没有（text_inventory §4 无对应形态）。
        /// </summary>
        public static string Speaker(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return raw.Trim();
        }

        /// <summary>台词正文：把原始文本转成可朗读文本。</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = raw;

            s = BrRe.Replace(s, "，");      // 依据：换行式 3 条 → 读成停顿
            s = SizeTagRe.Replace(s, "");   // 依据：强调式 3 条 → 去标签留正文
            s = AngleAnnotRe.Replace(s, " $1");  // 依据：自定义标注 20 条 → 去括号留词
            // 真实换行 7 条：留在文本里的 \r\n 统一读成空格（沿用骨架规则）

            s = s.Replace("\r", "").Replace("\n", " ");
            s = MultiSpaceRe.Replace(s, " ");
            return s.Trim();
        }

        /// <summary>
        /// 这一行要不要朗读。本作没有「纯指令行 / 行内注释行」形态（text_inventory §4），
        /// 只过滤空行。
        /// </summary>
        public static bool ShouldRead(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            return true;
        }
    }
}
