$ErrorActionPreference = 'Stop'
$output = Join-Path (Split-Path $PSScriptRoot) 'dist\DesktopEcosphere-v0.9'
$transfer=Get-Content "$output\smoke-settings-transfer.json" -Raw | ConvertFrom-Json
if(!$transfer.import -or !$transfer.export -or !$transfer.reset -or !$transfer.draftIsolated) { throw '设置导入导出、恢复默认或草稿隔离失败' }
Write-Host 'PASS 设置界面导入导出、恢复默认、草稿隔离'
foreach ($scene in @('riverscape','reefscape')) {
    $report = Get-Content "$output\smoke-$scene.json" -Raw | ConvertFrom-Json
    if ($scene -eq 'riverscape') {
        if ($report.stopped.loop.running -or $report.stopped.loop.pending) { throw "$scene 暂停后仍在调度" }
        if ($report.resumed.renderedFrames -le $report.stopped.renderedFrames) { throw "$scene 恢复后未增加帧数" }
    } else {
        if ($report.stopped.effectiveFPS -ne 0 -or $report.stopped.scheduled) { throw "$scene 暂停后仍在调度" }
        if ($report.resumed.frames -le $report.stopped.frames) { throw "$scene 恢复后未增加帧数" }
        if ($report.stopped.food -le 0) { throw "$scene 投食未生效" }
    }
    Write-Host "PASS $scene 渲染、暂停、恢复"
    $light=Get-Content "$output\smoke-lighting-$scene.json" -Raw | ConvertFrom-Json
    if($light.bright -le $light.dark*1.15 -or !$light.stopped) { throw "$scene 灯光图像或暂停测试失败" }
    Write-Host "PASS $scene 灯光实际图像、保持暂停"
    $profiles=Get-Content "$output\smoke-population-$scene.json" -Raw | ConvertFrom-Json
    if($profiles.Count -ne 3) { throw "$scene 密度档位测试不完整" }
    foreach($profile in $profiles) { if($profile.actual -ne $profile.expected -or !$profile.healthy) { throw "$scene 密度档位验证失败" } }
    Write-Host "PASS $scene 三档鱼群密度和切换渲染"
    $interaction=Get-Content "$output\smoke-interaction-$scene.json" -Raw | ConvertFrom-Json
    if(!$interaction.blocked -or !$interaction.enabled -or !$interaction.portions) { throw "$scene 互动或份量验证失败" }
    Write-Host "PASS $scene 鼠标互动开关、投食份量"
    $power = Get-Content "$output\smoke-power-$scene.json" -Raw | ConvertFrom-Json
    if(!$power.displayOff -or !$power.batteryPaused -or !$power.limited -or !$power.resumed -or !$power.simulated) { throw "$scene 模拟电源集成验证失败" }
    Write-Host "PASS $scene 模拟关屏、电池暂停、15 FPS 限制、恢复"
}
$desktop = Get-Content "$output\smoke-desktop.json" -Raw | ConvertFrom-Json
if ($desktop.skipped) { Write-Warning '当前会话无可用 WorkerW，桌面挂载未验证' }
elseif (!$desktop.attached -or !$desktop.detached -or !$desktop.independentScene -or !$desktop.recovered) { throw '桌面挂载、独立场景、恢复或还原失败' }
else { Write-Host "PASS 桌面挂载、独立场景、失效窗口恢复、返回预览（$($desktop.screenCount) 个显示器）" }
if(!$desktop.skipped) {
    $wallpaperLighting=Get-Content "$output\smoke-wallpaper-lighting.json" -Raw | ConvertFrom-Json
    if(!$wallpaperLighting.reloadRetained) { throw '壁纸重载灯光丢失' }
    Write-Host 'PASS 壁纸重载后灯光恢复'
    $diagnostics = Get-Content "$output\smoke-diagnostics.json" -Raw | ConvertFrom-Json
    if($diagnostics.surfaces.Count -ne $desktop.screenCount) { throw '诊断显示器数量不一致' }
    foreach($surface in $diagnostics.surfaces) { if(!$surface.attached -or !$surface.boundsValid -or $surface.dpi -le 0) { throw '诊断中的尺寸或 DPI 无效' } }
    Write-Host 'PASS 多屏诊断导出'
}
