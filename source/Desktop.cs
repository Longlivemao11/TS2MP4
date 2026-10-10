using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;

namespace TS2MP4 {
    public class Job {
        public string Input, Output = "", State = "等待", Error = "";
        public ListViewItem Row;
    }
    public class Update {
        public Job Job; public double Percent; public string Text; public ConversionResult Result;
        public int Finished, Total;
    }
    public class MainForm : Form {
        readonly Color ink = Color.FromArgb(28, 40, 56), blue = Color.FromArgb(36, 102, 210);
        readonly List<Job> jobs = new List<Job>();
        readonly List<Control> editControls = new List<Control>();
        TextBox ffmpegBox, outputBox, detailBox;
        ComboBox mode;
        CheckBox sameFolder;
        ListView list;
        ProgressBar current, total;
        Label currentText, totalText, notice;
        Button start, stop, folder;
        BackgroundWorker worker;
        Converter converter;
        string appDir, logPath = "", lastOutput = "";
        bool closing;
        readonly object logGate = new object();
        string runFFmpeg, runFolder;
        bool runEncode;
        Job[] runJobs;

        public MainForm() {
            Text = "TS → MP4 · 本地视频转换器";
            Font = new Font("Microsoft YaHei UI", 9F); ForeColor = ink;
            BackColor = Color.FromArgb(247, 249, 252);
            ClientSize = new Size(1030, 730); MinimumSize = new Size(870, 620);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
            appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TS2MP4");
            BuildUI(); LoadSettings();
            worker = new BackgroundWorker { WorkerReportsProgress = true };
            worker.DoWork += RunBatch; worker.ProgressChanged += OnProgress; worker.RunWorkerCompleted += OnDone;
            FormClosing += OnClosing;
            Shown += delegate {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
                if (String.IsNullOrEmpty(ffmpegBox.Text)) notice.Text = "请点击“浏览”选择 ffmpeg.exe；也可以放在本工具旁边。";
            };
        }
        Label LabelOf(string text, float size, bool bold) {
            return new Label { Text = text, AutoSize = true, Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 3, 0, 3) };
        }
        Button ButtonOf(string text, EventHandler handler, bool editable) {
            Button b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(90, 32), FlatStyle = FlatStyle.Flat, Margin = new Padding(4) };
            b.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 228); b.BackColor = Color.White; b.Click += handler;
            if (editable) editControls.Add(b); return b;
        }
        void BuildUI() {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 18), ColumnCount = 1, RowCount = 9 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            float[] heights = { 66, 42, 42, 68, 42, 0, 100, 74, 46 };
            for (int i = 0; i < heights.Length; i++) root.RowStyles.Add(i == 5 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, heights[i]));
            Controls.Add(root);
            FlowLayoutPanel title = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            title.Controls.Add(LabelOf("TS → MP4", 22, true)); title.Controls.Add(LabelOf("本地转换 · 批量处理 · 保留原文件", 9, false)); root.Controls.Add(title, 0, 0);

            TableLayoutPanel exeRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            exeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); exeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); exeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            exeRow.Controls.Add(LabelOf("FFmpeg", 9, true), 0, 0);
            ffmpegBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 7, 4, 4) }; exeRow.Controls.Add(ffmpegBox, 1, 0); editControls.Add(ffmpegBox);
            exeRow.Controls.Add(ButtonOf("浏览…", delegate { using (OpenFileDialog d = new OpenFileDialog { Filter = "FFmpeg|ffmpeg.exe|可执行文件|*.exe", Title = "选择已安装的 ffmpeg.exe" }) { if (d.ShowDialog(this) == DialogResult.OK) ffmpegBox.Text = d.FileName; } }, true), 2, 0); root.Controls.Add(exeRow, 0, 1);

            TableLayoutPanel outRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            outRow.Controls.Add(LabelOf("输出目录", 9, true), 0, 0);
            outputBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 7, 4, 4) }; editControls.Add(outputBox); outRow.Controls.Add(outputBox, 1, 0);
            folder = ButtonOf("选择…", delegate { using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "选择 MP4 保存目录", SelectedPath = Directory.Exists(outputBox.Text) ? outputBox.Text : "" }) { if (d.ShowDialog(this) == DialogResult.OK) { sameFolder.Checked = false; outputBox.Text = d.SelectedPath; } } }, true);
            outRow.Controls.Add(folder, 2, 0); root.Controls.Add(outRow, 0, 2);

            FlowLayoutPanel settings = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            FlowLayoutPanel modeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            modeRow.Controls.Add(LabelOf("转换方式", 9, true));
            mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270, Margin = new Padding(14, 0, 12, 0) };
            mode.Items.AddRange(new object[] { "快速无损（复制音视频流）", "重新编码（H.264 + AAC）" }); mode.SelectedIndex = 0; editControls.Add(mode); modeRow.Controls.Add(mode);
            sameFolder = new CheckBox { Text = "保存在每个原文件旁边", Checked = true, AutoSize = true, Margin = new Padding(12, 3, 0, 0) }; editControls.Add(sameFolder); modeRow.Controls.Add(sameFolder);
            sameFolder.CheckedChanged += delegate { outputBox.Enabled = !sameFolder.Checked && !workerBusy(); };
            settings.Controls.Add(modeRow);
            notice = LabelOf("无损模式不改变画质；若编码不兼容 MP4，请改用重新编码。", 9, false); notice.ForeColor = Color.FromArgb(91, 109, 131); notice.Margin = new Padding(0, 10, 0, 0); settings.Controls.Add(notice);
            mode.SelectedIndexChanged += delegate { notice.Text = mode.SelectedIndex == 0 ? "无损模式不改变画质；若编码不兼容 MP4，请改用重新编码。" : "H.264 / AAC，CRF 20；耗时和文件大小取决于视频，重新编码会有损耗。"; };
            root.Controls.Add(settings, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            actions.Controls.Add(ButtonOf("＋ 添加 TS", delegate { using (OpenFileDialog d = new OpenFileDialog { Multiselect = true, Filter = "TS 视频|*.ts|所有文件|*.*", Title = "选取一个或多个 TS 文件" }) { if (d.ShowDialog(this) == DialogResult.OK) AddFiles(d.FileNames); } }, true));
            actions.Controls.Add(ButtonOf("移除选中", delegate { foreach (ListViewItem r in list.SelectedItems.Cast<ListViewItem>().ToArray()) { jobs.Remove((Job)r.Tag); list.Items.Remove(r); } UpdateIdle(); }, true));
            actions.Controls.Add(ButtonOf("清空列表", delegate { jobs.Clear(); list.Items.Clear(); UpdateIdle(); }, true));
            actions.Controls.Add(LabelOf("  可将多个 .ts 文件拖入下面的列表", 9, false)); root.Controls.Add(actions, 0, 4);

            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, GridLines = false, AllowDrop = true, BorderStyle = BorderStyle.FixedSingle };
            list.Columns.Add("原文件", 300); list.Columns.Add("大小", 85); list.Columns.Add("状态 / 进度", 150); list.Columns.Add("输出文件", 330);
            list.DragEnter += delegate(object s, DragEventArgs e) { e.Effect = !workerBusy() && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; };
            list.DragDrop += delegate(object s, DragEventArgs e) { if (!workerBusy()) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); };
            list.DoubleClick += delegate { if (list.SelectedItems.Count == 0) return; Job j = (Job)list.SelectedItems[0].Tag; if (j.Error.Length > 0) ShowDetails(j.Error); else if (File.Exists(j.Output)) Reveal(j.Output); };
            root.Controls.Add(list, 0, 5);

            TableLayoutPanel progress = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0, 8, 0, 0) };
            currentText = LabelOf("当前文件：等待开始", 9, false); currentText.AutoSize = false; currentText.Dock = DockStyle.Fill; currentText.BackColor = BackColor;
            totalText = LabelOf("批量进度：0 个文件", 9, false); totalText.AutoSize = false; totalText.Dock = DockStyle.Fill; totalText.BackColor = BackColor;
            currentText.Margin = new Padding(0); currentText.AutoEllipsis = true; totalText.Margin = new Padding(0);
            current = new ProgressBar { Dock = DockStyle.Fill, Height = 12, Margin = new Padding(0, 2, 0, 5) };
            total = new ProgressBar { Dock = DockStyle.Fill, Height = 12, Margin = new Padding(0, 2, 0, 5) };
            for (int i = 0; i < 4; i++) progress.RowStyles.Add(new RowStyle(SizeType.Absolute, i % 2 == 0 ? 24 : 18));
            progress.Controls.Add(currentText, 0, 0); progress.Controls.Add(current, 0, 1); progress.Controls.Add(totalText, 0, 2); progress.Controls.Add(total, 0, 3); root.Controls.Add(progress, 0, 6);
            detailBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Text = "只保留第一路视频及全部音轨；不转换广播字幕或数据流。同名输出自动加编号。\r\n转换时先写入临时文件，成功后再保存为 MP4。双击失败项目可查看详细原因。" }; root.Controls.Add(detailBox, 0, 7);
            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            start = ButtonOf("开始 / 重试", StartBatch, false); start.BackColor = blue; start.ForeColor = Color.White; start.FlatAppearance.BorderColor = blue;
            stop = ButtonOf("取消批次", delegate { stop.Enabled = false; currentText.Text = "正在取消并清理临时文件…"; converter.Cancel(); }, false); stop.Enabled = false;
            bottom.Controls.Add(start); bottom.Controls.Add(stop);
            bottom.Controls.Add(ButtonOf("打开输出目录", delegate { string dir = lastOutput.Length > 0 ? Path.GetDirectoryName(lastOutput) : (!sameFolder.Checked ? outputBox.Text : (jobs.Count > 0 ? Path.GetDirectoryName(jobs[0].Input) : "")); if (Directory.Exists(dir)) OpenFolder(dir); else MessageBox.Show(this, "转换后即可打开输出目录。", "输出目录"); }, false));
            bottom.Controls.Add(ButtonOf("查看本次日志", delegate { if (File.Exists(logPath)) { try { Process.Start(new ProcessStartInfo("notepad.exe", Converter.Quote(logPath)) { UseShellExecute = false }); } catch (Exception ex) { ShowDetails(ex.Message); } } else MessageBox.Show(this, "开始转换后会生成日志。", "日志"); }, false));
            root.Controls.Add(bottom, 0, 8);
            outputBox.Enabled = false;
        }
        bool workerBusy() { return worker != null && worker.IsBusy; }
        void AddFiles(string[] paths) {
            if (workerBusy() || paths == null) return; int rejected = 0;
            foreach (string raw in paths) {
                try {
                    string path = Path.GetFullPath(raw);
                    if (!File.Exists(path) || !String.Equals(Path.GetExtension(path), ".ts", StringComparison.OrdinalIgnoreCase)) { rejected++; continue; }
                    if (jobs.Any(j => String.Equals(j.Input, path, StringComparison.OrdinalIgnoreCase))) continue;
                    Job job = new Job { Input = path };
                    job.Row = new ListViewItem(new string[] { Path.GetFileName(path), SizeOf(new FileInfo(path).Length), "等待", "" }) { Tag = job, ToolTipText = path };
                    jobs.Add(job); list.Items.Add(job.Row);
                } catch { rejected++; }
            }
            list.ShowItemToolTips = true; UpdateIdle();
            if (rejected > 0) detailBox.Text = "已忽略 " + rejected + " 个项目。请拖入实际存在的 .ts 文件（不支持文件夹）。";
        }
        string SizeOf(long bytes) { return bytes >= 1073741824L ? (bytes / 1073741824.0).ToString("F2") + " GB" : (bytes / 1048576.0).ToString("F1") + " MB"; }
        void UpdateIdle() { totalText.Text = "批量进度：" + jobs.Count + " 个文件"; }
        string LocateFFmpeg() {
            List<string> candidates = new List<string> { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"), @"C:\ffmpeg\bin\ffmpeg.exe" };
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(new char[] { Path.PathSeparator })) { try { candidates.Add(Path.Combine(dir.Trim().Trim(new char[] { '"' }), "ffmpeg.exe")); } catch { } }
            return candidates.FirstOrDefault(File.Exists) ?? "";
        }
        void LoadSettings() {
            ffmpegBox.Text = LocateFFmpeg();
            try {
                string path = Path.Combine(appDir, "settings.xml"); if (!File.Exists(path)) return;
                XmlDocument xml = new XmlDocument(); xml.XmlResolver = null; xml.Load(path);
                string exe = xml.SelectSingleNode("/settings/ffmpeg").InnerText; if (File.Exists(exe)) ffmpegBox.Text = exe;
                outputBox.Text = xml.SelectSingleNode("/settings/output").InnerText;
                sameFolder.Checked = xml.SelectSingleNode("/settings/sameFolder").InnerText != "false";
                mode.SelectedIndex = xml.SelectSingleNode("/settings/mode").InnerText == "encode" ? 1 : 0;
            } catch { }
        }
        void SaveSettings() {
            try {
                Directory.CreateDirectory(appDir);
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
                using (XmlWriter w = XmlWriter.Create(Path.Combine(appDir, "settings.xml"), settings)) {
                    w.WriteStartElement("settings"); w.WriteElementString("ffmpeg", ffmpegBox.Text); w.WriteElementString("output", outputBox.Text);
                    w.WriteElementString("sameFolder", sameFolder.Checked ? "true" : "false"); w.WriteElementString("mode", mode.SelectedIndex == 1 ? "encode" : "copy"); w.WriteEndElement();
                }
            } catch (Exception ex) { detailBox.Text = "设置未能保存：" + ex.Message; }
        }
        void Log(string text) {
            lock (logGate) { try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine, Encoding.UTF8); } catch { } }
        }
        void StartBatch(object sender, EventArgs e) {
            if (workerBusy()) return;
            string exe = ffmpegBox.Text.Trim().Trim(new char[] { '"' });
            if (!File.Exists(exe) || !String.Equals(Path.GetFileName(exe), "ffmpeg.exe", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "请先选取正确的 ffmpeg.exe。常用位置：C:\\ffmpeg\\bin\\ffmpeg.exe", "找不到 FFmpeg"); return; }
            if (!sameFolder.Checked && String.IsNullOrWhiteSpace(outputBox.Text)) { MessageBox.Show(this, "请选择输出目录，或勾选“保存在每个原文件旁边”。", "输出目录"); return; }
            runJobs = jobs.Where(j => j.State != "完成").ToArray();
            if (runJobs.Length == 0) { MessageBox.Show(this, jobs.Count == 0 ? "请先添加或拖入 TS 文件。" : "这些文件已经完成。添加新文件即可继续。", "待转换文件"); return; }
            runFFmpeg = Path.GetFullPath(exe); runFolder = sameFolder.Checked ? "" : outputBox.Text.Trim(); runEncode = mode.SelectedIndex == 1;
            try { Directory.CreateDirectory(Path.Combine(appDir, "logs")); logPath = Path.Combine(appDir, "logs", "convert-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".txt"); File.WriteAllText(logPath, "TS2MP4 1.0\r\n", Encoding.UTF8); }
            catch (Exception ex) { MessageBox.Show(this, "无法创建本地日志：" + ex.Message, "无法开始"); return; }
            SaveSettings(); converter = new Converter();
            foreach (Job j in runJobs) { j.State = "等待"; j.Error = ""; j.Row.SubItems[2].Text = "等待"; }
            foreach (Control c in editControls) c.Enabled = false;
            start.Enabled = false; stop.Enabled = true; current.Value = 0; total.Value = 0;
            detailBox.Text = "正在转换。取消会停止当前文件和后续队列；已经完成的 MP4 会保留。";
            worker.RunWorkerAsync();
        }
        void RunBatch(object sender, DoWorkEventArgs e) {
            int finished = 0;
            foreach (Job j in runJobs) {
                ConversionResult result = converter.Convert(runFFmpeg, j.Input, runFolder, runEncode,
                    delegate(double percent, string text) { worker.ReportProgress(0, new Update { Job = j, Percent = percent, Text = text, Finished = finished, Total = runJobs.Length }); }, Log);
                finished++; worker.ReportProgress(0, new Update { Job = j, Result = result, Finished = finished, Total = runJobs.Length });
                if (result.Cancelled) break;
            }
        }
        void OnProgress(object sender, ProgressChangedEventArgs e) {
            Update u = (Update)e.UserState;
            if (u.Result == null) {
                u.Job.State = "转换中";
                current.Style = u.Percent < 0 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
                if (u.Percent >= 0) current.Value = Math.Max(0, Math.Min(100, (int)u.Percent));
                u.Job.Row.SubItems[2].Text = u.Percent < 0 ? "处理中…" : u.Percent.ToString("F0") + "%";
                currentText.Text = "当前：" + Path.GetFileName(u.Job.Input) + " · " + u.Text;
            } else {
                ConversionResult r = u.Result;
                u.Job.State = r.Success ? "完成" : (r.Cancelled ? "已取消" : "失败"); u.Job.Error = r.Error; u.Job.Output = r.Output;
                u.Job.Row.SubItems[2].Text = u.Job.State; u.Job.Row.SubItems[3].Text = r.Output;
                u.Job.Row.ForeColor = r.Success ? Color.FromArgb(24, 122, 89) : (r.Cancelled ? ink : Color.FromArgb(180, 48, 48));
                if (r.Success) { lastOutput = r.Output; current.Style = ProgressBarStyle.Continuous; current.Value = 100; }
                else if (!r.Cancelled) { detailBox.Text = "失败：" + Path.GetFileName(u.Job.Input) + "\r\n" + r.Error + (runEncode ? "" : "\r\n编码不兼容时，可以选择“重新编码”再点击“开始 / 重试”。"); }
            }
            total.Value = Math.Min(100, u.Finished * 100 / u.Total); totalText.Text = "本次批量：已处理 " + u.Finished + " / " + u.Total + " 个文件（含失败或取消）";
        }
        void OnDone(object sender, RunWorkerCompletedEventArgs e) {
            if (e.Error != null) { Log(e.Error.ToString()); detailBox.Text = "批次异常：" + e.Error.Message; foreach (Job j in runJobs.Where(j => j.State == "转换中")) { j.State = "失败"; j.Row.SubItems[2].Text = "失败"; } }
            foreach (Control c in editControls) c.Enabled = true;
            outputBox.Enabled = !sameFolder.Checked; start.Enabled = true; stop.Enabled = false;
            current.Style = ProgressBarStyle.Continuous;
            int ok = runJobs.Count(j => j.State == "完成"), failed = runJobs.Count(j => j.State == "失败");
            currentText.Text = "批次结束：成功 " + ok + "，失败 " + failed + "，取消 / 未完成 " + (runJobs.Length - ok - failed);
            if (failed == 0) detailBox.Text = currentText.Text + "\r\n已完成项目不会重复转换。可以添加新文件，或重试取消 / 未完成项目。";
            if (closing) { closing = false; Close(); }
        }
        void OnClosing(object sender, FormClosingEventArgs e) {
            if (workerBusy()) {
                e.Cancel = true;
                if (!closing && MessageBox.Show(this, "正在转换。取消本批次并关闭？已完成的文件会保留。", "关闭工具", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { closing = true; stop.Enabled = false; converter.Cancel(); }
            } else SaveSettings();
        }
        void ShowDetails(string text) { using (Form f = new Form { Text = "详细信息", Size = new Size(740, 430), StartPosition = FormStartPosition.CenterParent }) { f.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = text, Font = Font }); f.ShowDialog(this); } }
        void OpenFolder(string dir) { try { Process.Start(new ProcessStartInfo("explorer.exe", Converter.Quote(dir)) { UseShellExecute = false }); } catch (Exception ex) { ShowDetails(ex.Message); } }
        void Reveal(string path) { try { Process.Start(new ProcessStartInfo("explorer.exe", "/select," + Converter.Quote(path)) { UseShellExecute = false }); } catch (Exception ex) { ShowDetails(ex.Message); } }
    }
    public static class Program {
        public static string StartupLogPath {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TS2MP4", "startup-runtime.txt"); }
        }
        public static void StartupLog(string message) {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(StartupLogPath));
                File.AppendAllText(StartupLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine, Encoding.UTF8);
            } catch { }
        }
        private static void ReportError(Exception ex) {
            StartupLog(ex.ToString());
            try { MessageBox.Show("程序启动或运行失败：\r\n" + ex.Message + "\r\n\r\n详细日志：\r\n" + StartupLogPath, "TS2MP4 错误", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
        [STAThread]
        public static void Main(string[] args) {
            try {
                StartupLog("入口已执行。CLR=" + Environment.Version + "; OS=" + Environment.OSVersion + "; 64bit=" + Environment.Is64BitProcess);
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { StartupLog("未捕获异常：" + e.ExceptionObject); };
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { ReportError(e.Exception); };
                StartupLog("正在创建主窗口。");
                using (MainForm form = new MainForm()) {
                    form.Shown += delegate { StartupLog("主窗口已显示。"); };
                    Application.Run(form);
                }
                StartupLog("程序已退出。");
            } catch (Exception ex) { ReportError(ex); Environment.ExitCode = 1; }
        }
    }
}
