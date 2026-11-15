# 打开「自定义规则」窗口 → 填一条规则 → 添加 → 回主窗口扫描 → 截图
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$exe = 'E:\DSH\Clean\src\DiskCleaner.App\bin\Release\net8.0-windows\DiskCleaner.App.exe'
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 3

$root = [System.Windows.Automation.AutomationElement]::RootElement
$pidCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCond)
if (-not $win) { Write-Output 'ERROR: window not found'; exit 1 }
Write-Output ("window: " + $win.Current.Name)

# 切到「清理」Tab
$tabCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '清理')
$tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
$tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 600

# 点「自定义规则…」
$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '自定义规则…')
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $btn) { Write-Output 'ERROR: custom rules button not found'; exit 1 }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Seconds 2

$dlgCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '自定义清理规则')
$dlg = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $dlgCond)
if (-not $dlg) { Write-Output 'ERROR: dialog not found'; exit 1 }
Write-Output ("dialog: " + $dlg.Current.Name)

function Set-Value($parent, $autoId, $text) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $autoId)
    $el = $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
    if (-not $el) { Write-Output "ERROR: element $autoId not found"; return $false }
    $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
    return $true
}

Set-Value $dlg 'NameBox' '测试规则：我的缓存' | Out-Null
Set-Value $dlg 'PathBox' 'E:\DSH\Clean\tmp\customtest' | Out-Null
Start-Sleep -Milliseconds 400

$addCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'AddButton')
$add = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $addCond)
$add.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'add clicked'
Start-Sleep -Seconds 1

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_customrules.png')
Write-Output 'screenshot 1 saved'

# 关闭对话框
$closeCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '关闭')
$close = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $closeCond)
$close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Seconds 3

$scanCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '扫描可清理项')
$scan = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $scanCond)
$scan.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Seconds 6

$bmp2 = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g2 = [System.Drawing.Graphics]::FromImage($bmp2)
$g2.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp2.Save('E:\DSH\Clean\tmp\gui_customrules_scan.png')
Write-Output ("saved; pid=" + $p.Id)
