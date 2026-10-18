using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ValRally.Launcher
{
    /// <summary>The launcher window: Valheim and ValRally status, Play / Update / Uninstall, and a log.</summary>
    public sealed class MainForm : Form
    {
        private readonly Launcher launcher;
        private readonly string zipOverride;

        private readonly Label valheimLabel = new Label { AutoSize = false, AutoEllipsis = true, Width = 530, Height = 20 };
        private readonly Label modLabel = new Label { AutoSize = true };
        private readonly Label updateLabel = new Label { AutoSize = true };
        private readonly Button playButton = new Button { Width = 150, Height = 40 };
        private readonly Button updateButton = new Button { Text = "Update", Width = 100, Height = 40 };
        private readonly Button uninstallButton = new Button { Text = "Uninstall", Width = 100, Height = 40 };
        private readonly LinkLabel zipLink = new LinkLabel { Text = "Install from zip…", AutoSize = true };
        private readonly LinkLabel valheimLink = new LinkLabel { Text = "Change Valheim folder…", AutoSize = true };
        private readonly LinkLabel folderLink = new LinkLabel { Text = "Open ValRally folder", AutoSize = true };
        private readonly LinkLabel releasesLink = new LinkLabel { Text = "Releases page", AutoSize = true };
        private readonly TextBox logBox = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
            BackColor = SystemColors.Window, Font = new Font(FontFamily.GenericMonospace, 8.5f),
        };
        private bool busy;

        public MainForm(LauncherPaths paths, string valheimOverride, string zipOverride)
        {
            this.zipOverride = zipOverride == null ? null : Path.GetFullPath(zipOverride);
            launcher = new Launcher(paths, Log, valheimOverride);

            Text = $"ValRally Launcher {LauncherConfig.LauncherVersion.ToString(3)}";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 380);
            MinimumSize = new Size(480, 340);
            StartPosition = FormStartPosition.CenterScreen;

            var title = new Label { Text = "ValRally", AutoSize = true, Font = new Font("Segoe UI", 18f, FontStyle.Bold) };
            var info = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            info.Controls.AddRange(new Control[] { title, valheimLabel, modLabel, updateLabel });

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            buttons.Controls.AddRange(new Control[] { playButton, updateButton, uninstallButton });

            var links = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            links.Controls.AddRange(new Control[] { zipLink, valheimLink, folderLink, releasesLink });

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(info);
            layout.Controls.Add(buttons);
            layout.Controls.Add(links);
            layout.Controls.Add(logBox);
            Controls.Add(layout);

            playButton.Click += async (s, e) => await Run(OnPlay);
            updateButton.Click += async (s, e) => await Run(Install);
            uninstallButton.Click += async (s, e) => await Run(OnUninstall);
            zipLink.LinkClicked += async (s, e) => await Run(OnInstallFromZip);
            valheimLink.LinkClicked += (s, e) => OnChangeValheim();
            folderLink.LinkClicked += (s, e) => { Directory.CreateDirectory(paths.Home); Steam.Open(paths.Home); };
            releasesLink.LinkClicked += (s, e) => Steam.Open(launcher.Latest?.PageUrl ?? LauncherConfig.ReleasesPage);
            Shown += async (s, e) => await Run(OnStart);
            FormClosed += (s, e) => launcher.Dispose();
        }

        private async Task OnStart()
        {
            launcher.FindValheim();
            Log(launcher.ValheimPath != null ? $"Valheim: {launcher.ValheimPath}" : "Valheim wasn't found in your Steam libraries.");
            if (zipOverride != null) Log($"Testing with a local zip: {zipOverride}");
            RefreshView();
            await launcher.CheckForUpdates();
        }

        private async Task OnPlay()
        {
            if (launcher.ValheimPath == null)
            {
                Steam.OpenStorePage();
                return;
            }
            if (!launcher.State.IsInstalled) await Install();
            else launcher.Play();
        }

        private async Task Install()
        {
            if (zipOverride != null) await launcher.InstallFromZip(zipOverride);
            else await launcher.InstallLatest();
        }

        private async Task OnInstallFromZip()
        {
            using var dialog = new OpenFileDialog { Filter = "ValRally zip (*.zip)|*.zip", Title = "Pick a ValRally release zip" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await launcher.InstallFromZip(dialog.FileName);
        }

        private Task OnUninstall()
        {
            var answer = MessageBox.Show(this,
                "Remove ValRally, BepInEx and Jotunn from the launcher's folder, and the launcher's files from the Valheim folder?\n\n" +
                "Keep your ValRally settings, saved stages and tunes?\n\nYes: keep them    No: delete them too",
                "Uninstall ValRally", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.Cancel) launcher.Uninstall(keepConfig: answer == DialogResult.Yes);
            return Task.CompletedTask;
        }

        private void OnChangeValheim()
        {
            using var dialog = new FolderBrowserDialog { Description = "Pick the Valheim folder (the one with valheim.exe)" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!Steam.IsValheimFolder(dialog.SelectedPath))
            {
                MessageBox.Show(this, "That folder has no valheim.exe.", "ValRally", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            launcher.SetValheimPath(dialog.SelectedPath);
            RefreshView();
        }

        /// <summary>Runs an action with the buttons disabled, showing any error.</summary>
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            RefreshView();
            try
            {
                await action();
            }
            catch (Exception e)
            {
                var message = e.GetBaseException().Message;
                Log($"Error: {message}");
                MessageBox.Show(this, message, "ValRally", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                launcher.Installer.LoadState();
                RefreshView();
            }
        }

        private void RefreshView()
        {
            var state = launcher.State;
            var found = launcher.ValheimPath != null;
            valheimLabel.Text = found ? $"Valheim: {launcher.ValheimPath}" : "Valheim: not found. Install it from Steam, or pick its folder below.";
            modLabel.Text = state.IsInstalled ? $"ValRally {state.ModVersion} installed" : "ValRally isn't installed yet";

            var latest = launcher.Latest;
            if (zipOverride != null) updateLabel.Text = $"Installing from {Path.GetFileName(zipOverride)}";
            else if (latest == null) updateLabel.Text = launcher.UpdateCheckError ?? "Checking GitHub for updates…";
            else if (launcher.UpdateAvailable && state.IsInstalled) updateLabel.Text = $"ValRally {latest.Version.ToString(3)} is available";
            else updateLabel.Text = $"Latest on GitHub: {latest.Version.ToString(3)}";
            if (launcher.LauncherUpdateAvailable) updateLabel.Text += $". A newer launcher is on the Releases page.";

            playButton.Text = !found ? "Get Valheim on Steam" : state.IsInstalled ? "Play" : "Install";
            playButton.Enabled = !busy && (!found || state.IsInstalled || latest != null || zipOverride != null);
            updateButton.Enabled = !busy && found && state.IsInstalled && (launcher.UpdateAvailable || zipOverride != null);
            uninstallButton.Enabled = !busy && (state.IsInstalled || Directory.Exists(launcher.Paths.Profile));
            zipLink.Enabled = valheimLink.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }
            logBox.AppendText(message + Environment.NewLine);
        }
    }
}
