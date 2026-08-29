namespace NovaCrypto;

/// <summary>Collects only a public OAuth client ID. GitHub handles user credentials in its browser page.</summary>
sealed class GitHubSignInDialog : Form
{
    readonly TextBox _clientId = new() { Left = 16, Top = 103, Width = 420, PlaceholderText = "OAuth App Client ID" };

    public string ClientId => _clientId.Text.Trim();

    public GitHubSignInDialog()
    {
        Text = "Sign in with GitHub"; ClientSize = new Size(455, 190); FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
        var explanation = new Label
        {
            Left = 16, Top = 15, Width = 420, Height = 75,
            Text = "NovaGit never asks for your GitHub username, email, password, or personal access token. Click Continue and sign in only on GitHub's secure browser page.\r\n\r\nEnter the public Client ID from your own GitHub OAuth App:",
            AutoSize = false
        };
        var continueButton = new Button { Text = "Continue to GitHub", Left = 196, Top = 145, Width = 120, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Left = 326, Top = 145, Width = 110, DialogResult = DialogResult.Cancel };
        Controls.AddRange([explanation, _clientId, continueButton, cancelButton]); AcceptButton = continueButton; CancelButton = cancelButton;
        FormClosing += (_, e) => { if (DialogResult == DialogResult.OK && string.IsNullOrWhiteSpace(ClientId)) { MessageBox.Show("Enter your GitHub OAuth App Client ID.", "NovaGit", MessageBoxButtons.OK, MessageBoxIcon.Warning); e.Cancel = true; } };
    }
}
