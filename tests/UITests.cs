using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Forms;
using TS2MP4;
class UITests {
    class TestForm : MainForm {
        public bool AcceptDeletion;
        public int Confirmations;
        protected override bool ConfirmOriginalDeletion(int count) { Confirmations++; return AcceptDeletion; }
    }
    static object Field(MainForm f, string name) { return typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f); }
    [STAThread] static void Main(string[] args) {
        if (args.Length != 3) throw new ArgumentException("Usage: UITests input-ts screenshot-path ffmpeg-exe");
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (TestForm f = new TestForm()) {
            f.Show(); Application.DoEvents();
            var add = typeof(MainForm).GetMethod("AddFiles", BindingFlags.Instance | BindingFlags.NonPublic);
            add.Invoke(f, new object[] { new string[] { args[0], args[0], Path.Combine(Path.GetDirectoryName(args[0]), "silent.ts") } });
            Application.DoEvents();
            ListView list = (ListView)Field(f, "list");
            if (list.Items.Count != 2 || !list.AllowDrop) throw new Exception("queue / duplicate / drop support");
            ((ComboBox)Field(f,"mode")).SelectedIndex = 1;
            ((CheckBox)Field(f,"sameFolder")).Checked = false;
            if (!((TextBox)Field(f,"outputBox")).Enabled) throw new Exception("output selection");
            Console.WriteLine("PASS GUI queue, deduplication, drag target, mode and folder controls");
            foreach (string n in new string[] { "currentText", "totalText", "current", "total" }) { Control c = (Control)Field(f,n); Console.WriteLine(n + " " + c.Bounds + " visible=" + c.Visible); }
            ((TextBox)Field(f,"ffmpegBox")).Text = @"C:\ffmpeg\bin\ffmpeg.exe";
            ((ComboBox)Field(f,"mode")).SelectedIndex = 0;
            ((CheckBox)Field(f,"sameFolder")).Checked = true;
            f.Refresh(); Application.DoEvents();
            for(int i=0;i<10;i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
            using (Bitmap b = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(b, new Rectangle(Point.Empty, f.Size)); b.Save(args[1]); }
            string dir = Path.GetDirectoryName(args[0]);
            DataObject data = new DataObject(); data.SetData(DataFormats.FileDrop, new string[] { Path.Combine(dir, "broken.ts") });
            typeof(Control).GetMethod("OnDragDrop", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(list, new object[] { new DragEventArgs(data,0,0,0,DragDropEffects.Copy,DragDropEffects.Copy) });
            if (list.Items.Count != 3) throw new Exception("drag-drop handler");
            ((TextBox)Field(f,"ffmpegBox")).Text = Path.GetFullPath(args[2]);
            ((CheckBox)Field(f,"sameFolder")).Checked = false;
            ((TextBox)Field(f,"outputBox")).Text = Path.Combine(dir,"gui-output");
            CheckBox deletion = (CheckBox)Field(f,"deleteOriginal");
            if (deletion.Checked) throw new Exception("deletion must default to off");
            deletion.Checked = true;
            ((Button)Field(f,"start")).PerformClick();
            if (f.Confirmations != 1 || ((BackgroundWorker)Field(f,"worker")).IsBusy || !File.Exists(args[0])) throw new Exception("declining deletion must not start a batch");
            deletion.Checked = false;
            ((Button)Field(f,"start")).PerformClick();
            if (((Button)Field(f,"start")).Enabled || !((Button)Field(f,"stop")).Enabled || deletion.Enabled) throw new Exception("running controls");
            DateTime deadline = DateTime.Now.AddSeconds(45);
            while ((((BackgroundWorker)Field(f,"worker")).IsBusy || !((Button)Field(f,"start")).Enabled) && DateTime.Now < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            Application.DoEvents();
            if (((BackgroundWorker)Field(f,"worker")).IsBusy) throw new Exception("GUI batch timeout");
            foreach (ListViewItem row in list.Items) { Job job = (Job)row.Tag; Console.WriteLine("GUI result: " + row.Text + " | " + row.SubItems[2].Text + " | " + job.Error + " | " + job.Warning); }
            if (list.Items[0].SubItems[2].Text != "完成" || list.Items[1].SubItems[2].Text != "完成" || list.Items[2].SubItems[2].Text != "失败") throw new Exception("GUI batch result states: " + ((TextBox)Field(f,"detailBox")).Text);
            if (!((Button)Field(f,"start")).Enabled || ((Button)Field(f,"stop")).Enabled) throw new Exception("restored controls");
            Console.WriteLine("PASS GUI drag-drop handler and real batch conversion, result states, controls");
            f.Close();
        }
        using (TestForm f = new TestForm()) {
            f.Show(); Application.DoEvents();
            if (((CheckBox)Field(f,"deleteOriginal")).Checked) throw new Exception("deletion must not persist across windows");
            string input = Path.Combine(Path.GetDirectoryName(args[0]), "gui-delete.ts"); File.Copy(args[0], input);
            typeof(MainForm).GetMethod("AddFiles", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, new object[] { new string[] { input } });
            ((TextBox)Field(f,"ffmpegBox")).Text = Path.GetFullPath(args[2]);
            ((CheckBox)Field(f,"sameFolder")).Checked = false;
            ((TextBox)Field(f,"outputBox")).Text = Path.Combine(Path.GetDirectoryName(input), "gui-delete-output");
            ((CheckBox)Field(f,"deleteOriginal")).Checked = true; f.AcceptDeletion = true;
            ((Button)Field(f,"start")).PerformClick();
            DateTime deadline = DateTime.Now.AddSeconds(45);
            while ((((BackgroundWorker)Field(f,"worker")).IsBusy || !((Button)Field(f,"start")).Enabled) && DateTime.Now < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
            Application.DoEvents();
            if (((BackgroundWorker)Field(f,"worker")).IsBusy) throw new Exception("GUI deletion batch timeout");
            ListView list = (ListView)Field(f,"list");
            if (f.Confirmations != 1 || File.Exists(input) || list.Items[0].SubItems[2].Text != "完成 · 已删 TS") throw new Exception("confirmed UI deletion result");
            if (((CheckBox)Field(f,"deleteOriginal")).Checked) throw new Exception("deletion must reset after a batch");
            Console.WriteLine("PASS GUI deletion defaults, decline, confirmation, result state and per-batch reset");
            f.Close();
        }
    }
}
