# P2 验证：扫 E 盘 → 切树图 / 大文件页 → 截图
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$exe = 'E:\DSH\Clean\src\DiskCleaner.App\bin\Release\net8.0-windows\DiskCleaner.App.exe'
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output 'ERROR: window not found'; exit 1 }
Write-Output ("window: " + $win.Current.Name)

# 选 E 盘
$comboCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$combo = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $comboCond)
if ($combo) {
    $ep = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $ep.Expand()
    Start-Sleep -Milliseconds 500
    $itemCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCond)
    $picked = $false
    foreach ($it in $items) {
        if ($it.Current.Name -like 'E:*') {
            $sp = $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $sp.Select()
            $picked = $true
            Write-Output ("selected: " + $it.Current.Name)
            break
        }
    }
    if (-not $picked) { Write-Output 'ERROR: E drive item not found' }
    $ep.Collapse()
    Start-Sleep -Milliseconds 300
}

# 点开始扫描
$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '开始扫描')
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $btn) { Write-Output 'ERROR: scan button not found'; exit 1 }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'scan clicked'
Start-Sleep -Seconds 6

function Save-Shot([string]$name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save($name)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("saved: " + $name)
}

# 切树图
$mapCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '树图')
$map = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $mapCond)
if ($map) {
    try {
        $tg = $map.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        if ($tg.Current.ToggleState -ne 'On') { $tg.Toggle() }
    } catch {
        $map.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    Write-Output 'treemap toggled'
} else { Write-Output 'ERROR: treemap radio not found' }
Start-Sleep -Seconds 2
Save-Shot 'E:\DSH\Clean\tmp\gui_treemap.png'

# 切大文件页
$tabCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '大文件')
$tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
if ($tab) {
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Write-Output 'bigfile tab selected'
}
Start-Sleep -Milliseconds 800
Save-Shot 'E:\DSH\Clean\tmp\gui_bigfiles.png'

Write-Output ("pid=" + $p.Id)
