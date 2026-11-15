# 关键词页 UIA 冒烟测试：切 Tab -> 选 E 盘 -> 输入「临时文件」-> 查找 -> 截图
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$exe = 'E:\DSH\Clean\src\DiskCleaner.App\bin\Release\net8.0-windows\DiskCleaner.App.exe'
$AE = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]

Get-Process DiskCleaner.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$proc = Start-Process $exe -PassThru
Start-Sleep -Seconds 5

$pc = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
$win = $AE::RootElement.FindFirst($Scope::Children, $pc)
if ($null -eq $win) { throw 'window not found' }
Write-Output ("window: " + $win.Current.Name)

# 切到「关键词」Tab
$tabCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, '关键词')),
    (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)))
$tab = $win.FindFirst($Scope::Descendants, $tabCond)
$tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Write-Output 'tab: keyword on'
Start-Sleep -Milliseconds 800

function ById($id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    $win.FindFirst($Scope::Descendants, $c)
}

# 选 E 盘
$combo = ById 'FindRootCombo'
$combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
Start-Sleep -Milliseconds 600
$items = $combo.FindAll($Scope::Children, [System.Windows.Automation.Condition]::TrueCondition)
$picked = $false
foreach ($it in $items) {
    if ($it.Current.Name -like 'E:*') {
        $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Write-Output ('drive: ' + $it.Current.Name)
        $picked = $true
        break
    }
}
if (-not $picked) { Write-Output ('items: ' + ($items | ForEach-Object { $_.Current.Name }) -join ' | ') }
try { $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch { }
Start-Sleep -Milliseconds 400

# 输入关键词
$box = ById 'KeywordBox'
$box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('临时文件')
Write-Output 'keyword typed'
Start-Sleep -Milliseconds 300

# 查找
$btn = ById 'FindButton'
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'find clicked'

# 等结果
Start-Sleep -Seconds 8
$status = ById 'FindStatus'
Write-Output ('status: ' + $status.Current.Name)

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_find.png')
$g.Dispose()
$bmp.Dispose()
Write-Output 'saved'
Write-Output ('pid=' + $proc.Id)
