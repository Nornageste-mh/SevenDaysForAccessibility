# SevenDaysForAccessibility

> 《我与你共度的七日》（Steam appid **3946810**）的**读屏无障碍补丁**。
> 让 NVDA / 争渡 / SAPI 用户能用键盘、靠听来玩通本作。
> 平台层与契约层来自 [A11yFramework](https://github.com/Nornageste-mh/Framework)。

**当前版本 0.1.0.0** · Mono / BepInEx 5.4.23.5 · 纯增量补丁（不修改、不替换、不删除任何游戏文件）

## 这个补丁做了什么

覆盖范围（全部在中文安装路径下实机验证过）：

- **对话**：说话人 + 台词逐句朗读（旁白不加说话人前缀）。有配音的台词默认也朗读，配置里可以关掉。
- **选项**：选项弹出时播报「共 N 个选项。1，…。2，…。按数字键选择。」；按 `1`-`9` 直接选；选择后播报「已选择，…」。
  限时选项额外播报「这是限时选项」，按 `0` 立刻沉默（等同于游戏自己的超时分支，不会选到游戏本来选不到的分支）。
- **历史回看**：按 `H` 打开（游戏原生只有鼠标滚轮上滑），`↑`/`↓` 逐条朗读「第 N 条，说话人，正文」，`回车` 播放该条语音，`Esc` 关闭。
- **存读档**：`F5` 存档界面、`F9` 读档界面；也可以用 `Esc` 菜单或 `Tab` 导航过去。槽位会念「存档槽 3，空，回车保存到这里」或「读档槽 2，2026-08-29 11:23:30，汐萝，已经拉钩约定了！…，回车读取」；空槽在读档模式下念「不可用」。
- **界面导航**：标题页、Esc 菜单、设置页、存读档页、游戏自己的「是/否」确认框都能用 `Tab` 进导航、`↑`/`↓` 选、`回车` 激活。设置页 6 个滑条会念名字（文本显示速度 / 自动翻页速度 / 全局音量 / 音乐音量 / 音效音量 / 语音音量）和百分比。
- **开机健康提示页**：没有可点控件（游戏设计），补丁会主动播报「健康提示页，连按两次空格键进入游戏。」
- 启动时播报一句按键提示；`Backspace` 随时重读最近一句。

明确**不做**的事：

- 不做画面/CG/场景描述（本作是纯文字 + 立绘，盲人可玩性靠文字就够）
- 不绕过任何技术保护措施，不改存档格式，不改游戏平衡
- 不翻译：朗读的就是游戏当前语言的文本

## 安装

**请先看发布包里的 `安装说明.md`**（分步、含卸载）。要点：

1. 关闭游戏，把发布 zip 解压后的内容**整体**拷进有 `The seven days i spent with you.exe` 的那一层，即
   `Steam\steamapps\common\我与你共度的七日\game\`。
2. 里面有一个 `BepInEx\mono-bcl\` 目录（22 个 dll）—— **不能删**。原因见下面「实现说明」：
   本作的 Mono 基础类库被裁剪过，BepInEx 5 必须靠这套完整 BCL 才能起得来。
3. 启动游戏。听到「《我与你共度的七日》无障碍补丁已加载」就说明通了。

## 快捷键

| 快捷键 | 功能 |
|---|---|
| `1`-`9` | 选择对应编号的选项 |
| `0` | 限时选择里「立刻沉默」（=游戏超时分支，选最后一项） |
| `Backspace` | 重读最近朗读过的一段 |
| `Tab` | 进入 / 退出界面导航模式 |
| `↑` `↓` | 上一项 / 下一项（历史回看打开时 = 上一条 / 下一条） |
| `←` `→` | 调整滑条 |
| `回车` / `空格` | 导航模式下激活控件；对话中交给游戏推进剧情 |
| `Home` / `End` | 第一项 / 最后一项 |
| `PageUp` / `PageDown` | 切换面板组 |
| `H` | 打开历史回看 |
| `F5` / `F9` | 打开存档 / 读档界面 |

**游戏原生占用的键**（补丁不抢，也别拿去当重读键）：`空格` `回车` `小键盘回车` `鼠标下滚`（推进）、
`A`（自动）、`S`（快进）、`按住左/右 Ctrl`（快进）、`Esc`（菜单）、`鼠标上滚`（历史回看）、`鼠标右键`（返回/关闭）。

## 配置

`BepInEx\config\sevendays.a11y.reader.cfg`，装
[ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) 可以在游戏里改。三组：

- **朗读** —— 朗读有配音的台词（默认开）/ 朗读时带上说话人（默认开）/ 朗读选项（默认开）/
  数字键选择选项（默认开）/ 沉默按键（`0`）/ 重读按键（`Backspace`）/ 语音后端（自动 / Tolk / NVDA / 争渡 / SAPI）/
  快进停止时补念当前句（默认开）
- **界面导航** —— 菜单键盘导航 / 按屏幕位置排序 / 只导航看得见的控件 / 退出前二次确认 / 退出确认对象名（`Exit`）/ 排除的对象名
- **其它** —— 启动时播报 / 界面诊断日志（默认关）/ 历史回看按键（`H`）/ 存档界面按键（`F5`）/ 读档界面按键（`F9`）

> 语音后端的「自动」= Tolk → NVDA → 争渡 → SAPI 逐级降级，同时只有一个后端在发声。
> 钉死某个后端而它不可用时**不会回退**，这是刻意的（避免「明明选了 SAPI 却在用 NVDA」）。

## 构建（维护者）

```powershell
# 1. 告诉构建脚本游戏在哪（写进环境变量；这个文件含个人路径，不要提交）
powershell -File tools\set-gamedir.ps1

# 2. 构建：查游戏目录 → 取 BepInEx → 取 NVDA Controller Client → 编译 + 守卫 → 组装 mod\package\
cd mod
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "D:\Steam\steamapps\common\我与你共度的七日\game"

# 3. 打包（绝不用 Compress-Archive，zip 条目必须用正斜杠）
powershell -ExecutionPolicy Bypass -File .\tools\makezip.ps1 -Source .\package -Zip .\dist\SevenDaysForAccessibility-0.1.0.0.zip
```

`mod\bcl\` 是完整 Mono BCL 的固定副本（含 `SHA256SUMS.txt` 与来源说明）。`bep_dl\`、`nvda_dl\`、`dist\`、`package\` 是构建产物，不入库。

## 目录结构

```
mod\src\SevenDaysForAccessibility\   L1 平台层 9 个文件（逐字节复用 Framework）+ A11yHost 契约层 + 逐作层
  Watcher.cs                          逐作层核心：轮询游戏公开状态，产生全部朗读/面板播报/热键
  Choices.cs                          选项捕获、公告、数字键、限时沉默键
  TextProc.cs                         文本清洗（<br> / <size> / 自定义 <> 标注 / 换行）
  UiNav.Game.cs                       逐作标签钩子（存读档槽、历史回看、设置滑条名）
  Plugin.cs                           BepInPlugin、配置、契约填充、Tick
mod\bcl\                             完整 Mono BCL（原因见下）
probe\                               只读探针（开发期用，不进发布包）
tools\                               静态守卫 / 打包 / 环境脚本
docs\                                流水线文档副本
```

## 实现说明（为什么发布版一行 Harmony 都没有）

本作的安装路径含中文（`…\我与你共度的七日\game\`），而 Harmony/MonoMod 在这个路径下给多数方法打补丁会抛：

```
System.ExecutionEngineException: String conversion error: Illegal byte sequence encounted in the input.
  at System.Reflection.RuntimeAssembly.get_code_base
  at MonoMod.Utils.MMReflectionImporter.ImportReference(Assembly asm)
```

实测 10 个候选补丁点里只有 3 个能挂上（`Backlog.AddLog`、`DataController.LoadData(int)`、
`DialogueUGUI.OnLoadSaveData`），其余（选项请求、面板开关、输入推进）**全部失败**；同一份 DLL 走
ASCII 路径（junction）时 15 个补丁全挂。补丁挂载数量随安装路径变化，不可交付。

所以 0.1.0.0 起整个逐作层改成**每帧轮询游戏自己的公开状态**，完全不依赖 Harmony：

- 新台词 = `UIManager.Instance.LastBacklog()` 换了对象（`Backlog.AddLog` 是台词/旁白/任务文本的唯一收口）
- 选项 = `DialogueUGUI.optionsGroup` 下处于激活状态的子按钮（设计期还挂着一颗 inactive 占位按钮，已过滤）
- 面板 = `SettingController.IsOpen` / `Config.IsOpen` / `Backlog.IsOpen` / `SharedVariable.SaveLoadOpen` / `YesNoDialogue.IsOpen`
- 历史回看条目 = 反射 `Backlog.DataList`；存读档槽 = `DataCheck.hasData`/私有 `isSave`

代价（如实写在「已知问题」里）：拦不住游戏自己的推进、也没法把限时选项的倒计时改长。
好处是**在任何安装路径下行为一致**，而且不需要 Harmony 重写 IL。

另一个坑：本作的 Mono BCL 被裁剪（`mscorlib.dll` 2.6 MB，缺 `Module.GetPEKind`；`System.Core.dll`
缺 `IGrouping`2.get_Key`），BepInEx 5 预加载器按完整 BCL 编译，原样装会在 `PreloaderPreMain()` 崩。
发布包用 UnityDoorstop 的 `dll_search_path_override = BepInEx\mono-bcl` 让 Mono 从附带目录加载完整 BCL。
细节见 `mod\bcl\README.md`。

## 如何复现分析

```powershell
pip install UnityPy
dotnet tool install --global ilspycmd
ilspycmd -p -o decompiled "<游戏目录>\The seven days i spent with you_Data\Managed\Assembly-CSharp.dll"
```

`extracted\`、`decompiled\` 都是游戏版权内容，已被 `.gitignore` 排除，**不要提交、不要分发**。
本地化文本在 `StreamingAssets\aa\...\localization-string-tables-*.bundle`（Unity Localization + Addressables，
主表 2067 条），本仓库不保存任何一条台词原文。

## 合规

- 补丁**不包含任何游戏资源**：没有美术、音频、剧本原文、反编译代码；仓库里也没有游戏截图与含台词的日志。
- 不绕过技术保护措施；不修改、不替换、不再分发游戏文件（安装 = 往游戏目录**新增**文件）。
- 发布包里的第三方二进制与许可：BepInEx 5（MIT）、HarmonyX/Mono.Cecil/MonoMod（MIT）、
  UnityDoorstop `winhttp.dll`（LGPL-2.1）、NVDA Controller Client（LGPL-2.1）、Mono BCL（MIT）。
  全文在 `licenses\`。争渡（ZDSR）的运行库是专有软件，**不随包分发**。
- 仅限正版玩家。

## 许可

补丁代码与文档：MIT（见 `LICENSE`，第三方组件与出处见 `NOTICE.md`）。