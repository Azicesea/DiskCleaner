# P2 树图下钻验证：扫 E 盘 → 切树图 → 点最大块 → 截图 → 检查返回按钮
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
$ep = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
$ep.Expand()
Start-Sleep -Milliseconds 500
$itemCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
foreach ($it in $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCond)) {
    if ($it.Current.Name -like 'E:*') {
        $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Write-Output ("selected: " + $it.Current.Name)
        break
    }
}
$ep.Collapse()

# 扫描
$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '开始扫描')
$win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'scan clicked'
Start-Sleep -Seconds 6

# 切树图
$nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '树图')
$radioCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::RadioButton)
$andCond = New-Object System.Windows.Automation.AndCondition($nameCond, $radioCond)
$map = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $andCond)
if (-not $map) { $map = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond) }
Write-Output ("map element: " + $map.Current.ControlType.ProgrammaticName + " pattern available: " + $map.Current.Name)
try {
    $tg = $map.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($tg.Current.ToggleState -ne 'On') { $tg.Toggle() }
} catch {
    $map.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
Write-Output 'treemap on'
Start-Sleep -Seconds 1

# 用「返回上级」按钮和窗口矩形推算 canvas 位置（WPF Canvas 不在 UIA 树里）
$backCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '← 返回上级')
$back = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $backCond)
if (-not $back) { Write-Output 'ERROR: back button not found'; exit 1 }

$br = $back.Current.BoundingRectangle
$wr = $win.Current.BoundingRectangle
Write-Output ("window: " + $wr.ToString() + "  back: " + $br.ToString())

$canvasTop = $br.Bottom + 10
$canvasLeft = $br.X - 4
$canvasBottom = $wr.Bottom - 55
$canvasRight = $wr.Right - 40
Write-Output ("canvas est: " + $canvasLeft + "," + $canvasTop + " -> " + $canvasRight + "," + $canvasBottom)

$px = [int]($canvasLeft + ($canvasRight - $canvasLeft) * 0.2)
$py = [int]($canvasTop + ($canvasBottom - $canvasTop) * 0.45)
Write-Output ("click at " + $px + "," + $py)

$sig = '[DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);'
$t = Add-Type -MemberDefinition $sig -Name 'MouseSim' -Namespace 'Win' -PassThru
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($px, $py)
Start-Sleep -Milliseconds 300
$t::mouse_event(0x0002, 0, 0, 0, 0)
Start-Sleep -Milliseconds 80
$t::mouse_event(0x0004, 0, 0, 0, 0)
Write-Output 'clicked'
Start-Sleep -Seconds 2

# 检查返回按钮
$backCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '← 返回上级')
$back = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $backCond)
if ($back) { Write-Output ("back button enabled: " + $back.Current.IsEnabled) }

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_drill.png')
$g.Dispose(); $bmp.Dispose()
Write-Output 'saved: E:\DSH\Clean\tmp\gui_drill.png'
Write-Output ("pid=" + $p.Id)
