# 更新日志

> 版本号规则：**纯数字分段（例如 0.1.0.1），2~4 段**，必须与 `Plugin.cs` 的
> `const string Version` 和 csproj 的 `<Version>` **完全一致**。
> 含字母的写法（0.1.0a）会让 BepInEx 判定 `version is invalid` 并**静默跳过整个插件**
> —— 日志里只有一行 Warning，表现是模组完全没加载。想表达「第三版修订」用第四位：0.1.0.3。

## 0.0.0.1（骨架生成）

> 这是骨架自带的**示例条目**。第一次真正发版时，把它改成上面规则允许的版本号
> （与插件版本一致），并重写下面每一条；不要保留「TODO」字样发出去。

- 由 A11yFramework 生成 `SevenDaysForAccessibility` 骨架（当前插件版本占位 `0.1.0.0`）。
- 未复测声明：TODO 本版本尚未在实机上按 `docs/templates/测试问卷.md` 复测
  （或：已复测，环境/结论写在这里）。
- 平台层从 Framework 整份拷贝、未修改；L1 文件清单见 `tools/l1-files.txt`。
- 已知问题：TODO 与 README 的「已知问题」保持一致。
