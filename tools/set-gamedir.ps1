# 在**当前** PowerShell 会话里设置 A11Y_GAMEDIR，供 tools\compile_check.ps1
# 的默认 -GameDir 与 mod\build.ps1 读取。换机器时改这里的值，或直接
#   powershell -File tools\compile_check.ps1 -GameDir "<游戏目录>"
#   powershell -File mod\build.ps1 -GameDir "<游戏目录>"
#
# 注意：本作真正的 exe 在 Steam 公共目录的 **game\ 子目录**里
#   <Steam>\steamapps\common\我与你共度的七日\game\The seven days i spent with you.exe
# build.ps1 / csproj 的「游戏目录」= exe 所在目录（*_Data 与 BepInEx 部署目标都在这层）。
$env:A11Y_GAMEDIR = "D:\Steam\steamapps\common\我与你共度的七日\game"
Write-Host "A11Y_GAMEDIR = $env:A11Y_GAMEDIR"