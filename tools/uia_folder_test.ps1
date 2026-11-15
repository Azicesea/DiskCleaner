# 「选文件夹…」冒烟测试：选一个目录扫描，再切清理页确认没有自定义规则按钮
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class W32 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, string l);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);

  public static List<IntPtr> TopLevelOf(uint pid, string cls) {
    var list = new List<IntPtr>();
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p != pid) return true;
      var sb = new StringBuilder(256); GetClassName(h, sb, 256);
      if (cls == null || sb.ToString() == cls) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }

  public static List<IntPtr> Children(IntPtr parent, string cls) {
    var list = new List<IntPtr>();
    EnumChildWindows(parent, (h, l) => {
      var sb = new StringBuilder(256); GetClassName(h, sb, 256);
      if (cls == null || sb.ToString() == cls) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }

  public static string Text(IntPtr h) {
    var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString();
  }
}
"@

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

function ById($id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    $win.FindFirst($Scope::Descendants, $c)
}

# 点「选文件夹…」
$pick = ById 'PickFolderButton'
$pick.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Write-Output 'pick clicked'
Start-Sleep -Seconds 3

$dlgs = [W32]::TopLevelOf($proc.Id, '#32770')
if ($dlgs.Count -eq 0) { throw 'folder dialog not found' }
$dlg = $dlgs[0]
Write-Output ('dialog: ' + [W32]::Text($dlg))

# 填路径
$target = 'E:\DSH\Clean\tmp\foldertest'
$edits = [W32]::Children($dlg, 'Edit')
$filled = $false
foreach ($e in $edits) {
    if ([W32]::IsWindowVisible($e)) {
        [W32]::SendMessage($e, 0x000C, [IntPtr]::Zero, $target) | Out-Null
        $filled = $true
        break
    }
}
Write-Output ('path filled: ' + $filled)

# 点「选择文件夹」
$btns = [W32]::Children($dlg, 'Button')
$ok = $false
foreach ($b in $btns) {
    if ([W32]::Text($b) -eq '选择文件夹') {
        [W32]::SendMessage($b, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        $ok = $true
        break
    }
}
Write-Output ('confirm clicked: ' + $ok)

Start-Sleep -Seconds 6

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save('E:\DSH\Clean\tmp\gui_pickfolder.png')
$g.Dispose(); $bmp.Dispose()
Write-Output 'saved pickfolder'

# 切清理页看有没有「自定义规则」按钮
$tabCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, '清理')),
    (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)))
$tab = $win.FindFirst($Scope::Descendants, $tabCond)
$tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 800
$old = ById 'CustomRulesButton'
Write-Output ('custom rules button present: ' + ($null -ne $old))
Write-Output 'done'
