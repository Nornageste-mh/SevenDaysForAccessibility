# 第三方组件与免责声明 / Third-Party Notices

> 本文件是 [A11yFramework](https://github.com/Nornageste-mh/Framework.git) 生成的骨架，
> **发布前必须按实际打进发布包的二进制逐项核对**，把 TODO 全部改掉。
> 根目录 [`LICENSE`](LICENSE)（MIT）**只覆盖本作自己的代码**；下表第三方组件不受它授权，
> 适用各自的许可条款，原文在 `mod/licenses/`（发布包里是 `licenses/`），必须随二进制分发。

## 1. 本作

- 名称：我与你共度的七日 无障碍补丁（程序集 `SevenDaysForAccessibility`）
- 引擎路线：mono
- 本作代码许可：MIT，见根目录 [`LICENSE`](LICENSE)
- 本项目是**非官方**第三方无障碍补丁

## 2. 随包分发的第三方二进制

| 组件 | 版本 | 许可 | 许可证文本 |
|---|---|---|---|
| BepInEx | 5.4.23.5（TODO 按实际包核对） | MIT | licenses/MIT-BepInEx.txt |
| HarmonyX | 随 BepInEx 5 | MIT | licenses/MIT-HarmonyX.txt |
| UnityDoorstop | 随 BepInEx 5（winhttp.dll） | LGPL-2.1 | licenses/LGPL-2.1-UnityDoorstop.txt |
| NVDA Controller Client | nvda_2026.2（TODO 核对） | LGPL-2.1 | licenses/LGPL-2.1-NVDA-ControllerClient.txt |

> TODO 发布前按 `package\` 里实际存在的文件重列：版本号从下载的 zip 名 / 程序集
> FileVersion 抄，不要沿用本骨架的示例值；新增或移除组件都要同步这张表。
> 若使用 `licenses/il2cppinterop-pr277.patch` 对应的修复版 Il2CppInterop.Runtime.dll，
> 还要在发布说明里写明来源（BepInEx 上游 PR #277）与修改内容。

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
游戏原始资源与剧本、开发机配置（`BepInEx\config\SevenDaysForAccessibility.cfg` 等）。这是 IP 与合规红线，
`mod\build.ps1` 与 `mod\release.ps1` 会机械拦截。
