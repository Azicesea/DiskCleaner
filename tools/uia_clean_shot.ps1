Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$exe = 'E:\DSH\Clean\src\DiskCleaner.App\bin\Release\net8.0-windows\DiskCleaner.App.exe'
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6
$ae = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
$win = $ae.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
Write-Output ("window: " + $win.Current.Name)

$tabCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '清理')
$tab = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
$tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Seconds 1
Write-Output 'clean tab on'

$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '扫描可清理项')
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'scan clicked'
Start-Sleep -Seconds 8

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_clean.png')
Write-Output ("saved, pid=" + $p.Id)