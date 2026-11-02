# SevenDaysForAccessibility

本仓库是**专门针对 Steam 游戏《我与你共度的七日》（The seven days I spent with you）** 的屏幕阅读器
辅助模组（游戏内 BepInEx 插件，通过 Tolk / NVDA Controller Client /
争渡读屏（ZDSRAPI）/ SAPI 朗读）。

> ## 🎮 关于本作
>
> | | |
> |---|---|
> | 中文名 | **我与你共度的七日** |
> | 英文名 | The seven days I spent with you |
> | 开发 | **RErevue_studio** |
> | 发行 | **LoveStoryProject** |
> | Steam | [AppID 3946810](https://store.steampowered.com/app/3946810/) |
> | 发售 | 2026 年 6 月 16 日 |
>
> **这个模组是玩家自制的第三方工具，与 RErevue_studio / LoveStoryProject 没有任何关系，
> 也没有得到他们的授权或背书。** 游戏的著作权归他们所有，
> 本仓库只发布自己的补丁代码，不含任何游戏资源。

> ## 致 RErevue_studio 与 LoveStoryProject
>
> 我们欠你们一句道歉。
>
> 做这个模组的过程中，我们反编译了你们的游戏（`Assembly-CSharp.dll`），
> 用 UnityPy 扫描了 Addressables 里的本地化字符串表并把文本规模、富文本标签统计了出来
> —— **这些都没有事先问过你们。**
>
> 技术上这是绕不开的一步：这个游戏的剧情由 NodeCanvas 对话树驱动，台词全部走
> Unity Localization 的字符串表，选项按钮是运行时实例化的 uGUI 按钮，
> 不反编译就找不到能读、能点的位置。但**「必要」不等于「可以不打招呼」**。
> 我们做这件事的出发点不是不尊重这部作品，恰恰相反 ——
> 是因为觉得它值得被更多人玩到，才想让读屏玩家也能走完一遍。
>
> 已经做的补救：
>
> - 提取的字符串表与反编译的代码**从未进入版本库**，只在本机用于分析，
>   已被 `.gitignore` 排除，也**不随发布包分发**；仓库里没有一条台词原文
> - 仓库与发布包**不含任何游戏资源**，只有我们自己的补丁代码和文档
> - 发给玩家的文档里没有任何剧情内容，也没有泄露结局相关的信息
> - **如果你们认为任何部分不妥，请联系我们 —— 我们会立刻调整，或者整体下架**
>
> 也请读到这里、并且用得上这个模组的玩家：
> **它让你能玩上这款游戏，不是让你不必买这款游戏。请去买一份正版。**
> 这部作品是 RErevue_studio 与 LoveStoryProject 的。

> ## ⚠️ 郑重警告（请务必阅读）
>
> - 本项目是 **Vibe coding 产物**（AI 辅助生成），**非官方**，
>   与开发兼发行 **RErevue_studio / LoveStoryProject** **没有任何关系**。
> - **请务必支持正版：本辅助仅面向已在 Steam 购买《我与你共度的七日》的玩家。**
>   **强烈要求每一位使用者通过 Steam 购买正版游戏。** 我们坚决反对任何形式的盗版、
>   破解、未授权传播；请勿将本工具用于协助获取或游玩盗版副本。
>   **这个补丁是让你能玩上这款游戏，不是让你不必买它** ——
>   作者的劳动成果值得被正当地支持。
> - 本项目**不保证可用、不保证稳定**，代码可能存在各种问题（兼容性、稳定性、安全性等），
>   **无任何维护承诺**。使用风险自负，仅供个人学习/研究参考，请勿用于商业或分发牟利。
> - **建议在完全理解代码的前提下再使用**，自行承担一切后果。
> - 本仓库**不包含任何游戏资源**，只发布运行时补丁代码与文档。
> - **如您是 RErevue_studio 或 LoveStoryProject 的成员**，或本作的版权方，认为本仓库有任何不妥，
>   请联系我们，我们会立即配合调整或移除。

---

## 说明

本辅助的作用：让使用读屏软件的玩家，也能基本正常地游玩《我与你共度的七日》。

- 朗读**剧情文本**：旁白、主角「我」、配角台词。**有配音的台词只放游戏语音、不朗读**
  （语音已经在播，读屏再念一遍就是叠音），但整句会进重读缓冲区，
  按 `Backspace` 随时能把这一句的文字念出来
- 朗读**选项**，并用数字键 `1`-`9` 选择（游戏原本的选项按钮是运行时生成的，读屏无法定位）
- **限时选择**会播报「这是限时选项」，并提供「沉默」键 `0`
  （走的是游戏自己的超时分支，不会扩大可达范围）
- **历史回看**：按 `H` 打开，上下键逐条朗读，回车播放该条语音
  （游戏原本只能用鼠标滚轮上滑打开）
- **存读档**：按 `F5` / `F9`，或用 `Esc` 菜单进去；槽位会念日期、说话人、正文
- **主菜单 / 存读档 / 设置 / 是-否确认框**的键盘导航与朗读
  （游戏原本这些界面几乎只能鼠标点）
- **开机健康提示页**会主动播报（那一页没有任何可点控件，只有一行「点击屏幕任意地方进入游戏」）
- **语音后端按可用性自动挑**：Tolk → NVDA → **争渡读屏（ZDSR）** → SAPI

游戏版本：Unity 2021.3.31f1，Mono 后端。仅覆盖 **Steam 正式版**（AppID 3946810），
本作没有公开的独立试玩版。

> ⚠️ 本作有一样前作都没遇到的东西：**游戏自带的 Mono 基础类库被裁剪过**，
> BepInEx 5 原样装会直接崩在预加载器。发布包因此带了一套完整 Mono 类库
> （`BepInEx\mono-bcl\`，22 个 dll，**不能删**）。原因见「几条踩过的坑」。

---

## 安装（仅限正版玩家）

**最快的装法**：到 [Releases](../../releases) 下载整合包 zip，解压后把里面的东西
**整体**拷进游戏根目录 —— 本作的游戏根目录是**再进一层的 `game\`**：

```
<Steam>\steamapps\common\我与你共度的七日\game\       ← 有 The seven days i spent with you.exe 的那一层
```

外层那个 `SevenDaysLauncher.exe` 只是启动器，**不要**把补丁拷到它那一层。
zip 内容就是下文的 `mod\package\`，另多一个 `licenses\` —— 第三方组件的许可证与来源说明，**别删**。

想自己构建、或只想往已有的 BepInEx 里加一个插件，看下面。

补丁由 BepInEx 在运行时挂载，**不修改任何游戏文件**，所以安装就是「拷文件」。
把 `mod\package\` 里的东西**全部复制进 `game\`**，保持目录结构不变。四条容易踩的坑：

- `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version` 必须在**游戏根目录**（`game\`）
- `BepInEx\core\` 里 16 个文件一个都不能少
- **`BepInEx\mono-bcl\`（22 个 dll）不能删**，`doorstop_config.ini` 里的
  `dll_search_path_override = BepInEx\mono-bcl` 也不能改 —— 本作的 Mono BCL 是裁剪过的
- `nvdaControllerClient.dll` 放游戏根目录（Mono 查找 DLL 时先看应用目录）

如果已经装过别的 BepInEx 模组，只需要两个文件：
`SevenDaysForAccessibility.dll` → `BepInEx\plugins\`，`nvdaControllerClient.dll` → 游戏根目录。
**不要**覆盖对方已有的 `winhttp.dll` 和 `BepInEx\core\`；
如果对方的 `doorstop_config.ini` 没有 `dll_search_path_override`，照本包那份补上（或整份替换）。

卸载：删掉游戏目录下的 `winhttp.dll` 即完全失效；连同 `BepInEx\` 一并删掉就彻底干净。
存档在 `%USERPROFILE%\AppData\LocalLow\…`，不受影响。

详细步骤、按键表、配置说明、故障排查见 [`mod/package/安装说明.txt`](mod/package/安装说明.txt)。

> 再次提醒：请通过 Steam 购买正版《我与你共度的七日》后再使用本辅助。

---

## 主要快捷键

| 快捷键 | 功能 |
| --- | --- |
| `1` - `9` | 选择对应编号的选项（剧情选项、限时选项通用） |
| `0` | 限时选择里「立刻沉默」（= 游戏自己的超时分支，选最后一项） |
| `Backspace` | 重读当前这一句（有配音的台词也能重读） |
| `Tab` | 进入 / 退出界面导航模式 |
| `↑` `↓` | 上一项 / 下一项（历史回看打开时 = 上一条 / 下一条） |
| `←` `→` | 调整滑条 |
| `回车` / `空格` | 导航模式下激活控件；否则交给游戏推进剧情 |
| `Home` / `End` | 第一项 / 最后一项 |
| `PageUp` / `PageDown` | 切换面板组 |
| `H` | 打开历史回看（可在配置里改键） |
| `F5` / `F9` | 打开存档 / 读档界面（可在配置里改键） |

**游戏原生占用的键**（模组不会去抢，也不要拿来当重读键）：
`A` 自动模式、`S` 快进、`按住 Ctrl` 快进、`空格`/`回车`/小键盘回车/鼠标下滚 推进、
`Esc` 菜单、`鼠标上滚` 历史回看、`鼠标右键` 返回。

> 本作 `H` / `F5` / `F9` 都没有被游戏占用，所以默认就是单键；
> 配置项「历史回看按键」「存档界面按键」「读档界面按键」接受任意 KeyCode 名称，留空即关闭。

> **关于语音后端**：本作目前**没有**做「运行中切换后端」的功能键
> （平台层有 `Speech.CycleAvailable()`，但逐作层还没接），
> 要换后端请改配置「语音后端」后重启游戏。

---

## 已知问题 / 注意事项

- **有配音的台词不自动朗读**：本作女主等角色的台词带日配，语音已经在播，
  再让读屏念一遍就是叠音（也会和姊妹补丁的行为不一致）。
  这类行**整句进重读缓冲区**、只是不自动播报 —— 想听文字按 `Backspace` 补读。
  这同时是 Framework 的 `Repeat` 规则：`Note` 只记不念，有配音行唯一的朗读通路是重读键。
  台词有没有配音由运行时 `BacklogData.VoiceFileName` 逐行判定。
- 通过读取游戏运行时信息工作，**游戏更新后可能失效**（本作读的是 `UIManager` /
  `Backlog` / `DataController` 这些公开成员）
- 游戏把一部分界面**画成了图片**：健康提示页整页没有可点控件（模组只做播报），
  美术鉴赏页是图片（**不做画面描述**），设置页的「语言选择 / 配音」两个下拉框
  只能念出当前值（`中文，按钮`），行名读不到
- **限时选择的倒计时改不了**：本作中文安装路径下 Harmony 打不上补丁（见「几条踩过的坑」），
  没有补丁就改不了 `availableTime`。模组只做「播报 + `0` 沉默」，
  沉默走的是**游戏自己的超时分支**（选最后一项），不会让玩家到达本来到不了的分支
- 对话中**导航模式会自动退出**：因为不能拦游戏自己的「回车/空格推进」，
  在对话里开导航会让空格既推进又点控件，所以进入对话就退出导航。
  对话里请用 `1`-`9` 选项、`Backspace` 重读、`H` 历史回看
- 未覆盖所有界面与交互，下列内容还没做：画面口述（立绘 / CG / 场景）、视频口述影像、
  打字机逐字朗读（整行一次念完）、`重写此生`（会清进度）的端到端验证、
  存读档面板的 `PageUp`/`PageDown` 翻页实测
- **争渡这一档只验证到「接口可用性判定」**：开发机后来装了争渡（公益档），
  实测模组能加载 `ZDSRAPI_x64.dll` 并读到 `GetSpeakState() = 2`，
  因此按规则把它判成「不可用」并给出 `争渡：争渡没有运行，或当前是公益档/未授权（GetSpeakState=2）`
  的结论 —— **判定逻辑是对的，但「授权版能不能真的出声」还没有实证**。
  另外 ZDSRAPI 是争渡的专有软件，不随本包分发
- 点显器（盲文）输出**尚未接入**：`Zdsr.cs` 里有接口，但没接到朗读通路上

---

## 构建

```powershell
cd mod
.\build.ps1 -GameDir "D:\Steam\steamapps\common\我与你共度的七日\game"
```

会自动下载 BepInEx 5.4.23.5 (win x64) 与 NVDA Controller Client (x64)，
编译插件并组装 `mod\package\`。第三方二进制不入库，全靠这个脚本复现。
需要 .NET SDK（本项目用 8.0.404 验证过）。

> **`mod\bcl\` 里的 22 个 Mono 类库不入库**（同 `bep_dl\` 的待遇），
> 但本作没有它们就跑不起来。第一次构建前请按 [`mod/bcl/README.md`](mod/bcl/README.md)
> 把目录填好（最省事的办法：从发布 zip 里取 `BepInEx\mono-bcl\`）。

> `build.ps1` 与 `tools\check-staged.ps1` 是 `.ps1`，必须保存为
> **UTF-8 带 BOM** —— Windows PowerShell 5.1 读无 BOM 的脚本会按 GBK 解码，
> 中文注释变乱码并直接语法错误。提交闸门会拦住这种情况。

### 目录结构

```
.
├─ mod/
│  ├─ build.ps1                 下依赖 → 编译 → 组包（含 Mono BCL 注入）
│  ├─ bcl/                      完整 Mono 类库（22 个 dll，不入库，见其 README）
│  ├─ src/SevenDaysForAccessibility/
│  │  ├─ Plugin.cs              BepInPlugin、配置、契约填充、每帧 Tick
│  │  ├─ Watcher.cs             ★ 逐作层核心：轮询游戏公开状态 → 朗读/面板播报/热键
│  │  ├─ Choices.cs             选项捕获、公告、数字键、限时沉默键
│  │  ├─ TextProc.cs            <br> / <size> / 自定义 <> 标注 / 换行 的清洗规则
│  │  ├─ UiNav.Game.cs          逐作标签钩子（存读档槽、历史回看、设置滑条名）
│  │  ├─ A11yHost.cs            契约层（来自 A11yFramework）
│  │  ├─ Speech.cs / Nvda.cs / Zdsr.cs / Sapi.cs    语音后端链
│  │  ├─ UiNav.cs / Hotkey.cs / KeyEdge.cs / Repeat.cs / GlobalKeys.cs   平台层
│  │  └─ SevenDaysForAccessibility.csproj
│  └─ package/                  安装包（直接拷进 game\）
│     └─ 安装说明.txt           ← 用户文档，先看这个
│
├─ probe/                       只读探针（开发期取证，不进发布包）
└─ （以下目录不入库，见 .gitignore）
   extracted/    从游戏提取的本地化文本（版权内容）
   decompiled/   反编译的游戏代码（版权内容）
```

### 模组做了什么

本作**没有用 Harmony**（原因见下一节），全部靠每帧读游戏自己的状态：

| 功能 | 读的是哪个状态 |
|---|---|
| 朗读台词 / 旁白 / 任务文本 | `UIManager.Instance.LastBacklog()`（底层 `Backlog.AddLog` 是唯一收口） |
| 判定有没有配音 | `BacklogData.VoiceFileName` 是否为空 |
| 朗读选项 + 数字键选择 | `DialogueUGUI.optionsGroup` 下**处于激活状态**的子按钮；选中走 `btn.onClick.Invoke()` |
| 判定限时选项 | 同一组按钮的 `CanvasRenderer` 透明度是否在往下走 |
| 历史回看逐条朗读 | 反射 `Backlog.DataList` + `BacklogItem.Index/Data` |
| 存读档槽位标签 | `DataCheck`（含反射私有字段 `isSave` / `button` / `deleteButton`） |
| 设置页滑条名字 | 反射 `GameProject.Config` 的 6 个 `Slider` 字段 |
| 菜单 / 标题 / 确认框导航 | `UiNav` 每帧扫描 `Selectable`（平台层，无逐作挂载点） |
| 推进剧情 | **不改**：回车/空格照旧交给游戏，对话中自动退出导航模式 |

**判定「语音后端」的方式**：平台层按 Tolk → NVDA → 争渡 → SAPI 顺序试，
日志里打「语音后端: xxx」。

### 几条踩过的坑（改之前请先读）

- **中文安装路径下绝对不要用 Harmony**：路径含「我与你共度的七日」时，
  Harmony/MonoMod 给多数方法打补丁会抛
  `ExecutionEngineException: String conversion error: Illegal byte sequence encounted in the input`
  （`RuntimeAssembly.get_code_base` → `MMReflectionImporter.ImportReference`）。
  实测 10 个候选补丁点里只有 3 个能挂上，同一份 DLL 走 ASCII junction 时 15 个全挂
  —— 补丁数量随安装路径变化，不可交付。整套逐作层因此改成**轮询式、零 Harmony**。
- **本作 Mono BCL 是裁剪过的**：自带 `mscorlib.dll` 只有 2,679,808 字节、
  缺 `Module.GetPEKind`；`System.Core.dll` 缺 `IGrouping`2.get_Key`。
  BepInEx 5 预加载器按完整 BCL 编译，原样装会崩在 `PreloaderPreMain()`。
  修法是附一整套 Mono 类库，并用 UnityDoorstop 的 `dll_search_path_override` 指过去。
- **`Backlog.AddLog` 是台词、旁白、任务文本的唯一收口**：三条路都汇到
  `Backlog.AddLog(BacklogData)`。抓它就等于拿到全量文本，不用逐个剧情节点挂。
- **`optionsGroup` 里挂着一颗设计期占位按钮**（文字「测试用文本」，`activeInHierarchy=false`），
  不按激活状态过滤就会把选项数算错，并且每帧「重新捕获」导致选项公告刷屏。
- **选项出现时要清掉 `EventSystem` 的选中项**：游戏自己的焦点会让盲人惯用的「空格推进」
  直接选中第一个选项。模组在捕获选项时清一次。
- **`DataController.isSave` 是字段不是属性**：按属性反射永远拿到 null，存/读档界面会一律
  被念成「读档界面」。
- **`</size>` 这种没有 `=` 的闭合标签**：只匹配 `<size=…>` 会漏，确认框正文会多念一个 `</size>`。
- **`EventSystem.sendNavigationEvents` 不要动**：本作游戏自己读 `Input`；
  平台层只在捕获选项时清一次选中项，不改全局开关。
- **限时选择的倒计时是一条剧情分支，不是计时器**：到点会走「沉默」分支。
  把它设成永不超时等于删掉这个选项 —— 本作没有补丁可打，所以干脆只做播报 + 沉默键。
- **别用 PowerShell 的文本 cmdlet 改源码**：`Get-Content -Raw` 在
  Windows PowerShell 5.1 下按 ANSI 代码页读取无 BOM 的 UTF-8，中文会变乱码。

---

## 如何复现分析

分析过程用 Python（需 `UnityPy`）扫描 Addressables bundle、定位并导出 TextAsset，
反编译游戏逻辑（需 `ilspycmd`）：

```powershell
pip install UnityPy
python tools/analyze_text_inventory.py            # 本仓库不含此脚本，按 docs 里的做法自写
dotnet tool install --global ilspycmd
ilspycmd -p -o decompiled "<游戏目录>\The seven days i spent with you_Data\Managed\Assembly-CSharp.dll"
```

生成的 `extracted/`、`decompiled/` 均为游戏版权内容，**已被 .gitignore 排除，
请勿提交或分发**。本仓库不含任何一条台词原文。

---

## 合规说明

- 在绝大部分国家和地区，为自己使用而修改你**合法拥有**的软件通常是允许的；
  但把**修改后的软件提供给第三方**，以及**绕过技术保护措施**，
  在绝大部分国家和地区都是**不被允许的**。
  各国规定不尽相同，请以你所在地的法律为准。
- 本模组因此**不包含任何游戏资源**，只发布补丁代码；整合包（BepInEx 运行时 +
  补丁 + 说明 + 一套 Mono 类库）通过本仓库的 Releases 提供，其中**不含游戏本体、
  不含任何游戏资源**。
- 模组不绕过任何技术保护措施，不修改、不替换、不再分发游戏文件。
- 发布包里附带的 `BepInEx\mono-bcl\` 是 **Mono 项目的开源类库（MIT）**，
  不是从游戏里提取或反编译出来的东西，也不含任何游戏内容。

---

## 许可

补丁代码与文档采用 [MIT 许可](LICENSE) 发布，附加的署名要求与免责条款见 [NOTICE.md](NOTICE.md)。

第三方组件：BepInEx 5.4.23.5 与 HarmonyX / Mono.Cecil / MonoMod（**MIT**）、
UnityDoorstop（`winhttp.dll`）与 NVDA Controller Client（**LGPL-2.1**）、
Mono 类库（**MIT**）—— 详见 [NOTICE.md](NOTICE.md)。

> **BepInEx 5 是 MIT，BepInEx 6 才是 LGPL-2.1** —— 这一条在本仓库的
> `NOTICE.md` 与 `mod\licenses\` 里都是按 MIT 写的，别记反。

简要说明：

- **可自由使用、修改、再分发**（含商用），但须标注出处 —— 本仓库地址与作者
  Nornageste-mh；若你做了修改，须注明「已修改」，不得让人误以为修改版出自原作者。
- **本项目为非官方第三方工具**：本模组是非官方玩家自制补丁，与游戏开发商、发行商
  RErevue_studio / LoveStoryProject 无任何关系，未获其授权或背书。
  游戏著作权归他们所有，本仓库只发布补丁代码，不含任何游戏资源。
- **按「现状」提供，不承担任何责任**：因使用本软件导致的游戏存档损坏、游戏崩溃、
  账号受限、或与游戏厂商及任何第三方产生的争议与索赔，作者与贡献者均不负责。
  是否使用请自行判断并自担风险。

第三方组件的许可与出处详见 [NOTICE.md](NOTICE.md)。