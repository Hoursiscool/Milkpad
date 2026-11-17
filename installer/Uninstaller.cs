using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

static class MilkpadUninstaller
{
    static readonly string DefaultInstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Milkpad");
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Milkpad");
    static readonly string StartMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "Milkpad");
    static readonly string DesktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Milkpad.lnk");
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Milkpad";

    static string InstallDir
    {
        get
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    string location = key == null ? null : key.GetValue("InstallLocation") as string;
                    if (!String.IsNullOrEmpty(location)) return location;
                }
            }
            catch { }
            return DefaultInstallDir;
        }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var form = new Form
        {
            Text = "Uninstall Milkpad",
            ClientSize = new Size(520, 280),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(238, 233, 237),
            Font = new Font("Segoe UI", 9F)
        })
        {
            form.Controls.Add(new Label { Text = "Uninstall Milkpad", AutoSize = true, Font = new Font("Segoe UI Semibold", 18F), ForeColor = Color.FromArgb(220, 145, 196), Location = new Point(24, 20) });
            form.Controls.Add(new Label
            {
                Text = "This removes the installed program and its shortcuts. Your documents and settings in %APPDATA%\\Milkpad stay unless you choose to remove them below.",
                AutoSize = false, Width = 470, Height = 58, ForeColor = Color.FromArgb(190, 183, 190), Location = new Point(26, 64)
            });
            var data = new CheckBox { Text = "Also remove my Milkpad settings, sessions and documents", AutoSize = true, ForeColor = Color.FromArgb(238, 233, 237), Location = new Point(26, 134) };
            form.Controls.Add(data);
            form.Controls.Add(new Label { Text = "Installed location: " + InstallDir, AutoSize = false, Width = 470, ForeColor = Color.FromArgb(145, 138, 145), Location = new Point(26, 166) });
            var remove = new Button { Text = "Uninstall Milkpad", Width = 150, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 145, 196), ForeColor = Color.FromArgb(33, 23, 33), Location = new Point(26, 216), Cursor = Cursors.Hand };
            var cancel = new Button { Text = "Cancel", Width = 86, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 34, 42), ForeColor = Color.FromArgb(238, 233, 237), Location = new Point(188, 216), DialogResult = DialogResult.Cancel };
            remove.FlatAppearance.BorderSize = 0;
            cancel.FlatAppearance.BorderSize = 0;
            remove.Click += (s, e) => { if (Confirm()) { form.Close(); Uninstall(data.Checked); } };
            form.Controls.Add(remove); form.Controls.Add(cancel);
            form.AcceptButton = remove; form.CancelButton = cancel;
            Application.Run(form);
        }
    }

    static bool Confirm()
    {
        if (Process.GetProcessesByName("milkpad").Any())
        {
            MessageBox.Show("Close Milkpad before uninstalling it, then try again.", "Uninstall Milkpad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return MessageBox.Show("Remove the installed Milkpad program and shortcuts?", "Uninstall Milkpad", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    static void Uninstall(bool removeData)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                if (key != null) key.DeleteSubKeyTree("Milkpad", false);
        }
        catch { }
        try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch { }
        try { if (File.Exists(DesktopLink)) File.Delete(DesktopLink); } catch { }

        if (removeData)
        {
            try { if (Directory.Exists(DataDir)) Directory.Delete(DataDir, true); } catch { }
        }

        bool insideInstallDir;
        try
        {
            string exe = Path.GetFullPath(Application.ExecutablePath);
            insideInstallDir = exe.StartsWith(Path.GetFullPath(InstallDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { insideInstallDir = false; }

        if (Directory.Exists(InstallDir))
        {
            try
            {
                if (insideInstallDir)
                {
                    Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + InstallDir + "\"")
                    {
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false
                    });
                }
                else Directory.Delete(InstallDir, true);
            }
            catch { }
        }

        MessageBox.Show("Milkpad was removed." + (removeData ? "\n\nYour Milkpad data was removed too." : "\n\nYour documents and settings were kept."), "Uninstall Milkpad", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
