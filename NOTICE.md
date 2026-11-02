# 第三方组件与免责声明 / Third-Party Notices

> 根目录 [`LICENSE`](LICENSE)（MIT）**只覆盖本作自己的代码**；下表第三方组件不受它授权，
> 适用各自的许可条款，原文在 `mod\licenses\`（发布包里是 `licenses\`），随二进制一起分发。

## 1. 本作

- 名称：《我与你共度的七日》无障碍补丁（程序集 `SevenDaysForAccessibility`）
- 引擎路线：Unity Mono + BepInEx 5
- 本作代码许可：MIT，见根目录 [`LICENSE`](LICENSE)
- 平台层（Speech/Nvda/Zdsr/Sapi/UiNav/Hotkey/KeyEdge/Repeat/GlobalKeys）与契约层 `A11yHost.cs`
  来自 [A11yFramework](https://github.com/Nornageste-mh/Framework)，同样 MIT
- 本项目是**非官方**第三方无障碍补丁

## 2. 随包分发的第三方二进制

| 组件 | 版本 | 许可 | 许可证文本 |
|---|---|---|---|
| BepInEx | 5.4.23.5 | MIT | licenses/MIT-BepInEx.txt |
| HarmonyX | 随 BepInEx 5（本作**运行时不使用**，仅 BepInEx 自带） | MIT | licenses/MIT-HarmonyX.txt |
| UnityDoorstop | 随 BepInEx 5（`winhttp.dll`、`.doorstop_version` 4.5.0） | LGPL-2.1 | licenses/LGPL-2.1-UnityDoorstop.txt |
| NVDA Controller Client | nvdaControllerClient.dll（x64，263,832 字节，取自本机 NVDA 2026.2 安装） | LGPL-2.1 | licenses/LGPL-2.1-NVDA-ControllerClient.txt |
| **Mono 类库（BCL）** | 22 个程序集，见 `BepInEx\mono-bcl\` 与 `mod\bcl\SHA256SUMS.txt` | **MIT** | licenses/MIT-Mono.txt |

关于 Mono BCL：本作自带的 Mono 基础类库被裁剪（`mscorlib.dll` 2,679,808 字节，缺
`Module.GetPEKind`；`System.Core.dll` 缺 `IGrouping`2.get_Key`），BepInEx 5 预加载器按完整 BCL 编译，
必须附一套完整的 Mono 类库并用 UnityDoorstop 的 `dll_search_path_override` 指向它。
这些程序集是 Mono 项目的开源类库（MIT），**不含任何游戏内容**，也不是从游戏文件反编译/提取所得。
来源与哈希见 `mod\bcl\README.md` 与 `mod\bcl\SHA256SUMS.txt`。

**不随包分发**：争渡（ZDSR）运行库 `ZDSRAPI.dll`（专有软件，由用户自行安装争渡读屏获得）、
`BepInEx\interop\`、`BepInEx\unity-libs\`、玩家自己的配置文件。

## 3. 非官方声明

本软件是非官方的第三方无障碍辅助项目，与《我与你共度的七日》的开发商、发行商、平台
及任何相关权利方**不存在**隶属、合作、赞助、授权或背书关系。相关商标、作品名称
与内容的全部权利归各自权利人所有。

## 4. 免责

本软件按「现状」提供，不附带任何明示或默示担保。在适用法律允许的最大范围内，
作者与贡献者对因使用或无法使用本软件产生的任何直接、间接、附带、特殊或后果性
损害不承担责任，包括但不限于存档损坏/丢失、游戏无法启动或崩溃、账号被限制、
与服务条款或 EULA 相关的争议。是否使用由你自行决定并承担全部风险。

## 5. 禁止随包的内容

发布包**不得**包含：`BepInEx\interop\`、`BepInEx\unity-libs\`、反编译/解包产物、
游戏原始资源与剧本、含台词原文的日志、开发机配置（`BepInEx\config\sevendays.a11y.reader.cfg`）。
这是 IP 与合规红线，`mod\build.ps1` 会机械拦截。