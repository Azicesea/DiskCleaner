using System.Runtime.InteropServices;

namespace DiskCleaner.Core.Interop;

/// <summary>Shell 相关 Win32 调用：走回收站的删除、回收站用量查询。</summary>
public static class ShellInterop
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_NOCONFIRMMKDIR = 0x0200;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    /// <summary>
    /// 把一批文件删进回收站。返回失败数量。
    /// SHFileOperation 的 pFrom 是双 null 结尾的路径列表且总长有限，所以内部分批。
    /// </summary>
    public static int DeleteToRecycleBin(IReadOnlyList<string> files)
    {
        var failed = 0;
        const int batchSize = 400;   // 每条路径可能很长，保守分批

        for (var offset = 0; offset < files.Count; offset += batchSize)
        {
            var batch = files.Skip(offset).Take(batchSize).ToArray();
            if (batch.Length == 0) break;

            var sb = new System.Text.StringBuilder();
            foreach (var f in batch)
            {
                sb.Append(f).Append('\0');
            }
            sb.Append('\0');   // 双 null 结尾

            var op = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FO_DELETE,
                pFrom = sb.ToString(),
                fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMMKDIR),
                fAnyOperationsAborted = 0,
            };

            int hr;
            try
            {
                hr = SHFileOperation(ref op);
            }
            catch (Exception)
            {
                failed += batch.Length;
                continue;
            }

            if (hr != 0 || op.fAnyOperationsAborted != 0)
            {
                // 整批报错时逐个重试，避免一个坏文件拖累整批
                foreach (var f in batch)
                {
                    if (File.Exists(f) && !TryDeleteOne(f)) failed++;
                }
            }
        }

        return failed;
    }

    private static bool TryDeleteOne(string file)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(file).Append('\0').Append('\0');
        var op = new SHFILEOPSTRUCT
        {
            hwnd = IntPtr.Zero,
            wFunc = FO_DELETE,
            pFrom = sb.ToString(),
            fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI),
        };
        try
        {
            var hr = SHFileOperation(ref op);
            return hr == 0 && op.fAnyOperationsAborted == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>查询回收站占用。root 传盘符如 "C:\"，传 null 查询全部。</summary>
    public static (long Size, long Items) QueryRecycleBin(string? root)
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        try
        {
            var hr = SHQueryRecycleBin(root, ref info);
            if (hr != 0) return (0, 0);
            return (info.i64Size, info.i64NumItems);
        }
        catch (Exception)
        {
            return (0, 0);
        }
    }

    /// <summary>清空回收站（不可还原）。返回是否成功。</summary>
    public static bool EmptyRecycleBin(string? root)
    {
        try
        {
            var hr = SHEmptyRecycleBin(IntPtr.Zero, root,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            return hr == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
