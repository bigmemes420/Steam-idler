using System.Diagnostics;
using Microsoft.Win32;

namespace WinFormsApp1
{
    public partial class Form1 : Form
    {
        private const int IconSize = 32;

        private SteamLibraryScan? _scan;
        private SteamIdle? _idle;
        private readonly Dictionary<uint, Image> _icons = new();
        private Image? _blankIcon;
        private bool _applyingIdle;

        public Form1()
        {
            InitializeComponent();
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? Icon;
            ApplyDarkTheme();
            gamesGrid.CellContentClick += GamesGrid_CellContentClick;
            gamesGrid.CurrentCellDirtyStateChanged += GamesGrid_CurrentCellDirtyStateChanged;
            FormClosing += (_, _) => _idle?.Dispose();
            Shown += (_, _) => ScanButton_Click(scanButton, EventArgs.Empty);
        }

        private async void ScanButton_Click(object? sender, EventArgs e)
        {
            scanButton.Enabled = false;
            saveButton.Enabled = false;
            statusLabel.Text = "Reading the Steam library...";
            gamesGrid.Rows.Clear();
            searchBox.Text = "";
            DisposeIcons();
            UseWaitCursor = true;

            try
            {
                var scan = await Task.Run(SteamLibraryScan.Run);
                _scan = scan;
                accountLabel.Text = $"{scan.Account.PersonaName} ({scan.Account.AccountName})";
                _blankIcon = new Bitmap(IconSize, IconSize);
                foreach (var game in scan.Games)
                {
                    gamesGrid.Rows.Add(_blankIcon, game.AppId, game.Name, game.Installed ? "Yes" : "", false);
                }

                statusLabel.Text = $"{scan.Games.Count} games from {scan.PackageCount} owned packages. Loading icons...";
                saveButton.Enabled = true;
                _ = LoadIconsAsync(scan);
            }
            catch (Exception ex)
            {
                _scan = null;
                accountLabel.Text = "No account";
                statusLabel.Text = ex.Message;
            }
            finally
            {
                UseWaitCursor = false;
                scanButton.Enabled = true;
            }
        }

        private async Task LoadIconsAsync(SteamLibraryScan scan)
        {
            var loaded = 0;
            foreach (var game in scan.Games)
            {
                if (IsDisposed || _scan != scan)
                {
                    return;
                }

                Image? icon = null;
                try
                {
                    var path = await Task.Run(() => SteamIcons.Find(scan.SteamPath, game.AppId));
                    if (path != null)
                    {
                        icon = await Task.Run(() => SteamIcons.LoadSquare(path, IconSize));
                    }
                }
                catch (Exception)
                {
                    icon = null;
                }

                if (icon != null && !IsDisposed && _scan == scan)
                {
                    _icons[game.AppId] = icon;
                    var row = RowFor(game.AppId);
                    if (row != null)
                    {
                        row.Cells[iconColumn.Index].Value = icon;
                    }

                    loaded++;
                    if (loaded % 25 == 0)
                    {
                        statusLabel.Text = $"Loaded {loaded} of {scan.Games.Count} icons.";
                    }
                }
            }

            if (!IsDisposed && _scan == scan)
            {
                statusLabel.Text = $"{scan.Games.Count} games, {loaded} icons. Check Idle? to tell Steam a game is running (up to {SteamIdle.MaxGames}).";
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            var query = searchBox.Text.Trim();
            gamesGrid.CurrentCell = null;
            foreach (DataGridViewRow row in gamesGrid.Rows)
            {
                var name = row.Cells[nameColumn.Index].Value as string ?? "";
                var appId = row.Cells[appIdColumn.Index].Value?.ToString() ?? "";
                row.Visible = query.Length == 0
                    || name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || appId.Contains(query, StringComparison.Ordinal);
            }
        }

        private DataGridViewRow? RowFor(uint appId)
        {
            foreach (DataGridViewRow row in gamesGrid.Rows)
            {
                if (row.Cells[appIdColumn.Index].Value is uint value && value == appId)
                {
                    return row;
                }
            }

            return null;
        }

        private void GamesGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (gamesGrid.IsCurrentCellDirty && gamesGrid.CurrentCell is DataGridViewCheckBoxCell)
            {
                gamesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private async void GamesGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != idleColumn.Index || _applyingIdle || _scan == null)
            {
                return;
            }

            gamesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            var row = gamesGrid.Rows[e.RowIndex];
            var appId = (uint)row.Cells[appIdColumn.Index].Value!;
            var idling = row.Cells[idleColumn.Index].Value is true;
            var selected = SelectedAppIds();
            if (idling && selected.Count > SteamIdle.MaxGames)
            {
                row.Cells[idleColumn.Index].Value = false;
                statusLabel.Text = $"Steam accepts at most {SteamIdle.MaxGames} games at once.";
                return;
            }

            _applyingIdle = true;
            try
            {
                await ApplyIdleAsync(idling ? selected : SelectedAppIds());
            }
            catch (Exception ex)
            {
                row.Cells[idleColumn.Index].Value = !idling;
                statusLabel.Text = ex.Message;
            }
            finally
            {
                _applyingIdle = false;
            }
        }

