# BepInEx\mono-bcl 用的完整 Mono 基础类库

> ⚠️ **本目录里的 22 个 `.dll` 不入版本库**（`.gitignore` 里 `mod/bcl/*.dll`，同 `bep_dl/` 的待遇）。
> 发布 zip 里已经带了它们；重新构建本作安装包时需要先把它们放回来。

## 为什么需要这个目录

本作的 Unity 运行时用的是**裁剪过的 Mono 基础类库（BCL）**：

- `The seven days i spent with you_Data\Managed\mscorlib.dll` 只有 2,679,808 字节（完整约 4.6 MB），
  里面**没有** `System.Reflection.Module.GetPEKind`；
- `System.Core.dll` 同样被裁剪，缺少 `System.Linq.IGrouping`2.get_Key`。

BepInEx 5 的预加载器是按完整 BCL 编译的，一进去就要调这两个 API，所以本作原样装 BepInEx 5 会在
`PreloaderPreMain()` 直接抛：

```
System.MissingMethodException: Method not found:
  void System.Reflection.Module.GetPEKind(PortableExecutableKinds&, ImageFileMachine&)
```

实测两轮定位（`BepInEx\preloader_*.log`）：

1. 只把完整的 mscorlib.dll 塞进去 → 过了 `GetPEKind`，立刻炸在
   `ConfigFile..ctor` 的 `IGrouping`2.get_Key；
2. 换成整套完整 BCL（本目录 22 个 dll） → 预加载器正常，插件正常装载。

解决办法就是用 UnityDoorstop 的 `dll_search_path_override` 让 Mono 从一个**额外目录**加载 BCL：
`doorstop_config.ini` 里写 `dll_search_path_override = BepInEx\mono-bcl`（安装包与 `build.ps1` 都会写好）。

## 怎么拿到这些 dll

任选其一：

1. **从发布 zip 里取**（推荐，最省事）：
   解压 `SevenDaysForAccessibility-*.zip`，把 `BepInEx\mono-bcl\` 整个目录复制到本目录。
2. 从任意**用 Unity 2022.x / 2021.3 的 Mono 后端**的游戏安装目录里，
   把 `<Game>_Data\Managed\` 下的 System 类程序集拷过来（至少要有 `mscorlib.dll`、
   `System.dll`、`System.Core.dll`、`System.Xml.dll`、`System.Configuration.dll`、
   `System.Data.dll`、`Mono.Security.dll`、`netstandard.dll`，其余按缺失补）。
3. 从 Unity 编辑器安装目录的 `Editor\Data\MonoBleedingEdge` 里取对应版本的类库。

放好后 `SHA256SUMS.txt` 可以对哈希。

## 这些 dll 能不能分发

这些是 Mono 项目自己的类库程序集（Mono 类库是 MIT 许可，见 `..\licenses\MIT-Mono.txt`），
不是游戏资源、不是反编译产物、不含任何游戏内容。本仓库随包分发是合规的（附 MIT 全文与来源说明）。

## 换版本要小心

替换这里任何 dll 都要重新跑一遍冒烟：`BepInEx\LogOutput.log` 里必须出现
`Preloader finished`、`[a11y] loaded`，缺一不可。