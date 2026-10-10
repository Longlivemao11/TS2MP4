# 从源码构建

## Windows 本机构建

需要 Windows 和已启用的 .NET Framework 4.x（含 `csc.exe`）。构建不需要 FFmpeg、额外 NuGet 包或 .NET SDK；转换时才需要 FFmpeg。

双击根目录 `Build-Windows.cmd`，或在命令提示符运行：

```bat
Build-Windows.cmd /nopause
```

输出为 `bin/TS2MP4.exe` 和 `bin/TS2MP4.exe.config`。脚本查找 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，找不到时回退到 32 位 Framework 目录。采用 AnyCPU、Windows Forms、UTF-8 源码和 .NET Framework 4.x 系统程序集。

## 代码结构

| 路径 | 用途 |
| --- | --- |
| `source/Desktop.cs` | Windows Forms 界面、队列、设置、日志与入口 |
| `source/ConversionCore.cs` | FFmpeg 进程、进度解析、取消及输出保护 |
| `source/AssemblyInfo.cs` | 程序名称与版本 |
| `source/app.manifest` | 权限、DPI 与 Windows 兼容声明 |
| `TS2MP4.exe.config` | .NET Framework 运行时配置 |
| `tests/` | 使用真实 FFmpeg 的集成测试 |

## 生成发布包

在 Windows PowerShell 中运行：

```powershell
powershell -NoProfile -File .\scripts\Package-Windows.ps1
```

该脚本先执行本机构建，再创建 `dist/TS2MP4-Windows-v1.0.2.zip` 和 `dist/SHA256SUMS.txt`。包内含程序、配置、备用启动脚本、源码、LICENSE、说明与版本历史，不包含 FFmpeg、日志和测试视频。默认不允许覆盖已有同版本下载包；确认需要覆盖时加 `-Force`。

若本机策略阻止脚本，请按组织或个人的 PowerShell 策略处理；无须为普通 EXE 使用修改脚本策略。

## 编译兼容性

跨平台编译时必须使用 Microsoft .NET Framework 对应参考程序集，不要把 Mono 的运行实现程序集当作 Windows 编译目标。此前直接启动问题来自错误引用导致使用 Windows .NET Framework 不存在的重载，v1.0.2 已改用字符数组重载。

当前交付的 EXE 沿用已经过用户 Windows 验证的 v1.0.2 二进制。本机构建得到的 EXE 时间戳、哈希可能不同；项目未声明二进制逐字节可复现。重新构建或修改源码后，应按 [测试说明](TESTING.md) 验证。