        private List<uint> SelectedAppIds()
        {
            var selected = new List<uint>();
            foreach (DataGridViewRow row in gamesGrid.Rows)
            {
                if (row.Cells[idleColumn.Index].Value is true)
                {
                    selected.Add((uint)row.Cells[appIdColumn.Index].Value!);
                }
            }

            return selected;
        }

        private async Task ApplyIdleAsync(IReadOnlyList<uint> appIds)
        {
            _idle ??= new SteamIdle();
            statusLabel.Text = appIds.Count == 0
                ? "Telling Steam nothing is running..."
                : "Telling the open Steam client...";
            await _idle.SetPlayingAsync(appIds);
            statusLabel.Text = appIds.Count == 0
                ? "Told Steam nothing is running."
                : $"Told Steam {appIds.Count} {(appIds.Count == 1 ? "game is" : "games are")} running.";
        }

        private async void StopAllButton_Click(object? sender, EventArgs e)
        {
            if (_applyingIdle)
            {
                return;
            }

            _applyingIdle = true;
            stopAllButton.Enabled = false;
            try
            {
                await ApplyIdleAsync([]);
                foreach (DataGridViewRow row in gamesGrid.Rows)
                {
                    row.Cells[idleColumn.Index].Value = false;
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = ex.Message;
            }
            finally
            {
                stopAllButton.Enabled = true;
                _applyingIdle = false;
            }
        }

        private async void RestartSteamButton_Click(object? sender, EventArgs e)
        {
            if (_applyingIdle)
            {
                return;
            }

            if (MessageBox.Show(this,
                    "This stops all idling and closes Steam, then starts it again. Any game running through Steam will be closed.\n\nRestart Steam now?",
                    "Restart Steam", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            string steamExe;
            try
            {
                steamExe = FindSteamExe();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Restart Steam", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _applyingIdle = true;
            restartSteamButton.Enabled = false;
            stopAllButton.Enabled = false;
            scanButton.Enabled = false;
            try
            {
                statusLabel.Text = "Stopping all idling...";
                await ApplyIdleAsync([]);
                foreach (DataGridViewRow row in gamesGrid.Rows)
                {
                    row.Cells[idleColumn.Index].Value = false;
                }

                if (IsSteamRunning())
                {
                    statusLabel.Text = "Asking Steam to exit...";
                    Process.Start(new ProcessStartInfo(steamExe, "-shutdown") { UseShellExecute = false })?.Dispose();
                    if (!await WaitUntilAsync(() => !IsSteamRunning(), TimeSpan.FromSeconds(30)))
                    {
                        statusLabel.Text = "Steam did not exit.";
                        MessageBox.Show(this,
                            "Steam is still running after 30 seconds, so it was not restarted. Close Steam from its tray icon and start it again yourself.",
                            "Restart Steam", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                statusLabel.Text = "Starting Steam...";
                Process.Start(new ProcessStartInfo(steamExe) { UseShellExecute = true })?.Dispose();
                statusLabel.Text = "Waiting for Steam to log in...";
                if (!await WaitUntilAsync(IsSteamLoggedIn, TimeSpan.FromSeconds(60)))
                {
                    statusLabel.Text = "Steam was started but is not logged in yet. Press Scan once it is.";
                    return;
                }

                statusLabel.Text = "Steam restarted.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Restart Steam", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                restartSteamButton.Enabled = true;
                stopAllButton.Enabled = true;
                scanButton.Enabled = true;
                _applyingIdle = false;
            }

            ScanButton_Click(scanButton, EventArgs.Empty);
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (await Task.Run(condition))
                {
                    return true;
                }

                await Task.Delay(500);
            }

            return await Task.Run(condition);
        }

        private static bool IsSteamRunning()
        {
            var processes = Process.GetProcessesByName("steam");
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return processes.Length > 0;
        }

        private static bool IsSteamLoggedIn()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            return key?.GetValue("ActiveUser") is int user && user != 0;
        }

        private static string FindSteamExe()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (key?.GetValue("SteamExe") is string exe && File.Exists(exe))
                {
                    return Path.GetFullPath(exe);
                }

                if (key?.GetValue("SteamPath") is string path && File.Exists(Path.Combine(path, "steam.exe")))
                {
                    return Path.GetFullPath(Path.Combine(path, "steam.exe"));
                }
            }

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ?? baseKey.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key?.GetValue("InstallPath") is string path && File.Exists(Path.Combine(path, "steam.exe")))
                {
                    return Path.Combine(path, "steam.exe");
                }
            }

            throw new InvalidOperationException("Could not find steam.exe. Is Steam installed?");
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (_scan == null)
            {
                return;
            }

            var account = string.Concat(_scan.Account.AccountName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"steam-games-{account}.json",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, _scan.ToJson());
            statusLabel.Text = $"Saved {_scan.Games.Count} games to {dialog.FileName}";
        }

        private void ApplyDarkTheme()
        {
            var background = Color.FromArgb(32, 34, 37);
            var panel = Color.FromArgb(40, 43, 48);
            var field = Color.FromArgb(24, 26, 28);
            var text = Color.FromArgb(220, 222, 225);
            var muted = Color.FromArgb(154, 160, 166);
            var line = Color.FromArgb(58, 62, 68);
            var accent = Color.FromArgb(70, 118, 176);

            BackColor = background;
            ForeColor = text;
            headerPanel.BackColor = panel;
            accountCaption.ForeColor = muted;
            accountLabel.ForeColor = text;
            statusLabel.ForeColor = muted;
            idleNoteLabel.ForeColor = muted;
            searchBox.BackColor = field;
            searchBox.ForeColor = text;
            searchBox.BorderStyle = BorderStyle.FixedSingle;

            foreach (var button in new[] { restartSteamButton, stopAllButton, scanButton, saveButton })
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = line;
                button.BackColor = field;
                button.ForeColor = text;
            }

            gamesGrid.BackgroundColor = background;
            gamesGrid.GridColor = line;
            gamesGrid.EnableHeadersVisualStyles = false;
            gamesGrid.BorderStyle = BorderStyle.None;
            gamesGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gamesGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            gamesGrid.DefaultCellStyle.BackColor = background;
            gamesGrid.DefaultCellStyle.ForeColor = text;
            gamesGrid.DefaultCellStyle.SelectionBackColor = accent;
            gamesGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            gamesGrid.ColumnHeadersDefaultCellStyle.BackColor = panel;
            gamesGrid.ColumnHeadersDefaultCellStyle.ForeColor = muted;
            gamesGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = panel;
            gamesGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = muted;
            gamesGrid.RowTemplate.DefaultCellStyle.BackColor = background;
            iconColumn.DefaultCellStyle.BackColor = background;
            iconColumn.DefaultCellStyle.SelectionBackColor = accent;
        }

        private void DisposeIcons()
        {
            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }

            _icons.Clear();
            _blankIcon?.Dispose();
            _blankIcon = null;
        }
    }
}
