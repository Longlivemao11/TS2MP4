# TS2MP4

一个轻量的 Windows 桌面 TS 转 MP4 工具。使用本机已经安装的 FFmpeg，支持批量无损封装和重新编码。

**版本：v1.1.0 · 作者：Akutagawa · C# / Windows Forms · MIT License**

## 下载与启动

v1.1.0 新增可选的“转换成功并校验后永久删除原 TS”。该选项默认关闭，仅对本批次生效。

[下载 Windows 成品](https://github.com/Longlivemao11/TS2MP4/releases/download/v1.1.0/TS2MP4-Windows-v1.1.0.zip) · [查看 v1.1.0 发布页](https://github.com/Longlivemao11/TS2MP4/releases/tag/v1.1.0)

普通用户请在本仓库的 **Releases** 页面下载 `TS2MP4-Windows-v1.1.0.zip`。GitHub 的 **Code → Download ZIP** 和自动生成的 **Source code** 下载项是源码，需要自行编译。

1. 将下载包完整解压到一个文件夹。
2. 双击 `TS2MP4.exe`。启动遇到问题时，可运行 `Start-TS2MP4.cmd`，使用 Windows 本机编译器重新生成并启动。
3. 在 FFmpeg 一栏选择已经安装的 `ffmpeg.exe`。
4. 添加或拖入 `.ts` 文件，选择转换方式、输出目录，点击 **开始 / 重试**。

需要 Windows 和 .NET Framework 4.x；推荐在 Windows 10 / 11 上使用。无需安装 Python、Node.js 或 .NET SDK。目标程序为 AnyCPU，FFmpeg 的架构需适合当前 Windows 系统。项目未承诺旧版 Windows、所有 Windows 构建及所有视频编码组合均通过测试。

FFmpeg 不随本工具分发。已安装的 FFmpeg 必须支持输入文件的解码和 MP4 输出；重新编码需要 `libx264` 和 `aac` 编码器。`ffprobe.exe` 放在 `ffmpeg.exe` 同目录可用于读取时长；没有 ffprobe 时会尝试用 FFmpeg 读取。

## 功能

- 选取多个 TS 文件，或将文件拖入列表；自动过滤重复路径。
- 快速无损：复制音视频流，更换容器，不重新压缩。
- 重新编码：H.264 + AAC，显示当前文件和批量进度。
- 保存在原文件旁边，或指定统一输出目录。
- 顺序批量转换；单项失败后继续处理，支持取消及重试。
- 同名输出自动加编号，不覆盖原文件或已有 MP4。
- 本地设置、转换日志和启动诊断。
- 可选在转换成功、完整校验并保存 MP4 后永久删除原 TS，默认关闭。

## 两种模式

| 模式 | 适用情况 | 行为 |
| --- | --- | --- |
| 快速无损 | 原音视频编码适合 MP4，优先保持画质 | 使用 `-c copy`；速度通常较快，播放器兼容性仍取决于原编码 |
| 重新编码 | 无损模式失败，或希望输出 H.264 / AAC | 视频 `libx264`、CRF 20、medium、yuv420p；音频 AAC 192 kb/s；会有损耗 |

只保留第一路视频和全部音轨，不保留广播字幕、其他视频流或数据流。重新编码会将奇数宽高向下调整为偶数。无损转换不等于逐字节复制整个文件。

批量进度按已处理的文件数量计算，包含失败和取消项；无法读取时长时，当前进度显示处理中。成功后才生成最终 MP4。正常取消或失败会尝试删除临时文件；系统断电或强制结束进程可能留下 `.ts2mp4-*.partial.mp4`。

## 转换后只保留 MP4

勾选“转换成功并校验后永久删除原 TS”，点击“开始 / 重试”并确认本批次。程序会额外完整解码并核对时长，成功保存后才删除对应原 TS。校验会增加处理时间；失败、取消、校验失败或源文件发生变化时保留原 TS。

这是**永久删除，不进入回收站**。选项不会保存到设置，批次结束后自动取消勾选。若删除受占用或权限限制，保留 MP4 并明确提示原 TS 未删除；双击该项目查看原因。取消批次不会恢复前面已成功删除的原 TS。

## 文档与开发

- [使用说明与故障排查](docs/USER_GUIDE.md)
- [源码构建](docs/BUILDING.md)
- [测试方法与验证范围](docs/TESTING.md)
- [版本历史](CHANGELOG.md) / [v1.1.0 发布说明](releases/v1.1.0.md)
- [参与贡献](CONTRIBUTING.md)
- [外部组件说明](THIRD_PARTY_NOTICES.md)

源码位于 `source/`，集成测试位于 `tests/`。在 Windows 上运行根目录的 `Build-Windows.cmd`，生成 `bin/TS2MP4.exe`。

## 隐私

应用自身没有账号、上传视频、遥测或自动更新功能。FFmpeg 通过本地进程调用。设置和日志位于 `%LOCALAPPDATA%\TS2MP4`；日志可能包含完整文件路径，请在公开提交前删去私人信息。关闭应用后删除此目录可清除设置与日志。

## 许可证

TS2MP4 自有代码和文档采用 [MIT License](LICENSE)。可以使用、修改和分发，也可以商用；分发时须保留版权和许可声明。MIT 许可不要求衍生项目继续开源。

FFmpeg 是独立外部工具，适用其自身许可证；TS2MP4 的 MIT 许可不替代 FFmpeg 或 Windows / .NET 的许可条款。
