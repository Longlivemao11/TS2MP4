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
        Console.WriteLine("ALL " + passed + " CHECKS PASSED");
    }
}
