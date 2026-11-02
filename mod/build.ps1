#Requires -Version 5.1
<#
    可复现构建：下载依赖 → 编译插件 →（IL2CPP）部署 → 组装安装包

    用法：
        cd <本文件所在的 mod 目录>
        .\build.ps1                        # 默认游戏目录（D:\Steam\steamapps\common\我与你共度的七日）
        .\build.ps1 -GameDir "D:\...\The seven days i spent with you"
        .\build.ps1 -NoDeploy              # IL2CPP：只编译打包，不写进游戏目录

    产物：
        .\package\    ← 直接拷进游戏根目录即可用

    安装包不含安装程序：补丁是 BepInEx 运行时挂载的，不修改游戏文件，
    安装就是「把 package\ 里的东西拷进有 The seven days i spent with you.exe 的那一层」。
    手写说明见 package\安装说明.md。

    两条引擎路线共用本模板，由 tools\new_game.py 在生成时按 @@ROUTE 标记裁掉
    不属于本作的那一半：
        mono  / BepInEx 5.4.23.5   ← 插件基类 BaseUnityPlugin
        il2cpp / BepInEx 6.x       ← 插件基类 BepInEx.Unity.IL2CPP.BasePlugin

    安装包里带了第三方二进制（BepInEx、UnityDoorstop、HarmonyX、Mono.Cecil、
    MonoMod、NVDA Controller Client），它们的许可证与来源说明放在 licenses\，
    由本脚本从 .\licenses\ 复制进去（LGPL-2.1 要求随二进制分发附上许可证文本）。

    注意：本脚本必须保存为 UTF-8 带 BOM —— Windows PowerShell 5.1 读无 BOM 的
    脚本会按 GBK 解码，中文注释变乱码并直接语法错误。
#>
[CmdletBinding()]
param(
    [string]$GameDir = "D:\Steam\steamapps\common\我与你共度的七日",
    # 依赖版本（如需升级改这里）
    [string]$BepInExVersion = "5.4.23.5",
    [string]$NvdaClientUrl  = "https://download.nvaccess.org/releases/stable/nvda_2026.2_controllerClient.zip",
    # 复用已有的下载缓存（可选）：设 A11Y_CACHE=某个 mod 目录即可，没有就自己下
    [string]$CacheDir = "$env:A11Y_CACHE"
)

$ErrorActionPreference = "Stop"
$mod = $PSScriptRoot
$repoRoot = Split-Path -Parent $mod
$dl  = Join-Path $mod "dl"
New-Item -ItemType Directory -Force -Path $dl | Out-Null

function Step($msg) { Write-Host "`n>>> $msg" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }

# ------------------------------------------------------------
Step "1/5  检查游戏目录"
if (-not (Test-Path -LiteralPath (Join-Path $GameDir "The seven days i spent with you.exe"))) {
    Write-Host "    找不到游戏：$GameDir" -ForegroundColor Red
    Write-Host "    用 -GameDir 参数指定游戏根目录。"
    exit 1
}
Ok $GameDir


# ------------------------------------------------------------
Step "2/5  获取 BepInEx $BepInExVersion (win x64)"
$bepZip = Join-Path $dl "BepInEx_win_x64_$BepInExVersion.zip"
$bepDir = Join-Path $mod "bep_dl"
$bepReady = Join-Path $bepDir "BepInEx\core\BepInEx.dll"
if (Test-Path -LiteralPath $bepReady) {
    Ok "已存在，跳过下载"
} else {
    if (-not (Test-Path -LiteralPath $bepZip)) {
        $cacheZip = if ($CacheDir) { Join-Path $CacheDir "dl\BepInEx_win_x64_$BepInExVersion.zip" } else { "" }
        if ($cacheZip -and (Test-Path -LiteralPath $cacheZip)) {
            Copy-Item $cacheZip $bepZip -Force
            Ok "复用缓存 $cacheZip"
        } else {
            $url = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"
            Invoke-WebRequest -Uri $url -OutFile $bepZip -UseBasicParsing -TimeoutSec 180
        }
    }
    if (Test-Path -LiteralPath $bepDir) { Remove-Item $bepDir -Recurse -Force }
    Expand-Archive -LiteralPath $bepZip -DestinationPath $bepDir -Force
    Ok "已解压到 bep_dl\"
}

