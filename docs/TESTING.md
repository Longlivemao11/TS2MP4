# 测试与验证范围

## Windows 集成测试

先运行 `Build-Windows.cmd /nopause`。测试需要支持 lavfi、libx264、AAC 的 FFmpeg，以及同目录或另行指定的 ffprobe。

```powershell
powershell -NoProfile -File .\tests\Test-Windows.ps1 -FFmpeg "C:\ffmpeg\bin\ffmpeg.exe"
```

可用 `-FFprobe "其他位置\ffprobe.exe"` 指定探测工具，加 `-IncludeUI` 运行界面测试。界面测试需要已登录且未锁定的桌面。

脚本会在临时目录生成合成 TS 样本，编译测试程序并运行，结束后删除临时样本。默认只运行核心测试；界面测试会临时写入应用设置，脚本在结束时恢复原 `settings.xml`，界面运行的测试日志留在应用日志目录。测试期间不要同时运行另一个 TS2MP4 窗口。

核心测试覆盖：真实 FFmpeg 无损与重编码、中文及特殊字符路径、同名输出保护、无音轨、多音轨、损坏输入、失败后的后续处理、取消及临时文件清理、原文件不变。界面测试覆盖添加与去重、拖放事件、模式及目录控件、实际批量转换和结果状态。

## 手动验收

发布重新构建的 Windows 包前，完整解压后检查：EXE 与备用 CMD 启动、浏览选择 FFmpeg、多选和实际拖放、两种模式、自定义输出目录、同名保护、取消与重试、错误日志，以及 100% / 150% 显示缩放。用实际播放器检查输出画面、音轨和音画同步。

## 本次发布依据

v1.0.2 原 EXE 已获用户反馈，在 Windows 上直接启动和使用正常。本次保留相同 EXE 与运行配置，发布整理没有改变转换和界面源码。

另有 Linux / Mono + 真实 FFmpeg 的核心与界面集成测试，用于验证进程调用、输出保护和队列逻辑。此环境不能代替 Windows 的全部行为或不同 Windows / FFmpeg 版本的测试。Windows CMD / PowerShell 脚本仍需在 Windows 上执行验证；本次环境没有原生 Windows，未声称这些脚本已在 Windows 实机跑通。
