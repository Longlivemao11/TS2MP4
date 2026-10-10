# 外部组件

TS2MP4 通过独立进程调用用户本机的 `ffmpeg.exe`；不链接 FFmpeg 库，不在本仓库或下载包中包含 FFmpeg / ffprobe 二进制或源码。

FFmpeg 适用其自己的许可证。根据官方说明，基础许可为 LGPL 2.1 或更高版本，启用某些可选组件时整体可能适用 GPL。具体以用户使用的 FFmpeg 构建及其随附声明为准。TS2MP4 的 MIT 许可不授予 FFmpeg 或其可选组件的许可。

- 官方网站：https://ffmpeg.org/
- 下载入口：https://ffmpeg.org/download.html
- 许可证说明：https://ffmpeg.org/legal.html

Windows、.NET Framework 和系统字体是运行环境，适用各自条款。本项目没有重新分发这些组件。

如未来决定捆绑外部组件，应先补齐该具体构建的许可证、对应源码及分发要求，再发布相应下载包。
