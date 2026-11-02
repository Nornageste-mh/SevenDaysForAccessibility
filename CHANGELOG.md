# 更新日志

## 版本方案

| 形式 | 含义 |
|---|---|
| `v0.x.y` | **现行官方序列。** 标注（annotated）标签。`v1.0.0` 的目标是「功能完整且经实机验证稳定」 |

> 版本号字段必须是**纯数字分段**（2~4 段），并且 `Plugin.cs` 的
> `[BepInPlugin(... Version = "...")]` 与 csproj 的 `<Version>` 要**完全一致**。
> 带字母的写法（`0.1.0a`）会被 BepInEx 判定 `version is invalid` 并
> **静默跳过整个插件** —— 表现是模组完全没加载、游戏里一片安静，极难排查。
> 要表达「第一版修订」请用第四位数字：`0.1.0.1`。

---

## 版本映射表

| 现行标签 | 原代码版本 | 提交 | 主要内容 |
|---|---|---|---|
| `v0.1.0.0` | 0.1.0.0 | 见 `v0.1.0.0` 标签 | 首个可用版。轮询式读屏（零 Harmony）+ 完整 Mono BCL 注入；台词 / 选项 / 历史回看 / 存读档 / 设置 / 菜单导航 |

---

## v0.1.0.0 —— 首个可用版

**面向「想靠自己听、不靠别人念屏，走完《我与你共度的七日》」的读屏玩家。**

环境：Windows 11、NVDA 2026.x、游戏 Steam AppID 3946810（Unity 2021.3.31f1，Mono），
游戏安装在**含中文的路径**下（`…\Steam\steamapps\common\我与你共度的七日\game\`），
未做任何 ASCII 转换。

### 一、问题：这个游戏跟前作有四处不一样

| 想做的事 | 前作怎么做 | 本作为什么不行 |
|---|---|---|
| 挂 Harmony 补丁 | 按方法名挂 Prefix/Postfix | 中文安装路径下多数目标抛 `ExecutionEngineException: String conversion error`（实测 10 个目标只成功 3 个；同一份 DLL 走 ASCII junction 时 15 个全挂） |
| 直接装 BepInEx 5 | 自带完整 Mono BCL，装上就能跑 | 本作 BCL 被裁剪（`mscorlib` 缺 `Module.GetPEKind`、`System.Core` 缺 `IGrouping`2.get_Key`），预加载器直接崩 |
| 用 UiNav 读历史回看 | 条目是 uGUI 控件 | 历史回看条目**不是 Selectable**（纯文本列表），UiNav 只看到一个关闭按钮和一根滚动条 |
| 限时选项延长倒计时 | 改 `ref` 参数 / 改 `availableTime` | 没有补丁可打 → 改不了。只做播报 + 沉默键（走游戏自己的超时分支） |

### 二、数据里有什么、没有什么

| 想读的东西 | 数据在哪 | 结论 |
|---|---|---|
| 当前台词 + 说话人 + 有无配音 | `UIManager.Instance.LastBacklog()`（底层 `Backlog.AddLog(BacklogData)`） | 有。`BacklogData{ Name, Text, VoiceFileName }` |
| 选项文本与顺序 | `DialogueUGUI.optionsGroup` 的激活子按钮 → `GetComponentInChildren<Text>()` | 有。注意组里挂着一颗 inactive 占位按钮（文字「测试用文本」） |
| 限时选项 | 同一组按钮的 `CanvasRenderer` 透明度（游戏用 `SetMassAlpha` 淡出） | 只能**观察**，改不了时长 |
| 历史回看条目 | `Backlog.DataList`（私有自动属性，反射）+ `BacklogItem.Index/Data` | 有，但要自己做浏览模式 |
| 存读档槽位 | `DataCheck` 的 `noText/contentText/dayText/hourText`、`HasData`、私有 `path` | 有。`DataController.isSave` 是**字段**，反射要取字段 |
| 设置页滑条名字 | `GameProject.Config` 的 6 个 `Slider` 字段 | 有（行名是美术字图片，读不到，只能按对象身份对名字） |
| 面板开合 | `SettingController.IsOpen` / `Config.IsOpen` / `Backlog.IsOpen` / `SharedVariable.SaveLoadOpen` / `YesNoDialogue.IsOpen` | 有 |

