using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NovaCrypto;

public sealed class GitMainForm : Form
{
    readonly ToolStrip _tools = new();
    readonly PictureBox _logo = new() { Width = 34, Height = 34, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(7, 4, 0, 4) };
    readonly Label _repository = new() { AutoSize = true, Text = "No repository selected", Padding = new Padding(10, 8, 10, 8) };
    readonly ListView _files = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true };
    readonly ListView _trackedFiles = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
    readonly RichTextBox _details = new() { Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Both, WordWrap = false, DetectUrls = false, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Gainsboro, Font = new Font("Cascadia Mono", 9) };
    readonly ListView _objects = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
    readonly ComboBox _objectFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    readonly TabControl _rightTabs = new() { Dock = DockStyle.Fill };
    readonly TextBox _blogTitle = new() { Dock = DockStyle.Top, PlaceholderText = "Post title" };
    readonly TextBox _blogBody = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, Font = new Font("Cascadia Mono", 10), PlaceholderText = "Write Markdown here..." };
    readonly TextBox _message = new() { Dock = DockStyle.Fill, PlaceholderText = "Commit message" };
    readonly ComboBox _branches = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    readonly GitClient _git = new();
    string? _root;
    bool _updatingBranches;
    bool _objectsLoaded;

    public GitMainForm()
    {
        Text = "NovaGit Desktop"; MinimumSize = new Size(900, 600); Size = new Size(1180, 760); StartPosition = FormStartPosition.CenterScreen; LoadBranding();
        _files.Columns.Add("State", 90); _files.Columns.Add("File", 420); _files.SelectedIndexChanged += async (_, _) => { if (_files.SelectedItems.Count == 0) return; _trackedFiles.SelectedItems.Clear(); await RunUi(ShowSelectedDiff); };
        _trackedFiles.Columns.Add("Repository file", 500); _trackedFiles.SelectedIndexChanged += async (_, _) => { if (_trackedFiles.SelectedItems.Count == 0) return; _files.SelectedItems.Clear(); await RunUi(ShowSelectedFile); };
        _trackedFiles.ItemActivate += async (_, _) => await RunUi(ShowSelectedFile);
        _objects.Columns.Add("Object ID", 285); _objects.Columns.Add("Type", 70); _objects.Columns.Add("Size", 85); _objects.Columns.Add("Delta base", 285);
        _objects.SelectedIndexChanged += async (_, _) => await RunUi(ShowSelectedObject);
        _objectFilter.Items.AddRange(["All", "commit", "tree", "blob", "tag"]); _objectFilter.SelectedIndex = 0; _objectFilter.SelectedIndexChanged += async (_, _) => await RunUi(LoadObjects);
        _rightTabs.SelectedIndexChanged += async (_, _) => { if (_rightTabs.SelectedIndex == 1 && !_objectsLoaded) await RunUi(LoadObjects); };
        Controls.Add(BuildLayout());
        BuildToolbar();
        Shown += async (_, _) => await RunUi(() => SelectRepository(Directory.GetCurrentDirectory()));
    }

    Control BuildLayout()
    {
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, AutoSize = false, WrapContents = false };
        header.Controls.Add(_logo); header.Controls.Add(_repository); header.Controls.Add(new Label { Text = "Branch:", AutoSize = true, Padding = new Padding(10, 8, 2, 8) }); header.Controls.Add(_branches);
        _branches.SelectedIndexChanged += async (_, _) => { if (!_updatingBranches && _branches.Focused && _branches.SelectedItem is string branch) await RunUi(() => Checkout(branch)); };
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 480 };
        var changesPage = new TabPage("Changes"); changesPage.Controls.Add(_files);
        var filesPage = new TabPage("Files");
        var filesHint = new Label { Dock = DockStyle.Top, Height = 26, Text = "Select, double-click, or press Enter on a file to preview its source.", Padding = new Padding(6, 5, 0, 0) };
        filesPage.Controls.Add(_trackedFiles); filesPage.Controls.Add(filesHint);
        var leftTabs = new TabControl { Dock = DockStyle.Fill }; leftTabs.TabPages.Add(changesPage); leftTabs.TabPages.Add(filesPage); split.Panel1.Controls.Add(leftTabs);
        var detailsPage = new TabPage("Details"); detailsPage.Controls.Add(_details);
        var objectsPage = new TabPage("Object Explorer");
        var objectHeader = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 }; objectHeader.Controls.Add(new Label { Text = "Type:", AutoSize = true, Padding = new Padding(8, 7, 2, 0) }); objectHeader.Controls.Add(_objectFilter);
        objectsPage.Controls.Add(_objects); objectsPage.Controls.Add(objectHeader); _rightTabs.TabPages.Add(detailsPage); _rightTabs.TabPages.Add(objectsPage);
        var blogPage = new TabPage("Blog");
        var blogActions = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) }; var saveBlog = new Button { Text = "Save Markdown post", Dock = DockStyle.Right, Width = 150 }; saveBlog.Click += async (_, _) => await RunUi(SaveBlogPost); blogActions.Controls.Add(saveBlog);
        blogPage.Controls.Add(_blogBody); blogPage.Controls.Add(_blogTitle); blogPage.Controls.Add(blogActions); _rightTabs.TabPages.Add(blogPage);
        split.Panel2.Controls.Add(_rightTabs);
        var commit = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
        var commitButton = new Button { Text = "Commit", Dock = DockStyle.Right, Width = 110 }; commitButton.Click += async (_, _) => await Commit();
        commit.Controls.Add(_message); commit.Controls.Add(commitButton);
        var root = new Panel { Dock = DockStyle.Fill }; root.Controls.Add(split); root.Controls.Add(commit); root.Controls.Add(header); root.Controls.Add(_tools); return root;
    }
    void BuildToolbar()
    {
        AddOpenMenu();
        AddButton("Init", InitRepository); AddButton("Refresh", RefreshRepository); _tools.Items.Add(new ToolStripSeparator());
        AddButton("Stage selected", StageSelected); AddButton("Unstage selected", UnstageSelected); AddButton("Stage all", async () => await Execute("add", "-A"));
        _tools.Items.Add(new ToolStripSeparator()); AddButton("New branch", NewBranch); AddButton("Log", ShowLog); AddButton("Diff", ShowSelectedDiff); AddButton("View file", ShowSelectedFile); AddRemoteMenu(); AddObjectMenu(); AddButton("Account", OAuthLogin);
    }
    void AddObjectMenu()
    {
        var menu = new ToolStripDropDownButton("Objects");
        AddMenuItem(menu, "Database compress", DatabaseCompress); AddMenuItem(menu, "Verify database", VerifyDatabase); AddMenuItem(menu, "Blame", Blame); AddMenuItem(menu, "Merge", Merge); AddMenuItem(menu, "Pull", Pull); AddMenuItem(menu, "Push", Push);
        _tools.Items.Add(menu);
    }
    void AddRemoteMenu()
    {
        var menu = new ToolStripDropDownButton("Remote");
        AddMenuItem(menu, "Connect GitHub / remote", ConfigureRemote); AddMenuItem(menu, "Fetch", Fetch); AddMenuItem(menu, "Pull", Pull); AddMenuItem(menu, "Push", Push);
        _tools.Items.Add(menu);
    }
    void AddMenuItem(ToolStripDropDownButton menu, string text, Func<Task> action) { var item = new ToolStripMenuItem(text); item.Click += async (_, _) => await RunUi(action); menu.DropDownItems.Add(item); }
    void AddButton(string text, Func<Task> action) { var b = new ToolStripButton(text); b.Click += async (_, _) => await RunUi(action); _tools.Items.Add(b); }
    void LoadBranding()
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "assets");
        var logoPath = Path.Combine(assetDirectory, "novagit.png"); var iconPath = Path.Combine(assetDirectory, "novagit.ico");
        if (File.Exists(logoPath)) _logo.Image = Image.FromFile(logoPath);
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);
    }
    void AddOpenMenu()
    {
        var menu = new ToolStripDropDownButton("Open");
        AddMenuItem(menu, "Select local folder", SelectLocalFolder); AddMenuItem(menu, "Clone repository", CloneRepository);
        _tools.Items.Insert(0, menu);
    }
    async Task RunUi(Func<Task> action) { try { UseWaitCursor = true; await action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "NovaGit", MessageBoxButtons.OK, MessageBoxIcon.Error); } finally { UseWaitCursor = false; } }
    async Task SelectRepository(string folder)
    {
        var result = await _git.Run(folder, "rev-parse", "--show-toplevel");
        if (result.ExitCode != 0) { _root = null; _repository.Text = "Not a Git repository — use Init or Open"; _files.Items.Clear(); _details.Clear(); return; }
        _root = result.StandardOutput.Trim(); _repository.Text = _root; await RefreshRepository();
    }
    async Task InitRepository()
    {
        using var picker = new FolderBrowserDialog { Description = "Select a folder to initialize as a Git repository" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        await _git.Require(picker.SelectedPath, "init"); await SelectRepository(picker.SelectedPath);
    }
    Task SelectLocalFolder()
    {
        using var picker = new FolderBrowserDialog { Description = "Select a local Git repository folder" };
        return picker.ShowDialog(this) == DialogResult.OK ? SelectRepository(picker.SelectedPath) : Task.CompletedTask;
    }
    async Task CloneRepository()
    {
        var remote = Prompt.Show("Repository URL (HTTPS or SSH):", "Clone repository"); if (string.IsNullOrWhiteSpace(remote)) return;
        var target = Prompt.Show("Destination folder (a new folder will be created):", "Clone repository"); if (string.IsNullOrWhiteSpace(target)) return;
        var fullTarget = Path.GetFullPath(target.Trim(' ', '\"')); var parent = Path.GetDirectoryName(fullTarget);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) throw new DirectoryNotFoundException("The destination folder's parent does not exist.");
        await _git.Require(parent, "clone", remote, fullTarget); await SelectRepository(fullTarget);
    }
    async Task RefreshRepository()
    {
        if (_root is null) return;
        var status = await _git.Require(_root, "status", "--porcelain=v1", "-uall");
        _files.BeginUpdate(); _files.Items.Clear();
        foreach (var line in status.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var item = new ListViewItem(line.Length >= 2 ? line[..2] : "??"); item.SubItems.Add(line.Length > 3 ? line[3..] : line); item.Tag = line.Length > 3 ? line[3..] : line; _files.Items.Add(item);
        }
        _files.EndUpdate();
        var tracked = (await _git.Require(_root, "ls-files", "--cached")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        _trackedFiles.BeginUpdate(); _trackedFiles.Items.Clear();
        foreach (var path in EnumerateWorkingFiles())
        {
            var item = new ListViewItem(path) { Tag = new WorktreeFile(path, tracked.Contains(path)) }; _trackedFiles.Items.Add(item);
        }
        _trackedFiles.EndUpdate();
        var branch = await _git.Require(_root, "branch", "--format=%(refname:short)"); var current = await _git.Require(_root, "branch", "--show-current");
        _updatingBranches = true; _branches.Items.Clear(); _branches.Items.AddRange(branch.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)); _branches.SelectedItem = current.StandardOutput.Trim(); _updatingBranches = false;
        _details.Text = (await _git.Require(_root, "log", "--oneline", "--decorate", "-n", "40")).StandardOutput;
        // Object enumeration can be very expensive for large repositories. Load it only when the user opens Object Explorer.
        _objectsLoaded = false; _objects.Items.Clear();
    }
    async Task StageSelected() { await ForSelected("add", "--"); }
    async Task UnstageSelected() { await ForSelected("restore", "--staged", "--"); }
    async Task ForSelected(params string[] start)
    {
        EnsureRepository(); var selected = _files.SelectedItems.Cast<ListViewItem>().Select(x => (string)x.Tag!).ToArray(); if (selected.Length == 0) return;
        var args = start.Concat(selected).ToArray(); await _git.Require(_root!, args); await RefreshRepository();
    }
    async Task Commit()
    {
        EnsureRepository(); if (string.IsNullOrWhiteSpace(_message.Text)) throw new ArgumentException("Enter a commit message.");
        await _git.Require(_root!, "commit", "-m", _message.Text); _message.Clear(); await RefreshRepository();
    }
    async Task NewBranch()
    {
        EnsureRepository(); var name = Prompt.Show("New branch name:", "Create branch"); if (string.IsNullOrWhiteSpace(name)) return;
        if (name.Any(char.IsWhiteSpace) || name.Contains("..") || name.StartsWith('-')) throw new ArgumentException("Invalid branch name.");
        await _git.Require(_root!, "switch", "-c", name); await RefreshRepository();
    }
    async Task Checkout(string branch) { EnsureRepository(); await _git.Require(_root!, "switch", branch); await RefreshRepository(); }
    async Task ShowLog() { EnsureRepository(); _details.Text = (await _git.Require(_root!, "log", "--graph", "--decorate", "--oneline", "-n", "100")).StandardOutput; }
    async Task ShowObjects() { EnsureRepository(); _rightTabs.SelectedIndex = 1; if (!_objectsLoaded) await LoadObjects(); }
    async Task DatabaseCompress() { EnsureRepository(); _details.Text = (await _git.Require(_root!, "gc")).StandardOutput; await RefreshRepository(); }
    async Task VerifyDatabase() { EnsureRepository(); _details.Text = (await _git.Require(_root!, "fsck", "--full")).StandardOutput; if (string.IsNullOrWhiteSpace(_details.Text)) _details.Text = "Repository database verified: no errors reported."; }
    async Task Blame()
    {
        EnsureRepository(); if (!TrySelectedPath(out var path, out _)) throw new ArgumentException("Select a file in Changes or Files first.");
        _details.Text = (await _git.Require(_root!, "blame", "--", path)).StandardOutput;
    }
    async Task Merge()
    {
        EnsureRepository(); var branch = Prompt.Show("Branch name to merge into the current branch:", "Merge branch"); if (string.IsNullOrWhiteSpace(branch)) return;
        _details.Text = (await _git.Require(_root!, "merge", branch)).StandardOutput; await RefreshRepository();
    }
    async Task ConfigureRemote()
    {
        EnsureRepository(); var url = Prompt.Show("GitHub or Git remote URL (HTTPS or SSH):", "Connect remote"); if (string.IsNullOrWhiteSpace(url)) return;
        if (url.Any(char.IsWhiteSpace) || url.Any(char.IsControl)) throw new ArgumentException("The remote URL cannot contain spaces or control characters.");
        var exists = (await _git.Require(_root!, "remote")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Contains("origin");
        if (exists) await _git.Require(_root!, "remote", "set-url", "origin", url); else await _git.Require(_root!, "remote", "add", "origin", url);
        SetDetails($"Connected remote 'origin':\n{url}\n\nUse Remote > Fetch, Pull, or Push.");
    }
    async Task<bool> EnsureOrigin()
    {
        EnsureRepository();
        var remotes = (await _git.Require(_root!, "remote")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (remotes.Contains("origin")) return true;
        await ConfigureRemote();
        return (await _git.Require(_root!, "remote")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Contains("origin");
    }
    async Task Fetch() { if (!await EnsureOrigin()) return; SetDetails((await _git.Require(_root!, "fetch", "origin")).StandardOutput); await RefreshRepository(); }
    async Task Pull()
    {
        if (!await EnsureOrigin()) return;
        var branch = (await _git.Require(_root!, "branch", "--show-current")).StandardOutput.Trim();
        SetDetails((await _git.Require(_root!, "pull", "origin", branch)).StandardOutput); await RefreshRepository();
    }
    async Task Push()
    {
        if (!await EnsureOrigin()) return;
        var branch = (await _git.Require(_root!, "branch", "--show-current")).StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(branch)) throw new InvalidOperationException("Cannot push while HEAD is detached. Switch to a branch first.");
        SetDetails((await _git.Require(_root!, "push", "--set-upstream", "origin", branch)).StandardOutput);
    }
    Task SaveBlogPost()
    {
        EnsureRepository();
        if (string.IsNullOrWhiteSpace(_blogTitle.Text) || string.IsNullOrWhiteSpace(_blogBody.Text)) throw new ArgumentException("Add a blog title and body first.");
        var slug = string.Concat(_blogTitle.Text.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-'); if (string.IsNullOrEmpty(slug)) slug = "post";
        var directory = Path.Combine(_root!, "blog"); Directory.CreateDirectory(directory); var path = Path.Combine(directory, $"{DateTime.UtcNow:yyyy-MM-dd}-{slug}.md");
        File.WriteAllText(path, $"---\ntitle: {_blogTitle.Text.Trim()}\ndate: {DateTimeOffset.Now:O}\n---\n\n{_blogBody.Text}"); _details.Text = $"Saved blog draft:\n{path}\n\nStage and commit it when ready."; _rightTabs.SelectedIndex = 0; return Task.CompletedTask;
    }
    async Task OAuthLogin()
    {
        using var dialog = new GitHubSignInDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var clientId = dialog.ClientId;
        var account = await GitHubOAuth.LoginAsync(clientId, this);
        MessageBox.Show($"GitHub verified the account '{account}'. You are signed in for this session. Tokens are not written to disk.", "NovaGit", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    async Task LoadObjects()
    {
        if (_root is null) return;
        var result = await _git.Require(_root, "cat-file", "--batch-all-objects", "--batch-check=%(objectname) %(objecttype) %(objectsize) %(deltabase)");
        var filter = _objectFilter.SelectedItem?.ToString() ?? "All";
        _objects.BeginUpdate(); _objects.Items.Clear();
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (parts.Length < 3 || (filter != "All" && parts[1] != filter)) continue;
            var deltaBase = parts.Length > 3 && parts[3].Trim('0').Length > 0 ? parts[3] : "-";
            var item = new ListViewItem(parts[0]); item.SubItems.Add(parts[1]); item.SubItems.Add(parts[2]); item.SubItems.Add(deltaBase); item.Tag = new GitObject(parts[0], parts[1], parts[2], deltaBase); _objects.Items.Add(item);
        }
        _objects.EndUpdate();
        _objectsLoaded = true;
    }
    async Task ShowSelectedObject()
    {
        if (_root is null || _objects.SelectedItems.Count != 1) return;
        var item = (GitObject)_objects.SelectedItems[0].Tag!;
        _rightTabs.SelectedIndex = 0;
        if (item.Type == "blob")
        {
            if (!long.TryParse(item.Size, out var size) || size > 2 * 1024 * 1024) { _details.Text = $"blob {item.Id}\nsize: {item.Size} bytes\ndelta base: {item.DeltaBase}\n\nThis blob is too large to preview."; return; }
            var content = await _git.Run(_root, "cat-file", "-p", item.Id);
            SetDetails(content.StandardOutput.Contains('\0') ? $"blob {item.Id} is binary and cannot be rendered as source." : $"// blob {item.Id}\r\n\r\n{content.StandardOutput}");
            return;
        }
        _details.Text = (await _git.Require(_root, "cat-file", "-p", item.Id)).StandardOutput;
    }
    async Task ShowSelectedDiff()
    {
        if (_root is null || !TrySelectedPath(out var path, out var isUntracked)) return;
        var diff = await _git.Run(_root, "diff", "HEAD", "--", path);
        if (string.IsNullOrEmpty(diff.StandardOutput) && isUntracked) diff = await _git.Run(_root, "diff", "--no-index", "--", "/dev/null", SafeWorktreePath(path));
        SetDetails(string.IsNullOrEmpty(diff.StandardOutput) ? "No text diff is available for this file. Select View file to see its current source." : diff.StandardOutput);
    }
    Task ShowSelectedFile()
    {
        if (_root is null || !TrySelectedPath(out var path, out _)) return Task.CompletedTask;
        var fullPath = SafeWorktreePath(path);
        if (!File.Exists(fullPath)) { _details.Text = "This file does not exist in the working folder."; return Task.CompletedTask; }
        var info = new FileInfo(fullPath);
        if (info.Length > 2 * 1024 * 1024) { _details.Text = $"{path} is {info.Length:N0} bytes. Files above 2 MB are not previewed."; return Task.CompletedTask; }
        var bytes = File.ReadAllBytes(fullPath);
        if (bytes.Contains((byte)0)) { _details.Text = $"{path} is binary ({bytes.Length:N0} bytes), so it cannot be shown as source code."; return Task.CompletedTask; }
        SetDetails($"// {path}\r\n\r\n" + System.Text.Encoding.UTF8.GetString(bytes)); _rightTabs.SelectedIndex = 0; return Task.CompletedTask;
    }
    async Task Execute(params string[] args) { EnsureRepository(); await _git.Require(_root!, args); await RefreshRepository(); }
    void EnsureRepository() { if (_root is null) throw new InvalidOperationException("Open or initialize a Git repository first."); }
    bool TrySelectedPath(out string path, out bool isUntracked)
    {
        if (_files.SelectedItems.Count == 1) { var item = _files.SelectedItems[0]; path = (string)item.Tag!; isUntracked = item.Text.Contains('?'); return true; }
        if (_trackedFiles.SelectedItems.Count == 1) { var item = (WorktreeFile)_trackedFiles.SelectedItems[0].Tag!; path = item.Path; isUntracked = !item.IsTracked; return true; }
        path = string.Empty; isUntracked = false; return false;
    }
    string SafeWorktreePath(string relativePath)
    {
        EnsureRepository(); var root = Path.GetFullPath(_root!) + Path.DirectorySeparatorChar; var fullPath = Path.GetFullPath(Path.Combine(_root!, relativePath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid path outside the repository.");
        return fullPath;
    }
    void SetDetails(string text)
    {
        _details.SuspendLayout(); _details.Text = text;
        if (text.Length > 1_000_000) { _details.ResumeLayout(); return; }
        _details.SelectAll(); _details.SelectionColor = Color.Gainsboro;
        // Git diff colours.
        ApplyPattern(@"(?m)^\+.*$", Color.FromArgb(160, 220, 160)); ApplyPattern(@"(?m)^-.*$", Color.FromArgb(255, 150, 150));
        ApplyPattern(@"(?m)^(diff --git|index |@@|\+\+\+|--- ).*$", Color.FromArgb(120, 190, 255));
        // Source-code colours for C#, JSON, JavaScript, Python, and similar text files.
        ApplyPattern(@"(?m)//.*$|#.*$|/\*[\s\S]*?\*/", Color.FromArgb(110, 160, 110));
        ApplyPattern(@"[""'](?:\\.|[^""'])*[""']", Color.FromArgb(220, 180, 110));
        ApplyPattern(@"\b(class|public|private|protected|internal|static|void|string|int|bool|var|new|return|async|await|using|namespace|if|else|for|foreach|while|switch|case|break|continue|try|catch|throw|true|false|null|const|readonly|function|def|import|from|let|export)\b", Color.FromArgb(110, 190, 255));
        ApplyPattern(@"\b\d+(?:\.\d+)?\b", Color.FromArgb(205, 140, 230));
        _details.Select(0, 0); _details.ResumeLayout();
    }
    void ApplyPattern(string pattern, Color color)
    {
        foreach (Match match in Regex.Matches(_details.Text, pattern, RegexOptions.Multiline)) { _details.Select(match.Index, match.Length); _details.SelectionColor = color; }
    }
    IEnumerable<string> EnumerateWorkingFiles()
    {
        EnsureRepository();
        return Directory.EnumerateFiles(_root!, "*", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(Path.Combine(_root!, ".git") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Select(file => Path.GetRelativePath(_root!, file).Replace('\\', '/')).Order();
    }
}

sealed class GitClient
{
    public async Task<GitResult> Run(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git could not be started. Install Git for Windows and add it to PATH.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); return new GitResult(process.ExitCode, await output, await error);
    }
    public async Task<GitResult> Require(string directory, params string[] arguments)
    {
        var result = await Run(directory, arguments); if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? "Git operation failed." : result.StandardError.Trim()); return result;
    }
}
record GitResult(int ExitCode, string StandardOutput, string StandardError);
record GitObject(string Id, string Type, string Size, string DeltaBase);
record WorktreeFile(string Path, bool IsTracked);

static class Prompt
{
    public static string? Show(string label, string title)
    {
        using var form = new Form { Text = title, ClientSize = new Size(380, 115), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
        var text = new TextBox { Left = 12, Top = 40, Width = 355 }; var ok = new Button { Text = "Create", DialogResult = DialogResult.OK, Left = 208, Top = 75, Width = 75 };
        form.Controls.AddRange([new Label { Text = label, Left = 12, Top = 15, AutoSize = true }, text, ok, new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 292, Top = 75, Width = 75 }]); form.AcceptButton = ok;
        return form.ShowDialog() == DialogResult.OK ? text.Text.Trim() : null;
    }
}
