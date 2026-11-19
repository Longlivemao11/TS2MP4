using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using TS2MP4;
class CoreTests {
    static int passed;
    static string ffmpeg, ffprobe;
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); passed++; }
    static ConversionResult Run(string file, string folder, bool encode) {
        double last = -1;
        var result = new Converter().Convert(ffmpeg, file, folder, encode, delegate(double p, string t) { last = p; }, delegate(string s) { });
        Check(result.Success, (encode ? "encode" : "remux") + " actual ffmpeg: " + result.Error);
        Check(last == 100, "progress completes only after success"); return result;
    }
    static string Probe(string file) {
        var p = Process.Start(new ProcessStartInfo(ffprobe, "-v error -show_entries stream=codec_name -of csv=p=0 " + Converter.Quote(file)) { RedirectStandardOutput = true, UseShellExecute = false });
        string text = p.StandardOutput.ReadToEnd(); p.WaitForExit(); if (p.ExitCode != 0) throw new Exception("ffprobe failed"); p.Dispose(); return text;
    }
    static void DeletionTests(string dir, string original, string output) {
        Action<double,string> progress = delegate(double p, string t) {};
        Action<string> log = delegate(string text) {};
        foreach (bool encode in new bool[] { false, true }) {
            string input = Path.Combine(dir, encode ? "delete-encode.ts" : "delete-copy.ts");
            File.Copy(original, input);
            var result = new Converter().Convert(ffmpeg, input, output, encode, true, progress, log);
            Check(result.Success && result.OriginalDeleted && result.Warning.Length == 0, "verified conversion removes opted-in source, encode=" + encode);
            Check(!File.Exists(input) && File.Exists(result.Output) && Probe(result.Output).Contains("h264"), "only MP4 remains after opted-in success");
        }
        string invalid = Path.Combine(dir, "delete-invalid.ts"); File.WriteAllText(invalid, "not video");
        var failure = new Converter().Convert(ffmpeg, invalid, output, false, true, progress, log);
        Check(!failure.Success && !failure.OriginalDeleted && File.Exists(invalid), "failed conversion never removes source");

        string corrupt = Path.Combine(dir, "delete-invalid-output.ts"); File.Copy(original, corrupt);
        bool corrupted = false;
        var validationFailure = new Converter().Convert(ffmpeg, corrupt, output, false, true,
            delegate(double p, string text) {
                if (text.Contains("正在完整校验")) {
                    string[] partial = Directory.GetFiles(output, ".ts2mp4-*");
                    Check(partial.Length == 1, "one candidate exists before validation");
                    File.WriteAllText(partial[0], "not an MP4"); corrupted = true;
                }
            }, log);
        Check(corrupted && !validationFailure.Success && validationFailure.Error.Contains("校验失败") && File.Exists(corrupt), "undecodable output keeps source");
        Check(!File.Exists(Path.Combine(output, "delete-invalid-output.mp4")), "invalid candidate is never published");

        string shortVideo = Path.Combine(dir, "short-output.mp4");
        using (Process p = Process.Start(new ProcessStartInfo(ffmpeg, "-hide_banner -nostdin -v error -i " + Converter.Quote(original) + " -t 0.2 -c copy " + Converter.Quote(shortVideo)) { UseShellExecute = false })) {
            p.WaitForExit(); Check(p.ExitCode == 0, "generate playable but truncated MP4 fixture");
        }
        string truncated = Path.Combine(dir, "delete-truncated.ts"); File.Copy(original, truncated);
        var truncatedResult = new Converter().Convert(ffmpeg, truncated, output, false, true,
            delegate(double p, string text) { if (text.Contains("正在完整校验")) File.Copy(shortVideo, Directory.GetFiles(output, ".ts2mp4-*")[0], true); }, log);
        Check(!truncatedResult.Success && truncatedResult.Error.Contains("时长") && File.Exists(truncated), "playable but truncated MP4 never causes source deletion");

        string cancelledInput = Path.Combine(dir, "delete-cancelled.ts"); File.Copy(original, cancelledInput);
        var cancelling = new Converter();
        var cancelled = cancelling.Convert(ffmpeg, cancelledInput, output, false, true,
            delegate(double p, string text) { if (text.Contains("正在完整校验")) cancelling.Cancel(); }, log);
        Check(cancelled.Cancelled && !cancelled.OriginalDeleted && File.Exists(cancelledInput), "cancellation at validation keeps source");
        Check(!File.Exists(Path.Combine(output, "delete-cancelled.mp4")), "cancellation before publication leaves no final MP4");

        string changed = Path.Combine(dir, "delete-changed.ts"); File.Copy(original, changed);
        var changedResult = new Converter().Convert(ffmpeg, changed, output, false, true,
            delegate(double p, string text) { if (text.Contains("正在完整校验")) File.AppendAllText(changed, "changed"); }, log);
        Check(changedResult.Success && !changedResult.OriginalDeleted && changedResult.Warning.Contains("变化") && File.Exists(changed), "changed source is retained with a warning");
        Check(File.Exists(changedResult.Output), "cleanup warning preserves the successful MP4");

        if (Path.DirectorySeparatorChar == '\\') {
            string locked = Path.Combine(dir, "delete-locked.ts"); File.Copy(original, locked);
            using (FileStream held = File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var lockedResult = new Converter().Convert(ffmpeg, locked, output, false, true, progress, log);
                Check(lockedResult.Success && !lockedResult.OriginalDeleted && lockedResult.Warning.Contains("删除失败"), "locked source reports cleanup warning on Windows");
                Check(File.Exists(locked) && File.Exists(lockedResult.Output), "deletion failure keeps both source and successful MP4");
            }
        }
        string other = Path.Combine(dir, "do-not-delete.dat"); File.Copy(original, other);
        var rejected = new Converter().Convert(ffmpeg, other, output, false, true, progress, log);
        Check(!rejected.Success && File.Exists(other), "automatic deletion rejects non-TS inputs");
        Check(Directory.GetFiles(output, ".ts2mp4-*").Length == 0, "deletion mode cleans failed or cancelled candidates");
    }
    static void Main(string[] args) {
        if (args.Length != 3) throw new ArgumentException("Usage: CoreTests fixture-directory ffmpeg ffprobe");
        ffmpeg = Path.GetFullPath(args[1]); ffprobe = Path.GetFullPath(args[2]);
        string dir = args[0], input = Path.Combine(dir, "中文 空格 & 100% ' 引号.ts"), output = Path.Combine(dir, "输出 空格");
        Directory.CreateDirectory(output);
        Check(Converter.Quote(@"C:\视频 目录\").EndsWith("\\\\\""), "trailing backslash Windows quoting");
        byte[] original = File.ReadAllBytes(input);
        var copy = Run(input, output, false);
        Check(Probe(copy.Output).Contains("h264") && Probe(copy.Output).Contains("aac"), "remux codecs preserved");
        byte[] existing = File.ReadAllBytes(copy.Output);
        var encoded = Run(input, output, true);
        Check(encoded.Output.EndsWith(" (1).mp4"), "same-name protection");
        Check(Convert.ToBase64String(existing) == Convert.ToBase64String(File.ReadAllBytes(copy.Output)), "existing output unchanged");
        Check(Probe(encoded.Output).Contains("h264") && Probe(encoded.Output).Contains("aac"), "reencode H264 AAC");
        var silent = Run(Path.Combine(dir, "silent.ts"), "", false);
        Check(File.Exists(silent.Output), "no-audio TS and beside-source output");
        var multi = Run(Path.Combine(dir, "multi.ts"), output, false);
        Check(Probe(multi.Output).Split(new string[]{"aac"}, StringSplitOptions.None).Length == 3, "multiple audio tracks retained");
        string bad = Path.Combine(dir, "broken.ts"); File.WriteAllText(bad, "this is not video");
        var failure = new Converter().Convert(ffmpeg, bad, output, false, delegate(double p, string t) {}, delegate(string s) {});
        Check(!failure.Success && failure.Error.Length > 0, "corrupt input reports failure");
        Check(!File.Exists(Path.Combine(output, "broken.mp4")), "failure publishes no MP4");
        var recovery = Run(input, output, false); Check(recovery.Success, "batch continues after failed item");
        var cancel = new Converter(); ConversionResult cancelled = null; bool cancelIssued = false;
        Thread thread = new Thread(delegate() { cancelled = cancel.Convert(ffmpeg, Path.Combine(dir, "long.ts"), output, true,
            delegate(double p, string t) { if (p >= 0 && !cancelIssued) { cancelIssued = true; cancel.Cancel(); } }, delegate(string s) {}); });
        thread.Start(); Check(thread.Join(30000), "cancelled conversion exits promptly");
        Check(cancelIssued && cancelled.Cancelled && !cancelled.Success, "cancel active ffmpeg");
        Check(!File.Exists(Path.Combine(output, "long.mp4")), "cancel publishes no partial MP4");
        Check(Directory.GetFiles(output, ".ts2mp4-*").Length == 0, "temporary files cleaned after failure and cancellation");
        Check(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(input)), "original TS unchanged");
        var pre = new Converter(); pre.Cancel(); var stopped = pre.Convert(ffmpeg, input, output, false, delegate(double p, string t) {}, delegate(string s) {});
        Check(stopped.Cancelled, "cancel before process startup");
        DeletionTests(dir, input, output);
        Console.WriteLine("ALL " + passed + " CHECKS PASSED");
    }
}
