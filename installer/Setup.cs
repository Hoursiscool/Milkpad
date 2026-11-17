using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

static class MilkpadSetup
{
    const string PayloadResource = "Milkpad.Payload.zip";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Milkpad";
    const string Version = "1.1.0";

    static readonly string DefaultTarget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Milkpad");

    [STAThread]
    static void Main(string[] args)
    {
        string target = DefaultTarget;
        bool useShortcuts = true, silent = false, launch = false;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? "";
            if (arg == "--target" && i + 1 < args.Length) target = args[++i];
            else if (arg.StartsWith("--target=", StringComparison.OrdinalIgnoreCase)) target = arg.Substring("--target=".Length);
            else if (arg == "--no-shortcuts") useShortcuts = false;
            else if (arg == "--silent") silent = true;
            else if (arg == "--launch") launch = true;
            else if (arg == "--help" || arg == "/?")
            {
                MessageBox.Show("Milkpad setup\n\n--target <folder>  choose the install folder\n--no-shortcuts     do not create Start Menu or Desktop shortcuts\n--silent           install without showing the wizard\n--launch           start Milkpad after installing", "Milkpad setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (silent)
        {
            try
            {
                Install(target, useShortcuts, launch);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Milkpad setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        Application.Run(new SetupForm(target, useShortcuts));
    }

    internal static void Install(string destination, bool shortcuts, bool launch)
    {
        destination = Path.GetFullPath(destination.Trim());
        if (destination.Length < 4 || String.Equals(destination.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(destination).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a folder for Milkpad; a drive root cannot be used.");
        if (Process.GetProcessesByName("milkpad").Length > 0)
            throw new InvalidOperationException("Milkpad is running. Close it, then try again.");

        Directory.CreateDirectory(destination);
        ExtractPayload(destination);
        RemoveObsoleteFiles(destination);
        if (shortcuts) CreateShortcuts(destination);
        WriteUninstallEntry(destination);
        if (launch) Launch(destination);
    }

    static void ExtractPayload(string destination)
    {
        using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource))
        {
            if (payload == null) throw new InvalidOperationException("The setup payload is missing from this file. Rebuild Milkpad-Setup.exe with installer\\build-installer.cmd.");
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
            {
                string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string name = (entry.FullName ?? "").Replace('\\', '/');
                    if (name.Length == 0 || name.EndsWith("/", StringComparison.Ordinal)) continue;
                    string path = Path.GetFullPath(Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar)));
                    if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The setup payload contains an unsafe path.");
                    string folder = Path.GetDirectoryName(path);
                    if (!String.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                    using (Stream source = entry.Open())
                    using (FileStream target = File.Create(path))
                        source.CopyTo(target);
                }
            }
        }
    }

    // Old builds shipped these factory files in different folders. Removing only these
    // known names keeps any themes the user added themselves.
    static void RemoveObsoleteFiles(string destination)
    {
        TryDelete(Path.Combine(destination, "themes", "fun-gang-x-acerola.cfg"));
        TryDeleteDirectory(Path.Combine(destination, "themes", "extra"));
        TryDeleteDirectory(Path.Combine(destination, "scripts"));
        TryDelete(Path.Combine(destination, "install-sega-genesis.cmd"));
    }

    static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    static void CreateShortcuts(string destination)
    {
        string executable = Path.Combine(destination, "milkpad.exe");
        string uninstaller = Path.Combine(destination, "Uninstall-Milkpad.exe");
        string programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Milkpad");
        Directory.CreateDirectory(programs);
        CreateShortcut(Path.Combine(programs, "Milkpad.lnk"), executable, "", executable + ",0", "Milkpad editor");
        CreateShortcut(Path.Combine(programs, "Uninstall Milkpad.lnk"), uninstaller, "", uninstaller + ",0", "Uninstall Milkpad");
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (Directory.Exists(desktop)) CreateShortcut(Path.Combine(desktop, "Milkpad.lnk"), executable, "", executable + ",0", "Milkpad editor");
    }

    // WScript.Shell is always present, and late binding keeps this single-file setup
    // free of an interop assembly.
    static void CreateShortcut(string linkPath, string targetPath, string arguments, string icon, string description)
    {
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            Type type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            if (arguments.Length > 0) type.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            if (icon.Length > 0) type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icon });
            if (description.Length > 0) type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch { }
    }

