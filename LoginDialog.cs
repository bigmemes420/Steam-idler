namespace WinFormsApp1;

internal sealed class LoginDialog : Form
{
    private readonly TextBox _username = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _password = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, UseSystemPasswordChar = true };
    private readonly TextBox _code = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };

    public LoginDialog(string accountName)
    {
        Text = "Log in to Steam";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(360, 214);
        _username.Text = accountName;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Account", _username);
        AddRow(layout, 1, "Password", _password);
        AddRow(layout, 2, "Guard code", _code);

        var note = new Label
        {
            Text = "The guard code is only needed when Steam asks for one.",
            AutoSize = true,
            Dock = DockStyle.Fill,
        };
        layout.Controls.Add(note, 0, 3);
        layout.SetColumnSpan(note, 2);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = "Log in", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string Username => _username.Text.Trim();

    public string Password => _password.Text;

    public string? AuthCode => string.IsNullOrWhiteSpace(_code.Text) ? null : _code.Text.Trim();

    public string? TwoFactorCode => AuthCode;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && (Username.Length == 0 || Password.Length == 0))
        {
            e.Cancel = true;
        }

        base.OnFormClosing(e);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string caption, Control input)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(input, 1, row);
    }
}
