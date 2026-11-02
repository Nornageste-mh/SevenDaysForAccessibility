# 在**当前** PowerShell 会话里设置 A11Y_GAMEDIR，供 tools\compile_check.ps1
# 的默认 -GameDir 读取。换机器/换会话时改这里的值，或直接跑
#   powershell -File tools\compile_check.ps1 -GameDir "<游戏目录>"
# 本文件是 --gamedir 生成的一次性助手，不要提交真实的个人路径。
$env:A11Y_GAMEDIR = "D:\Steam\steamapps\common\我与你共度的七日"
Write-Host "A11Y_GAMEDIR = $env:A11Y_GAMEDIR"