    static void WriteUninstallEntry(string destination)
    {
        string executable = Path.Combine(destination, "milkpad.exe");
        string uninstaller = Path.Combine(destination, "Uninstall-Milkpad.exe");
        long bytes = 0;
        try
        {
            foreach (string file in Directory.GetFiles(destination, "*", SearchOption.AllDirectories)) bytes += new FileInfo(file).Length;
        }
        catch { }
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            if (key == null) return;
            key.SetValue("DisplayName", "Milkpad");
            key.SetValue("DisplayVersion", Version);
            key.SetValue("Publisher", "Milkpad");
            key.SetValue("InstallLocation", destination);
            key.SetValue("DisplayIcon", executable + ",0");
            key.SetValue("UninstallString", "\"" + uninstaller + "\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)Math.Max(1, bytes / 1024), RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        }
    }

    static void Launch(string destination)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(destination, "milkpad.exe")) { WorkingDirectory = destination, UseShellExecute = true });
        }
        catch { }
    }

    internal sealed class SetupForm : Form
    {
        readonly TextBox folder = new TextBox();
        readonly CheckBox desktop = new CheckBox();
        readonly CheckBox launch = new CheckBox();
        readonly Button install = new Button();
        readonly Button cancel = new Button();
        readonly Label status = new Label();
        readonly ProgressBar progress = new ProgressBar();
        readonly Color accent = Color.FromArgb(220, 145, 196);
        readonly Color panel = Color.FromArgb(43, 34, 42);
        readonly Color ink = Color.FromArgb(238, 233, 237);
        readonly Color muted = Color.FromArgb(170, 162, 170);
        bool working;

        public SetupForm(string initialTarget, bool shortcuts)
        {
            Text = "Milkpad setup";
            ClientSize = new Size(620, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30); ForeColor = ink;
            Font = new Font("Segoe UI", 9F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var heading = new Label { Text = "Milkpad", AutoSize = true, Font = new Font("Segoe UI Semibold", 24F), ForeColor = accent, Location = new Point(24, 18) };
            var subtitle = new Label { Text = "A small, fast local editor for notes and code.", AutoSize = true, Font = new Font("Segoe UI", 10F), ForeColor = muted, Location = new Point(27, 62) };
            var blurb = new Label
            {
                Text = "Milkpad installs for this Windows user only. No administrator rights are required.\nYour documents, themes and settings stay in %APPDATA%\\Milkpad and are not removed by an upgrade.",
                AutoSize = false, Width = 566, Height = 42, ForeColor = muted, Location = new Point(27, 92)
            };
            var folderLabel = new Label { Text = "Install folder", AutoSize = true, ForeColor = ink, Location = new Point(27, 142) };
            folder.Text = initialTarget; folder.SetBounds(27, 164, 470, 24); folder.BackColor = panel; folder.ForeColor = ink; folder.BorderStyle = BorderStyle.FixedSingle;
            var browse = new Button { Text = "Browse…", FlatStyle = FlatStyle.Flat, BackColor = panel, ForeColor = ink, Location = new Point(505, 162), Size = new Size(88, 27), Cursor = Cursors.Hand };
            browse.FlatAppearance.BorderColor = Color.FromArgb(88, 76, 88);
            browse.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog { Description = "Choose where Milkpad should be installed", SelectedPath = folder.Text })
                    if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath;
            };

            desktop.Text = "Create a Desktop shortcut"; desktop.Checked = shortcuts; desktop.AutoSize = true; desktop.ForeColor = ink; desktop.Location = new Point(27, 201);
            launch.Text = "Start Milkpad when setup finishes"; launch.Checked = true; launch.AutoSize = true; launch.ForeColor = ink; launch.Location = new Point(27, 227);
            status.AutoSize = false; status.SetBounds(27, 266, 566, 42); status.ForeColor = muted;
            progress.Style = ProgressBarStyle.Marquee; progress.MarqueeAnimationSpeed = 28; progress.SetBounds(27, 316, 566, 8); progress.Visible = false;

            install.Text = "Install Milkpad"; install.FlatStyle = FlatStyle.Flat; install.BackColor = accent; install.ForeColor = Color.FromArgb(33, 23, 33); install.SetBounds(27, 350, 156, 36); install.Cursor = Cursors.Hand;
            install.FlatAppearance.BorderSize = 0;
            cancel.Text = "Cancel"; cancel.FlatStyle = FlatStyle.Flat; cancel.BackColor = panel; cancel.ForeColor = ink; cancel.SetBounds(195, 350, 90, 36); cancel.Cursor = Cursors.Hand; cancel.DialogResult = DialogResult.Cancel;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(88, 76, 88);
            install.Click += (s, e) => InstallClicked();
            var note = new Label { Text = "Existing documents and settings are never overwritten.", AutoSize = true, ForeColor = Color.FromArgb(130, 124, 132), Location = new Point(27, 396) };

            Controls.Add(heading); Controls.Add(subtitle); Controls.Add(blurb); Controls.Add(folderLabel); Controls.Add(folder); Controls.Add(browse);
            Controls.Add(desktop); Controls.Add(launch); Controls.Add(status); Controls.Add(progress); Controls.Add(install); Controls.Add(cancel); Controls.Add(note);
            AcceptButton = install; CancelButton = cancel;
        }

        void InstallClicked()
        {
            if (working) return;
            string destination;
            try { destination = Path.GetFullPath(folder.Text.Trim()); }
            catch { status.Text = "Choose a valid install folder."; return; }
            if (destination.Length < 4 || String.Equals(destination.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(destination).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                status.Text = "Choose a folder for Milkpad; a drive root cannot be used.";
                return;
            }
            if (File.Exists(Path.Combine(destination, "milkpad.exe")))
            {
                if (MessageBox.Show(this, "Milkpad is already installed in this folder. Replace it with this version?\n\nYour documents and settings will be kept.", "Upgrade Milkpad", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }
            SetWorking(true);
            status.Text = "Installing Milkpad…";
            Application.DoEvents();
            try
            {
                Install(destination, desktop.Checked, false);
                status.Text = "Installed to " + destination;
                progress.Visible = false;
                MessageBox.Show(this, "Milkpad is installed.\n\nYou can open it from the Start Menu" + (desktop.Checked ? ", the Desktop shortcut" : "") + " or Settings → Apps.", "Milkpad setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (launch.Checked) Launch(destination);
                Close();
            }
            catch (Exception ex)
            {
                progress.Visible = false;
                status.Text = "Setup could not finish.";
                SetWorking(false);
                MessageBox.Show(this, ex.Message, "Milkpad setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void SetWorking(bool busy)
        {
            working = busy;
            folder.Enabled = !busy; install.Enabled = !busy; cancel.Enabled = !busy; desktop.Enabled = !busy; launch.Enabled = !busy;
            progress.Visible = busy;
        }
    }
}