# ------------------------------------------------------------
Step "3/5  获取 NVDA Controller Client (x64)"
$nvdaZip = Join-Path $dl "nvda_controllerClient.zip"
$nvdaDir = Join-Path $mod "nvda_dl"
$nvdaDll = $null
if (Test-Path -LiteralPath $nvdaDir) {
    $nvdaDll = Get-ChildItem $nvdaDir -Recurse -File -Filter "nvdaControllerClient.dll" |
               Where-Object { $_.DirectoryName -like "*x64*" } | Select-Object -First 1
}
if ($nvdaDll) {
    Ok "已存在，跳过下载"
} else {
    $cacheNvda = if ($CacheDir) { Join-Path $CacheDir "nvda_dl" } else { "" }
    if ($cacheNvda -and (Test-Path -LiteralPath $cacheNvda)) {
        New-Item -ItemType Directory -Force -Path $nvdaDir | Out-Null
        Copy-Item (Join-Path $cacheNvda "*") $nvdaDir -Recurse -Force
        Ok "复用缓存 $cacheNvda"
    } else {
        if (-not (Test-Path -LiteralPath $nvdaZip)) {
            Invoke-WebRequest -Uri $NvdaClientUrl -OutFile $nvdaZip -UseBasicParsing -TimeoutSec 300
        }
        Expand-Archive -LiteralPath $nvdaZip -DestinationPath $nvdaDir -Force
    }
    $nvdaDll = Get-ChildItem $nvdaDir -Recurse -File -Filter "nvdaControllerClient.dll" |
               Where-Object { $_.DirectoryName -like "*x64*" } | Select-Object -First 1
    if (-not $nvdaDll) { Write-Host "    找不到 x64\nvdaControllerClient.dll" -ForegroundColor Red; exit 1 }
    Ok $nvdaDll.FullName
}

# ------------------------------------------------------------
Step "4/5  编译插件"

