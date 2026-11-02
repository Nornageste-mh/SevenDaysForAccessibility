# 更新日志

> 版本号规则：**纯数字分段（例如 0.1.0.1），2~4 段**，必须与 `Plugin.cs` 的
> `[BepInPlugin(... Version = "...")]` 和 csproj 的 `<Version>` **完全一致**。
> 含字母的写法（0.1.0a）会让 BepInEx 判定 `version is invalid` 并**静默跳过整个插件**
> —— 日志里只有一行 Warning，表现是模组完全没加载。

## 版本方案

- `0.1.0.x`：首个可用版与其修订。功能覆盖对话/选项/历史回看/存读档/设置/标题与菜单导航。
- `0.2.x`：功能补完（美术鉴赏页可用、更多本作特有形态）、配置项调整。
- `1.0.0.0`：功能完整、实机稳定，向盲人玩家公开推荐。

## 版本清单

| 版本 | 日期 | 说明 |
|---|---|---|
| 0.1.0.0 | 2026-11-02 | 首个可用版：轮询式读屏，中文安装路径下全链路实测通过 |

## 0.1.0.0

初版。**环境**：Windows 11、NVDA 2026.x、游戏 Steam appid 3946810（Unity 2021.3.31f1，Mono）、
安装路径 `…\Steam\steamapps\common\我与你共度的七日\game\`（含中文，未做 ASCII 转换）。

### 架构

- 平台层（L1）从 A11yFramework 整份拷贝，仅改命名空间；契约层 `A11yHost.cs` 同源。
- **逐作层完全不使用 Harmony**。原因：本作安装路径含中文时，Harmony/MonoMod 给多数方法打补丁会抛
  `ExecutionEngineException: String conversion error: Illegal byte sequence encounted in the input`
  （`RuntimeAssembly.get_code_base` → `MMReflectionImporter.ImportReference`）；
  实测 10 个候选补丁点只有 3 个能挂上，同一份 DLL 走 ASCII junction 时 15 个全挂
  → 补丁数量随安装路径变化，不可交付。改为**每帧轮询游戏公开状态**，行为与路径无关。
- 新台词 = `UIManager.Instance.LastBacklog()` 换对象（`Backlog.AddLog` 是台词/旁白/任务文本唯一收口）；
  选项 = `DialogueUGUI.optionsGroup` 里激活状态的子按钮；面板 = 各面板的 `IsOpen`；
  历史回看 = 反射 `Backlog.DataList`；存读档槽 = `DataCheck` + 私有 `isSave`。

### 本作的运行环境坑

- 游戏 Mono BCL 被裁剪：自带 `mscorlib.dll` 2,679,808 字节、缺 `Module.GetPEKind`；
  `System.Core.dll` 缺 `IGrouping`2.get_Key`。BepInEx 5 预加载器按完整 BCL 编译，原样装会崩
  `MissingMethodException: Module.GetPEKind`。
  修法：包内附 22 个 Mono BCL 程序集到 `BepInEx\mono-bcl\`，并用 UnityDoorstop 的
  `dll_search_path_override = BepInEx\mono-bcl` 指向它（BCL 为 Mono 类库，MIT，`licenses\MIT-Mono.txt`）。

### 功能

- 台词：说话人 + 正文逐句朗读；旁白无前缀；有配音的台词默认可朗读（可关）。
- 选项：公告「共 N 个选项。1，…。2，…。按数字键选择。」；`1`-`9` 走游戏自己的按钮 `onClick`
  （`Finalize` 路径，自带防双选）；选择后播「已选择，…」；限时选项播报并支持 `0` 沉默。
- 历史回看：`H` 打开，`↑`/`↓` 逐条朗读，`回车` 播放该条语音，`Esc` 关闭（游戏原生只能鼠标滚轮开）。
- 存读档：`F5`/`F9` 或菜单进入；槽位念「存档槽 3，空，回车保存到这里」/
  「读档槽 2，2026-08-29 11:23:30，汐萝，…，回车读取」；读档后自动恢复朗读。
- 界面导航：标题页/菜单/设置/存读档/游戏确认框均可用 `Tab` + `↑`/`↓` + `回车`；
  设置页 6 个滑条按对象身份对上名字并念百分比。
- 开机健康提示页无控件 → 主动播报「连按两次空格键进入游戏」。

### 实机修掉的问题

- 选项公告重复 8 次以上：`optionsGroup` 里挂着一颗设计期占位按钮（文字「测试用文本」，inactive），
  被当成选项计入 → 每帧判定「选项失效」→ 重新捕获 → 反复播报。
  修：捕获与指纹都跳过 `!activeInHierarchy` 的子物体。（现象→后果→复测：修复后只公告一次。）
- 选项出现时按空格会把第一项直接选掉：游戏自身的 EventSystem 焦点行为。
  修：捕获选项时清空 `EventSystem.current` 选中项。（复测：连按空格不再误选。）
- 选中选项后又念一遍同一组选项、并误报「这是限时选项」：游戏选中后把整组按钮淡出（alpha 1→0）期间
  子物体仍 active，被重新捕获，而 alpha<0.97 又被限时判定当成倒计时。
  修：选中后 2 秒抑制捕获 + 选项文本指纹 5 秒去重 + 限时判定延后 0.8 秒。（复测：正常。）
- 有配音的台词完全不出声 → 改为默认朗读，配置可关。
- 按住 Ctrl/S 快进时每句都念：游戏快进不改 `skipMode`。修：`IsSkipping()` 同时看
  `Input.GetKey(Left/RightControl)`、`S`。停快进只补念最后一句（单一出口，曾出现同句念 2-3 遍）。
- 存/读档界面一律念成「读档界面」：`DataController.isSave` 是**字段**不是属性，反射按属性取永远 null。
  修：先取字段再退回属性。
- 确认框正文出现 `</size>`：`TextProc` 的 size 标签正则只匹配带 `=` 的写法，漏了 `</size>`。
  修：`</?size(\s*=[^>]*)?>`。
- 设置页 6 个滑条念「未命名滑条」：按对象身份反射 `Config` 的 6 个 `Slider` 字段映射中文名。
- 存档槽标签出现「…重启。，」双标点、`": "` 未本地化：`SlotLabel` 里去掉尾部标点、把 `": "` 换「，」。

### 未验证 / 已知限制

- **争渡（ZDSR）后端未实机验证**：开发机未安装争渡，代码走 Framework 平台层既有逻辑。
- 限时选项无法延长倒计时（无 Harmony，见上）；只做播报 + `0` 沉默（=游戏超时分支）。
- 美术鉴赏（CG 查看）页不做画面描述。
- 「重写此生」未做端到端验证（会清进度，冒烟没有跑）。
- 打字机逐字动画期间不逐字朗读（整行一次念完，刻意）。