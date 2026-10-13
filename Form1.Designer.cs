namespace WinFormsApp1
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeIcons();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            headerPanel = new Panel();
            accountCaption = new Label();
            accountLabel = new Label();
            scanButton = new Button();
            saveButton = new Button();
            gamesGrid = new DataGridView();
            iconColumn = new DataGridViewImageColumn();
            appIdColumn = new DataGridViewTextBoxColumn();
            nameColumn = new DataGridViewTextBoxColumn();
            installedColumn = new DataGridViewTextBoxColumn();
            idleColumn = new DataGridViewCheckBoxColumn();
            statusLabel = new Label();
            headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gamesGrid).BeginInit();
            SuspendLayout();
            //
            // headerPanel
            //
            headerPanel.Controls.Add(accountCaption);
            headerPanel.Controls.Add(accountLabel);
            headerPanel.Controls.Add(scanButton);
            headerPanel.Controls.Add(saveButton);
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Location = new Point(0, 0);
            headerPanel.Name = "headerPanel";
            headerPanel.Padding = new Padding(12, 10, 12, 10);
            headerPanel.Size = new Size(884, 64);
            headerPanel.TabIndex = 0;
            //
            // accountCaption
            //
            accountCaption.AutoSize = true;
            accountCaption.Location = new Point(12, 12);
            accountCaption.Name = "accountCaption";
            accountCaption.Size = new Size(115, 15);
            accountCaption.TabIndex = 0;
            accountCaption.Text = "Logged in account";
            //
            // accountLabel
            //
            accountLabel.AutoSize = true;
            accountLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            accountLabel.Location = new Point(12, 30);
            accountLabel.Name = "accountLabel";
            accountLabel.Size = new Size(92, 20);
            accountLabel.TabIndex = 1;
            accountLabel.Text = "Not scanned";
            //
            // scanButton
            //
            scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            scanButton.Location = new Point(648, 16);
            scanButton.Name = "scanButton";
            scanButton.Size = new Size(100, 32);
            scanButton.TabIndex = 2;
            scanButton.Text = "Scan";
            scanButton.UseVisualStyleBackColor = true;
            scanButton.Click += ScanButton_Click;
            //
            // saveButton
            //
            saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            saveButton.Enabled = false;
            saveButton.Location = new Point(756, 16);
            saveButton.Name = "saveButton";
            saveButton.Size = new Size(116, 32);
            saveButton.TabIndex = 3;
            saveButton.Text = "Save JSON";
            saveButton.UseVisualStyleBackColor = true;
            saveButton.Click += SaveButton_Click;
            //
            // gamesGrid
            //
            gamesGrid.AllowUserToAddRows = false;
            gamesGrid.AllowUserToDeleteRows = false;
            gamesGrid.AllowUserToResizeRows = false;
            gamesGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gamesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gamesGrid.BackgroundColor = SystemColors.Window;
            gamesGrid.BorderStyle = BorderStyle.None;
            gamesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gamesGrid.Columns.AddRange(new DataGridViewColumn[] { iconColumn, appIdColumn, nameColumn, installedColumn, idleColumn });
            gamesGrid.Location = new Point(12, 76);
            gamesGrid.MultiSelect = false;
            gamesGrid.Name = "gamesGrid";
            gamesGrid.ReadOnly = false;
            gamesGrid.RowHeadersVisible = false;
            gamesGrid.RowTemplate.Height = 36;
            gamesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gamesGrid.Size = new Size(860, 430);
            gamesGrid.TabIndex = 1;
            //
            // iconColumn
            //
            iconColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            iconColumn.FillWeight = 1F;
            iconColumn.HeaderText = "";
            iconColumn.ImageLayout = DataGridViewImageCellLayout.Zoom;
            iconColumn.MinimumWidth = 48;
            iconColumn.Name = "iconColumn";
            iconColumn.ReadOnly = true;
            iconColumn.Width = 48;
            //
            // appIdColumn
            //
            appIdColumn.FillWeight = 25F;
            appIdColumn.HeaderText = "AppID";
            appIdColumn.Name = "appIdColumn";
            appIdColumn.ReadOnly = true;
            appIdColumn.SortMode = DataGridViewColumnSortMode.Automatic;
            //
            // nameColumn
            //
            nameColumn.FillWeight = 150F;
            nameColumn.HeaderText = "Game";
            nameColumn.Name = "nameColumn";
            nameColumn.ReadOnly = true;
            nameColumn.SortMode = DataGridViewColumnSortMode.Automatic;
            //
            // installedColumn
            //
            installedColumn.FillWeight = 25F;
            installedColumn.HeaderText = "Installed";
            installedColumn.Name = "installedColumn";
            installedColumn.ReadOnly = true;
            installedColumn.SortMode = DataGridViewColumnSortMode.Automatic;
            //
            // idleColumn
            //
            idleColumn.FillWeight = 20F;
            idleColumn.HeaderText = "Idle?";
            idleColumn.Name = "idleColumn";
            idleColumn.ToolTipText = "Tell Steam this game is running. Up to 34 at once.";
            //
            // statusLabel
            //
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            statusLabel.AutoEllipsis = true;
            statusLabel.Location = new Point(12, 516);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(860, 20);
            statusLabel.TabIndex = 2;
            statusLabel.Text = "Scan reads the account currently logged in to Steam.";
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(884, 546);
            Controls.Add(statusLabel);
            Controls.Add(gamesGrid);
            Controls.Add(headerPanel);
            MinimumSize = new Size(640, 360);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Steam library";
            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gamesGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel headerPanel;
        private Label accountCaption;
        private Label accountLabel;
        private Button scanButton;
        private Button saveButton;
        private DataGridView gamesGrid;
        private DataGridViewImageColumn iconColumn;
        private DataGridViewTextBoxColumn appIdColumn;
        private DataGridViewTextBoxColumn nameColumn;
        private DataGridViewTextBoxColumn installedColumn;
        private DataGridViewCheckBoxColumn idleColumn;
        private Label statusLabel;
    }
}