# 发布前守卫：版本号必须是纯数字四段。
# 它不是给人看的显示名 —— 填 "0.5.8a" 这类带字母的写法，BepInEx 会判定
# 「version is invalid」并**静默跳过整个插件**：日志里只有一行 Warning，
# 表现是模组完全没加载、游戏里一片安静，极难排查（上一作 v0.5.8a 踩过）。
# 想表达 0.5.8a 这种「第五版修订」，用第四位数字：0.5.8.1。
$srcDir  = Join-Path $mod "src\SevenDaysForAccessibility"
$pluginCs = Join-Path $srcDir "Plugin.cs"
# 优先读 const string Version（两条路线的模板都这么写，BepInPlugin 里引用它）；
# 老仓库若还是字面量写法，退回旧正则。
$m = Select-String -LiteralPath $pluginCs -Pattern 'const\s+string\s+Version\s*=\s*"([^"]+)"' | Select-Object -First 1
if (-not $m) {
    $m = Select-String -LiteralPath $pluginCs -Pattern 'BepInPlugin\([^)]*"([^"]+)"\s*\)' | Select-Object -First 1
}
if (-not $m) { Write-Host "    在 Plugin.cs 里找不到版本号（const Version 或 BepInPlugin 字面量）" -ForegroundColor Red; exit 1 }
$pluginVer = $m.Matches[0].Groups[1].Value
$parsed = $null
if (-not [System.Version]::TryParse($pluginVer, [ref]$parsed)) {
    Write-Host "    BepInPlugin 版本号 `"$pluginVer`" 不是合法版本号。" -ForegroundColor Red
    Write-Host "    BepInEx 会因此跳过整个插件（日志：version is invalid）。"
    Write-Host "    请改成纯数字形式，例如 0.1.0.1"
    exit 1
}
try {
    $asmVer = ([xml](Get-Content -LiteralPath (Join-Path $srcDir "SevenDaysForAccessibility.csproj") -Raw)).Project.PropertyGroup.Version
    if ($asmVer -and $asmVer.Trim() -ne $pluginVer) {
        Write-Host "    版本号不一致：csproj=$asmVer  BepInPlugin=$pluginVer" -ForegroundColor Red
        Write-Host "    两处必须一致，否则插件版本与程序集版本会对不上。"
        exit 1
    }
} catch { Write-Host "    （csproj 版本号读取失败，已跳过一致性检查）" -ForegroundColor Yellow }
Ok "版本号 $pluginVer（合法，且与 csproj 一致）"

Push-Location $srcDir
try {
    dotnet build -c Release -v minimal -nowarn:MSB3277 -p:SevenDaysGameDir="$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "编译失败" }
} finally { Pop-Location }
$pluginDll = Join-Path $srcDir "bin\Release\SevenDaysForAccessibility.dll"
if (-not (Test-Path -LiteralPath $pluginDll)) { Write-Host "    找不到编译产物" -ForegroundColor Red; exit 1 }
Ok $pluginDll


# ------------------------------------------------------------
Step "5/5  组装安装包"
$pkg = Join-Path $mod "package"
# 保留自产的说明文件，其余重建
$keep = @("安装说明.md", "常见问题.md")
$saved = @{}
foreach ($k in $keep) {
    $p = Join-Path $pkg $k
    if (Test-Path -LiteralPath $p) { $saved[$k] = Get-Content -LiteralPath $p -Raw -Encoding UTF8 }
}
if (Test-Path -LiteralPath $pkg) { Remove-Item $pkg -Recurse -Force }
New-Item -ItemType Directory -Force -Path $pkg | Out-Null
foreach ($k in $saved.Keys) {
    [System.IO.File]::WriteAllText((Join-Path $pkg $k), $saved[$k], (New-Object System.Text.UTF8Encoding $true))
}

Copy-Item (Join-Path $bepDir "*") -Destination $pkg -Recurse -Force

$plugDir = Join-Path $pkg "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $plugDir | Out-Null
Copy-Item $pluginDll -Destination $plugDir -Force
Copy-Item $nvdaDll.FullName -Destination $plugDir -Force
# 同时放一份到包根：DllImport 会先查应用目录
Copy-Item $nvdaDll.FullName -Destination (Join-Path $pkg "nvdaControllerClient.dll") -Force

# ------------------------------------------------------------
# 本作特有：完整 Mono BCL（见 mod\bcl\README.md）。
# 本作的 Mono BCL 被裁剪过：mscorlib 没有 Module.GetPEKind、System.Core 缺
# IGrouping`2.get_Key，BepInEx 5 预加载器按完整 BCL 编译，原样装会在
# PreloaderPreMain() 直接 MissingMethodException。实测唯一解：把完整 BCL 放进
# BepInEx\mono-bcl，并让 UnityDoorstop 用 dll_search_path_override 指向它。
$bclSrc = Join-Path $mod "bcl"
if (-not (Test-Path -LiteralPath $bclSrc)) {
    Write-Host "    找不到 mod\bcl\（完整 Mono BCL）；本作没有它 BepInEx 起不来" -ForegroundColor Red
    exit 1
}
$bclDest = Join-Path $pkg "BepInEx\mono-bcl"
New-Item -ItemType Directory -Force -Path $bclDest | Out-Null
Copy-Item (Join-Path $bclSrc "*.dll") -Destination $bclDest -Force
if (-not (Test-Path -LiteralPath (Join-Path $bclDest "mscorlib.dll"))) {
    Write-Host "    BCL 里没有 mscorlib.dll，包不完整" -ForegroundColor Red
    exit 1
}
Ok "已放入完整 Mono BCL：BepInEx\mono-bcl（$((Get-ChildItem $bclDest -File).Count) 个 dll）"

# doorstop 配置里指向这个目录（幂等：有这行就改写，没有就追加）。
$dsCfg = Join-Path $pkg "doorstop_config.ini"
if (-not (Test-Path -LiteralPath $dsCfg)) {
    Write-Host "    doorstop_config.ini 不在包里，安装后补丁不会注入" -ForegroundColor Red
    exit 1
}
$dtxt = [System.IO.File]::ReadAllText($dsCfg)
$dpat = '(?m)^[ \t]*dll_search_path_override[ \t]*=.*$'
$dline = 'dll_search_path_override = BepInEx\mono-bcl'
if ([regex]::IsMatch($dtxt, $dpat)) {
    $dtxt = [regex]::Replace($dtxt, $dpat, $dline)
} else {
    $dtxt = $dtxt.TrimEnd("`r", "`n") + "`r`n" + $dline + "`r`n"
}
[System.IO.File]::WriteAllText($dsCfg, $dtxt, (New-Object System.Text.UTF8Encoding $false))
Ok "doorstop_config.ini：dll_search_path_override = BepInEx\mono-bcl"

# 根许可证与通知：LICENSE 只覆盖本仓库自己的代码；第三方二进制（BepInEx、
# UnityDoorstop、Il2CppInterop、NVDA）各自的文本在 licenses\，由 NOTICE.md 指路。
foreach ($f in @("LICENSE", "NOTICE.md")) {
    $p = Join-Path $repoRoot $f
    if (Test-Path -LiteralPath $p) { Copy-Item $p -Destination $pkg -Force }
    else { Write-Host "    警告：仓库根目录缺少 $f" -ForegroundColor Yellow }
}
$chg = Join-Path $repoRoot "CHANGELOG.md"
if (Test-Path -LiteralPath $chg) {
    Copy-Item $chg -Destination (Join-Path $pkg "更新日志.md") -Force
} else {
    Write-Host "    警告：仓库根目录缺少 CHANGELOG.md，安装包将没有 更新日志.md" -ForegroundColor Yellow
}

# 第三方许可证与来源说明（随二进制分发的合规要求）。
# ★ 拷内容而不是拷目录：若 $pkg\licenses 已存在，Copy-Item $licDir -Destination $pkg
#   会生成 $pkg\licenses\licenses 这种嵌套（上一作真的出现过）。
$licDir = Join-Path $mod "licenses"
if (Test-Path -LiteralPath $licDir) {
    $licDest = Join-Path $pkg "licenses"
    New-Item -ItemType Directory -Force -Path $licDest | Out-Null
    Copy-Item (Join-Path $licDir "*") -Destination $licDest -Recurse -Force
    Ok "已附上第三方许可证：licenses\（$((Get-ChildItem $licDest -Recurse -File).Count) 个文件）"
} else {
    Write-Host "    警告：找不到 licenses\，安装包将缺少第三方许可证" -ForegroundColor Yellow
}

# ------------------------------------------------------------
# 红线自检：安装包里绝不能出现 interop / unity-libs / 反编译工程 / 开发配置。
# 这些是 IP 与合规红线（见 README「IP 与合规」），发布前必须机械拦一道。
$forbiddenRel = @("BepInEx\interop", "BepInEx\unity-libs", "SevenDaysForAccessibilityProbe", "SevenDaysForAccessibilityProbe.dll")
$forbiddenExt = @(".pdb", ".cs", ".ps1", ".py", ".bin", ".nson", ".log", ".bundle", ".assets")
$bad = @()
foreach ($f in Get-ChildItem $pkg -Recurse -File -Force) {
    $rel = $f.FullName.Substring($pkg.Length).TrimStart('\')
    foreach ($x in $forbiddenRel) {
        if ($rel -like "$x\*" -or $rel -ieq $x) { $bad += "$rel（禁止路径）" }
    }
    if ($forbiddenExt -contains $f.Extension.ToLowerInvariant()) { $bad += "$rel（禁止扩展名）" }
    if ($f.Extension -ieq ".cfg" -and $rel -ine "BepInEx\config\BepInEx.cfg") {
        $bad += "$rel（只允许 BepInEx\config\BepInEx.cfg）"
    }
}
if ($bad.Count -gt 0) {
    Write-Host "    安装包里混进了不该发布的东西：" -ForegroundColor Red
    $bad | Select-Object -First 20 | ForEach-Object { Write-Host "      - $_" }
    exit 1
}
Ok "红线自检通过（无 interop / unity-libs / 反编译产物 / 开发配置）"

$n = (Get-ChildItem $pkg -Recurse -File -Force).Count
Ok "安装包就绪：$pkg（$n 个文件）"

Write-Host "`n构建完成。安装：把 $pkg 里的东西全部拷进游戏根目录（有 The seven days i spent with you.exe 的那一层）。`n" -ForegroundColor Cyan
