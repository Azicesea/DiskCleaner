# 用 UIAutomation 驱动主窗口：选 E 盘 → 点开始扫描 → 截图
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$exe = 'E:\DSH\Clean\src\DiskCleaner.App\bin\Release\net8.0-windows\DiskCleaner.App.exe'
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Output 'ERROR: window not found'; exit 1 }
Write-Output ("window: " + $win.Current.Name)

$cbCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$cb = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cbCond)
if (-not $cb) { Write-Output 'ERROR: combo not found'; exit 1 }

$expand = $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
$expand.Expand()
Start-Sleep -Milliseconds 600

$liCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
$items = $cb.FindAll([System.Windows.Automation.TreeScope]::Descendants, $liCond)
Write-Output ("items: " + $items.Count)
foreach ($it in $items) {
    Write-Output ("  item: " + $it.Current.Name)
    if ($it.Current.Name -like 'E:*') {
        $sel = $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $sel.Select()
        Write-Output 'selected E'
    }
}
$expand.Collapse()
Start-Sleep -Milliseconds 400

$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '开始扫描')
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $btn) { Write-Output 'ERROR: button not found'; exit 1 }
$inv = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$inv.Invoke()
Write-Output 'scan clicked'

Start-Sleep -Seconds 8

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_scan_e.png')
Write-Output ("saved; pid=" + $p.Id + " alive=" + (-not $p.HasExited))
