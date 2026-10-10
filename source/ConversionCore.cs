using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Collections.Generic;

namespace TS2MP4 {
    public sealed class ConversionResult {
        public bool Success, Cancelled;
        public string Output = "", Error = "";
    }
    public sealed class Converter {
        private readonly object gate = new object();
        private Process active;
        private volatile bool cancelled;
        public void Cancel() {
            lock (gate) {
                cancelled = true;
                try { if (active != null && !active.HasExited) active.Kill(); } catch { }
            }
        }
        public static string Quote(string arg) {
            // Windows CommandLineToArgvW / CRT quoting, without a shell.
            StringBuilder b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in arg) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append(c); }
                else { b.Append('\\', slashes); b.Append(c); }
                slashes = 0;
            }
            b.Append('\\', slashes * 2); return b.Append('"').ToString();
        }
        public static string Arguments(string input, string temp, bool encode) {
            return "-hide_banner -nostdin -n -loglevel warning -nostats -progress pipe:1 -i " + Quote(input)
                + " -map 0:v:0 -map 0:a? -sn -dn "
                + (encode ? "-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:a aac -b:a 192k " : "-c copy ")
                + "-avoid_negative_ts make_zero -movflags +faststart -f mp4 " + Quote(temp);
        }
        public static string AvailableOutput(string input, string folder) {
            string dir = String.IsNullOrWhiteSpace(folder) ? Path.GetDirectoryName(Path.GetFullPath(input)) : Path.GetFullPath(folder);
            string stem = Path.GetFileNameWithoutExtension(input);
            string path = Path.Combine(dir, stem + ".mp4"); int n = 1;
            while (File.Exists(path) || Directory.Exists(path)) path = Path.Combine(dir, stem + " (" + n++ + ").mp4");
            return path;
        }
        private Process NewProcess(string executable, string args) {
            Process p = new Process();
            p.StartInfo = new ProcessStartInfo(executable, args) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            return p;
        }
        private bool Start(Process p) {
            lock (gate) {
                if (cancelled) return false;
                p.Start(); active = p; return true;
            }
        }
        private void Detach(Process p) { lock (gate) { if (active == p) active = null; } }
        public double Duration(string ffmpeg, string input) {
            string probe = Path.Combine(Path.GetDirectoryName(ffmpeg), "ffprobe.exe");
            // Unix path is only used by the automated core tests.
            if (!File.Exists(probe)) probe = Path.Combine(Path.GetDirectoryName(ffmpeg), "ffprobe");
            bool hasProbe = File.Exists(probe);
            string args = hasProbe ? "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 " + Quote(input) : "-hide_banner -nostdin -i " + Quote(input);
            using (Process p = NewProcess(hasProbe ? probe : ffmpeg, args)) {
                StringBuilder stdout = new StringBuilder(), stderr = new StringBuilder();
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null && stdout.Length < 65536) stdout.AppendLine(e.Data); };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null && stderr.Length < 65536) stderr.AppendLine(e.Data); };
                if (!Start(p)) return 0;
                try {
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } }
                    p.WaitForExit();
                    double seconds;
                    if (hasProbe && Double.TryParse(stdout.ToString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds > 0) return seconds;
                    Match m = Regex.Match(stderr.ToString(), @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                    if (m.Success) return Double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 + Double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 60 + Double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    return 0;
                } finally { Detach(p); }
            }
        }
        public ConversionResult Convert(string ffmpeg, string input, string folder, bool encode, Action<double,string> progress, Action<string> log) {
            ConversionResult result = new ConversionResult(); string temp = null;
            try {
                if (cancelled) { result.Cancelled = true; return result; }
                if (!File.Exists(input)) throw new IOException("原文件不存在：" + input);
                string output = AvailableOutput(input, folder);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                temp = Path.Combine(Path.GetDirectoryName(output), ".ts2mp4-" + Guid.NewGuid().ToString("N") + ".partial.mp4");
                progress(-1, "读取视频信息…");
                double duration = Duration(ffmpeg, input);
                if (cancelled) { result.Cancelled = true; return result; }
                string args = Arguments(input, temp, encode); log("FFmpeg: " + Quote(ffmpeg) + " " + args);
                Queue<string> errors = new Queue<string>(); string speed = "";
                using (Process p = NewProcess(ffmpeg, args)) {
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) {
                        if (e.Data == null) return;
                        log(e.Data); lock (errors) { errors.Enqueue(e.Data); while (errors.Count > 16) errors.Dequeue(); }
                    };
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) {
                        if (e.Data == null) return;
                        int split = e.Data.IndexOf('='); if (split < 0) return;
                        string key = e.Data.Substring(0, split), value = e.Data.Substring(split + 1);
                        if (key == "speed") speed = value;
                        long micro;
                        if ((key == "out_time_us" || key == "out_time_ms") && Int64.TryParse(value, out micro)) {
                            double sec = Math.Max(0, micro / 1000000.0);
                            double percent = duration > 0 ? Math.Min(99, sec / duration * 100) : -1;
                            progress(percent, "已处理 " + TimeSpan.FromSeconds(Math.Min(sec, 31536000)).ToString(@"hh\:mm\:ss") + (speed.Length > 0 ? " · " + speed : ""));
                        }
                        if (key == "progress" && value == "end") progress(duration > 0 ? 99 : -1, "整理 MP4 文件…");
                    };
                    if (!Start(p)) { result.Cancelled = true; return result; }
                    try {
                        p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
                        if (cancelled) { result.Cancelled = true; return result; }
                        if (p.ExitCode != 0) {
                            string details; lock (errors) { details = String.Join(Environment.NewLine, errors.ToArray()); }
                            throw new IOException("FFmpeg 转换失败（退出码 " + p.ExitCode + "）。\r\n" + details);
                        }
                    } finally { Detach(p); }
                }
                if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new IOException("FFmpeg 未产生有效输出文件。");
                // Never overwrite a file that appears while conversion is running.
                lock (gate) {
                    if (cancelled) { result.Cancelled = true; return result; }
                    output = AvailableOutput(input, folder);
                    File.Move(temp, output);
                    result.Success = true; result.Output = output;
                }
                progress(100, "完成"); log("完成：" + output);
            } catch (Exception ex) {
                if (cancelled) result.Cancelled = true;
                else { result.Error = ex.Message; log("错误：" + ex); }
            } finally {
                if (temp != null && File.Exists(temp)) {
                    try { File.Delete(temp); } catch (Exception ex) { log("临时文件清理失败，请手动删除：" + temp + " · " + ex.Message); }
                }
            }
            return result;
        }
    }
}