### 三、做法：全部改成轮询，一行 Harmony 都不留

- 每帧读上面这些公开成员，算差分（新台词 / 新选项 / 面板边沿 / 页面变化）。
- 选项选中走游戏自己的按钮：`btn.onClick.Invoke()` → 游戏的 `Finalize` 路径，
  自带「防双选」和 0.2 秒动画。
- 对话中**自动退出 UiNav**（不能拦游戏推进，否则空格既推进又点控件）。
- 历史回看、存读档热键、重读键都是自己解析 `KeyCode` 的边沿检测。
- 平台层（Speech/Nvda/Zdsr/Sapi/UiNav/Hotkey/KeyEdge/Repeat/GlobalKeys）与契约层
  来自 A11yFramework，只改了命名空间。

### 四、实机修掉的问题

| 现象 | 后果 | 修法 |
|---|---|---|
| 选项公告重复 8 次以上 | 完全没法玩 | `optionsGroup` 里的 inactive 占位按钮被算成选项 → 捕获与指纹都跳过 `!activeInHierarchy` |
| 选项一出现按空格就把第一项选掉 | 盲人惯用的推进键变成误选 | 捕获选项时清空 `EventSystem.current` 选中项 |
| 选中后又念一遍整组选项、并误报「这是限时选项」 | 噪声 + 误导 | 选中后 2 秒抑制捕获 + 文本指纹 5 秒去重 + 限时判定延后 0.8 秒 |
| 有配音的台词完全不出声 | 大段剧情没有信息 | 改成默认朗读（配置可关）；关掉后仍进重读缓冲 |
| 按住 Ctrl 快进时每句都念 | 快进变成噪声 | `IsSkipping()` 同时看 `skipMode`、`IsSkip`、`Left/RightControl`、`S` |
| 停快进时同一句念 2-3 遍 | 重复 | 去掉第二个出口，只保留「补念最后一句」 |
| 存/读档界面一律念成「读档界面」 | 走错流程 | `DataController.isSave` 是字段不是属性，反射先取字段 |
| 确认框正文多念一个 `</size>` | 噪声 | `TextProc` 的 size 正则补上无 `=` 的闭合写法 |
| 设置页 6 个滑条念「未命名滑条」 | 不知道在调什么 | 按对象身份反射 `Config` 的 6 个 Slider 字段映射中文名 |
| 槽位标签出现「…重启。，」、`": "` 没本地化 | 别扭 | 去掉尾部标点，`": "` 换「，」 |

### 五、复测要点（开发机，NVDA，中文路径）

- 健康提示页 → 连按两次空格 → 标题页 `Tab` 导航 6 项 → 回车进游戏
- 台词逐句朗读；说话人前缀「我，」「汐萝，」；旁白无前缀；有配音台词也朗读
- 选项公告一次、数字键 `2` 选中 → 「已选择，…」、无重复、无限时误报
- 按 `H` 打开历史回看（50 条）→ 上/下逐条朗读 → 回车播放语音 → Esc 关闭
- `F5` 存档：13 项、槽位标签、在空槽 3 真实保存成功（13 → 14 项，多出删除按钮）
- `F9` 读档：空槽念「不可用」→ 选槽 3 → 确认框 → 「是」→ 面板关闭、读档后自动恢复朗读
- 设置页：19 项，6 个滑条念名字 + 百分比，开关念状态
- 标题页第 6 项「退出游戏」→ 进入二次确认 → 选「否」返回
- **关掉补丁**（把 `winhttp.dll` 改名）→ 游戏照常进健康提示页 → 标题页，无 `LogOutput.log`

### 六、未验证 / 已知限制

- **争渡（ZDSR）后端未实机验证**：开发机没装争渡，代码走平台层既有逻辑。
- **真实限时选项样本**：冒烟里没遇到，只做过 UI 层的限时判定（会播报 + `0` 沉默）。
- 存读档翻页（`PageUp` / `PageDown`）、美术鉴赏页内容、`重写此生` 端到端。
- 运行中切换语音后端的按键**没有接入**（平台层有 `CycleAvailable()`）。
- 点显器（盲文）输出没有接入。