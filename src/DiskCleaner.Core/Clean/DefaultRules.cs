namespace DiskCleaner.Core.Clean;

/// <summary>内置清理规则集。只包含「删了也能自动重建」的东西。</summary>
public static class DefaultRules
{
    /// <summary>该不该建议用户勾选（缓存类默认勾，回收站不默认勾）。</summary>
    public static IReadOnlyList<CleanRule> All { get; } = new[]
    {
        new CleanRule
        {
            Id = "user-temp",
            Name = "用户临时文件",
            Description = "程序运行中产生的临时文件。正在使用的会自动跳过，删了不影响任何程序。",
            Category = CleanCategory.Temp,
            UseRecycleBin = false,
            Paths = ["%TEMP%"],
        },
        new CleanRule
        {
            Id = "crash-dumps",
            Name = "崩溃转储文件",
            Description = "程序崩溃时生成的诊断文件，只在排查崩溃时需要。",
            Category = CleanCategory.Temp,
            UseRecycleBin = false,
            Paths = ["%LOCALAPPDATA%\\CrashDumps"],
        },
        new CleanRule
        {
            Id = "thumbnail-cache",
            Name = "缩略图缓存",
            Description = "资源管理器的图片预览缓存，删掉后打开文件夹会重新生成。",
            Category = CleanCategory.System,
            UseRecycleBin = false,
            Paths = ["%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db"],
        },
        new CleanRule
        {
            Id = "d3d-cache",
            Name = "DirectX 着色器缓存",
            Description = "游戏和图形程序的着色器缓存，删掉后首次运行会稍慢，之后自动恢复。",
            Category = CleanCategory.System,
            UseRecycleBin = false,
            Paths = ["%LOCALAPPDATA%\\D3DSCache"],
        },
        new CleanRule
        {
            Id = "nvidia-cache",
            Name = "NVIDIA 着色器缓存",
            Description = "NVIDIA 显卡的着色器缓存，删掉后游戏首次加载会重新编译。",
            Category = CleanCategory.System,
            UseRecycleBin = false,
            Paths = ["%LOCALAPPDATA%\\NVIDIA\\DXCache", "%LOCALAPPDATA%\\NVIDIA\\GLCache"],
        },
        new CleanRule
        {
            Id = "chrome-cache",
            Name = "Chrome 缓存",
            Description = "Chrome 缓存的网页图片、脚本和视频片段。不动书签、密码和登录状态。",
            Category = CleanCategory.Browser,
            UseRecycleBin = false,
            Paths =
            [
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\GrShaderCache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\ShaderCache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\Cache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\Code Cache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\GPUCache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\DawnGraphiteCache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\DawnWebGPUCache",
                "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\*\\Service Worker\\CacheStorage",
            ],
        },
        new CleanRule
        {
            Id = "edge-cache",
            Name = "Edge 缓存",
            Description = "Edge 浏览器缓存，不动收藏夹和登录状态。",
            Category = CleanCategory.Browser,
            UseRecycleBin = false,
            Paths =
            [
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\GrShaderCache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\ShaderCache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\Cache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\Code Cache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\GPUCache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\DawnGraphiteCache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\DawnWebGPUCache",
                "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\*\\Service Worker\\CacheStorage",
            ],
        },
        new CleanRule
        {
            Id = "firefox-cache",
            Name = "Firefox 缓存",
            Description = "Firefox 浏览器缓存，不动历史记录和登录状态。",
            Category = CleanCategory.Browser,
            UseRecycleBin = false,
            Paths = ["%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2"],
        },
        new CleanRule
        {
            Id = "recycle-bin",
            Name = "回收站",
            Description = "彻底清空回收站里的文件。这个操作不可还原。",
            Category = CleanCategory.RecycleBin,
            Kind = CleanRuleKind.RecycleBin,
            UseRecycleBin = false,
        },
    };

    /// <summary>默认勾选项：缓存类勾上，回收站不勾。</summary>
    public static bool DefaultChecked(CleanRule rule) => rule.Kind != CleanRuleKind.RecycleBin;
}
