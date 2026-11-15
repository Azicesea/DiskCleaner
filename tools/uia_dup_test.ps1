# 切到「重复文件」页 → 点查找 → 等结果 → 截图
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

$tabCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '重复文件')
$tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
if ($tab) {
    $sel = $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $sel.Select()
    Write-Output 'tab selected'
} else {
    Write-Output 'ERROR: tab not found'; exit 1
}
Start-Sleep -Milliseconds 800

$idCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'DupScanButton')
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
if (-not $btn) {
    $btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '查找重复文件')
    $btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
}
if (-not $btn) { Write-Output 'ERROR: dup scan button not found'; exit 1 }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'dup scan clicked'

# 轮询状态栏，等它变成结果文字
$statusCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'DupStatus')
$status = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $statusCond)
if (-not $status) {
    $statusCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '还没有查找过')
    $status = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $statusCond)
}

$deadline = (Get-Date).AddSeconds(300)
$last = ''
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    if ($status) {
        try { $last = $status.Current.Name } catch { $last = '' }
        if ($last -match '组重复|没找到重复|已取消|查找失败') { break }
    }
}
Write-Output ("status: " + $last)

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_dup.png')
Write-Output ("saved; pid=" + $p.Id)
