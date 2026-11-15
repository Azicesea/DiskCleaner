# DiskCleaner

一个轻量的 Windows 磁盘清理工具。.NET 8 + WPF，单窗口、无外部依赖、不联网。

## 功能

### 📊 磁盘占用

扫描整个硬盘，按文件夹列出占用大小、文件数和占比。也可以点「选文件夹…」只看某一个目录，不用扫全盘。

- **列表视图**：按大小降序的表格，带占比百分比
- **树图视图**：方块面积就是实际占用，一眼看出谁最占空间；点击方块能下钻进子文件夹，左上角「返回上级」退回
- 全盘 25 万个文件、275 GB 大约 1–2 秒扫完
- 没有权限的目录会自动跳过，不会卡住

### 📁 大文件

列出全盘最大的 200 个文件，带大小、文件名和所在文件夹。双击任意一行，直接在资源管理器里定位并选中它，方便判断该不该留。

### 🔍 重复文件

三阶段查找，先易后难，避免把每个文件都完整读一遍：

1. 按文件大小分组，大小不同的直接排除
2. 组内比对每个文件的头 8KB
3. 全部相同的候选再算完整 SHA-256 确认

结果按「能省多少空间」降序排列。可以选要扫描哪个磁盘，也可以设置只看大于 1MB / 10MB / 100MB 的文件。

`Program Files`、`Windows` 下的重复项会被标注为「系统文件」——它们通常是程序自带的多份拷贝，删了软件会坏，看得到但别动。

### 🔎 关键词

按关键词在文件名和完整路径里找文件，列出大小、修改时间和所在位置。

- 直接输入想找的东西，比如「临时文件」「缓存」「日志」「.mp4」
- 中文词自带同义词：输入「临时文件」会同时匹配 Temp 目录、`.tmp`、`.temp`；「缓存」匹配 cache；「视频」匹配 `.mp4`/`.mkv`/`.avi` 等扩展名
- 英文词按整词匹配，`temp` 不会误命中 `Templates`
- 查找位置可以是整块磁盘，也可以点「选文件夹…」指定单个目录
- 结果按大小降序排列，双击一行在资源管理器里定位

### 🧹 清理

内置 9 条清理规则：用户临时文件、崩溃转储、缩略图缓存、DirectX 着色器缓存、NVIDIA 着色器缓存、Chrome 缓存、Edge 缓存、Firefox 缓存、回收站。

- 先扫描出每一项能省多少空间，你勾选确认后才动手
- 默认「删掉的文件放进回收站」，删错了能捡回来
- 正在被程序占用的文件会自动跳过
- 本机没有的项目显示「本机没有」，不会标成 0 字节混淆视听
- 回收站这一项默认不勾选，因为它清空后不可还原

## 安全性

清理工具最要紧的是不误删。

- **只删文件，不删目录**——目录结构保留，下次运行照常
- **默认走回收站**：调用 Windows `SHFileOperation` + `FOF_ALLOWUNDO`，不是硬删除
- **不碰系统文件**：内置规则只命中缓存和临时文件；重复文件里系统目录的项会单独标注
- **不联网**：没有任何网络请求，不收集、不上传任何数据
- **扫描全盘是只读的**：不写一个字节

## 系统要求

- Windows 10 / 11（x64）
- [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（如果自己编译过就不用另装）

## 从源码构建

```bash
git clone https://github.com/Azicesea/DiskCleaner.git
cd DiskCleaner
dotnet build DiskCleaner.sln -c Release
```

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。产物在：

```
src/DiskCleaner.App/bin/Release/net8.0-windows/DiskCleaner.App.exe
```

## 打包成单文件

想让软件在没装 .NET 的电脑上直接双击运行，可以打成自包含单文件：

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish.ps1
```

产物是 `publish/DiskCleaner.App.exe`，一个文件约 68 MB，运行时已经打包在里面。拷到任何 Windows 10 / 11 x64 上双击即可使用。

单文件版第一次启动会稍慢（要把自己解压到临时目录），之后就正常了。

## 项目结构

```
DiskCleaner.sln
├── src/
│   ├── DiskCleaner.Core/            核心逻辑，与界面无关
│   │   ├── Models/                  DirNode（目录树）、FileEntry
│   │   ├── Scan/                    DiskScanner（并行扫描）、TreemapLayout（squarified 树图）
│   │   ├── Clean/                   清理规则、扫描、执行
│   │   ├── Dup/                     DuplicateFinder（大小分组 + 预筛 + 哈希）
│   │   ├── Find/                    KeywordFinder（关键词 + 同义词匹配）
│   │   └── Interop/                 ShellInterop（回收站相关 Win32 API）
│   └── DiskCleaner.App/             WPF 界面
│       └── MainWindow.xaml          五个页面：磁盘占用 / 大文件 / 重复文件 / 关键词 / 清理
└── tools/                           命令行验证工具与 UI 自动化测试脚本
```

## 已知限制

- 重复文件只比对 1MB 及以上的文件，小文件不参与
- 关键词查找最多返回 5000 条，再多请缩小查找位置或换更具体的关键词
- 树图下钻只到文件夹层级，不展开单个文件（看单个大文件请去「大文件」页）
- 清理规则靠路径匹配，不理解文件内容
