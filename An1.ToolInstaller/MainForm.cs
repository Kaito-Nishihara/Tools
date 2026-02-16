using System.Diagnostics;
using System.Text;

namespace An1.ToolInstaller;

public partial class MainForm : Form
{
    private readonly TextBox _txtPackageId = new() { Width = 260 };
    private readonly TextBox _txtSource = new() { Width = 520 };
    private readonly TextBox _txtToolPath = new() { Width = 520 };
    private readonly TextBox _txtCommandName = new() { Width = 260 };
    private readonly TextBox _txtLog = new() { Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, ReadOnly = true, Dock = DockStyle.Fill };

    private readonly Button _btnPickSource = new() { Text = "参照..." };
    private readonly Button _btnPickToolPath = new() { Text = "参照..." };

    private readonly Button _btnCheckDotnet = new() { Text = "dotnet確認" };
    private readonly Button _btnList = new() { Text = "一覧" };
    private readonly Button _btnInstall = new() { Text = "インストール" };
    private readonly Button _btnUpdate = new() { Text = "更新" };
    private readonly Button _btnUninstall = new() { Text = "アンインストール" };
    private readonly Button _btnRun = new() { Text = "an1 実行テスト" };
    private readonly Button _btnClear = new() { Text = "ログクリア" };

    private readonly CancellationTokenSource _cts = new();

    public MainForm()
    {
        Text = "AN1 Tool Installer (An1.Cli)";
        Width = 980;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        // 初期値
        _txtPackageId.Text = "An1.Cli";  // NuGet PackageId
        _txtCommandName.Text = "an1";    // ToolCommandName
        _txtSource.Text = @"C:\github\Tools\nupkg"; // 例: ローカル nupkg
        _txtToolPath.Text = Path.Combine(Environment.CurrentDirectory, ".tools"); // repo内推奨

        // レイアウト
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(10),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        AddRow(top, "PackageId", _txtPackageId, new Label { Text = "", AutoSize = true });
        AddRow(top, "CommandName", _txtCommandName, new Label { Text = "", AutoSize = true });

        _btnPickSource.Click += (_, _) => PickFolder(_txtSource);
        AddRow(top, "Source (nupkg)", _txtSource, _btnPickSource);

        _btnPickToolPath.Click += (_, _) => PickFolder(_txtToolPath);
        AddRow(top, "ToolPath (.tools)", _txtToolPath, _btnPickToolPath);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 0, 10, 10),
            WrapContents = true
        };

        buttons.Controls.AddRange(new Control[]
        {
            _btnCheckDotnet, _btnList, _btnInstall, _btnUpdate, _btnUninstall, _btnRun, _btnClear
        });

        _btnClear.Click += (_, _) => _txtLog.Clear();

        _btnCheckDotnet.Click += async (_, _) => await CheckDotnetAsync();
        _btnList.Click += async (_, _) => await ToolListAsync();
        _btnInstall.Click += async (_, _) => await ToolInstallAsync();
        _btnUpdate.Click += async (_, _) => await ToolUpdateAsync();
        _btnUninstall.Click += async (_, _) => await ToolUninstallAsync();
        _btnRun.Click += async (_, _) => await RunCommandAsync();

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 520,
            Panel1MinSize = 200,
            Panel2MinSize = 120
        };

        split.Panel1.Controls.Add(_txtLog);

        var hint = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            BackColor = SystemColors.Control,
            BorderStyle = BorderStyle.None,
            Text =
@"使い方:
1) Source に nupkg フォルダを指定（例: C:\github\Tools\nupkg）
2) ToolPath にインストール先（repo内推奨: <repo>\.tools）
3) 「インストール」→ 成功したら ToolPath\an1.exe が作成されます

補足:
- 失敗時はログの [ERR] を確認してください
- dotnet SDK が必要です"
        };
        split.Panel2.Controls.Add(hint);

        Controls.Add(split);
        Controls.Add(buttons);
        Controls.Add(top);

        FormClosing += (_, _) => _cts.Cancel();
    }

    private static void AddRow(TableLayoutPanel panel, string label, Control main, Control right)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        panel.Controls.Add(main, 1, row);
        panel.Controls.Add(right, 2, row);

        main.Dock = DockStyle.Top;
        right.Dock = DockStyle.Top;
    }

    private void PickFolder(TextBox target)
    {
        using var dlg = new FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(target.Text) ? target.Text : Environment.CurrentDirectory,
            ShowNewFolderButton = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            target.Text = dlg.SelectedPath;
    }

    private string PackageId => _txtPackageId.Text.Trim();
    private string CommandName => _txtCommandName.Text.Trim();
    private string SourcePath => Path.GetFullPath(_txtSource.Text.Trim());
    private string ToolPath => Path.GetFullPath(_txtToolPath.Text.Trim());

    private void Log(string line)
    {
        if (InvokeRequired) { Invoke(new Action<string>(Log), line); return; }
        _txtLog.AppendText(line + Environment.NewLine);
    }

    private void Err(string line)
    {
        if (InvokeRequired) { Invoke(new Action<string>(Err), line); return; }
        _txtLog.AppendText("[ERR] " + line + Environment.NewLine);
    }

    private void SetBusy(bool busy)
    {
        if (InvokeRequired) { Invoke(new Action<bool>(SetBusy), busy); return; }
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        foreach (var b in new[] { _btnCheckDotnet, _btnList, _btnInstall, _btnUpdate, _btnUninstall, _btnRun })
            b.Enabled = !busy;
    }

    private async Task CheckDotnetAsync()
    {
        SetBusy(true);
        Log("== dotnet --info ==");
        var code = await RunDotNetAsync("--info");
        Log($"ExitCode: {code}");
        SetBusy(false);
    }

    private async Task ToolListAsync()
    {
        SetBusy(true);
        EnsureDirs();
        Log("== dotnet tool list ==");
        var code = await RunDotNetAsync($"tool list --tool-path \"{ToolPath}\"");
        Log($"ExitCode: {code}");
        SetBusy(false);
    }

    private async Task ToolInstallAsync()
    {
        SetBusy(true);
        EnsureDirs();

        if (!Directory.Exists(SourcePath))
        {
            Err($"Source folder not found: {SourcePath}");
            SetBusy(false);
            return;
        }

        Log("== dotnet tool install ==");
        Log($"PackageId: {PackageId}");
        Log($"Source   : {SourcePath}");
        Log($"ToolPath : {ToolPath}");

        // --ignore-failed-sources: 他のソースが死んでても引っ張られにくい
        var args =
            $"tool install {PackageId} " +
            $"--tool-path \"{ToolPath}\" " +
            $"--add-source \"{SourcePath}\" " +
            $"--ignore-failed-sources";

        var code = await RunDotNetAsync(args);
        Log($"ExitCode: {code}");

        // 成功したら exe の存在確認
        var exe = Path.Combine(ToolPath, $"{CommandName}.exe");
        if (code == 0 && File.Exists(exe))
            Log($"OK: {exe}");
        else
            Err($"Not found (maybe install failed): {exe}");

        SetBusy(false);
    }

    private async Task ToolUpdateAsync()
    {
        SetBusy(true);
        EnsureDirs();

        Log("== dotnet tool update ==");
        Log($"PackageId: {PackageId}");
        Log($"Source   : {SourcePath}");
        Log($"ToolPath : {ToolPath}");

        var args =
            $"tool update {PackageId} " +
            $"--tool-path \"{ToolPath}\" " +
            $"--add-source \"{SourcePath}\" " +
            $"--ignore-failed-sources";

        var code = await RunDotNetAsync(args);
        Log($"ExitCode: {code}");
        SetBusy(false);
    }

    private async Task ToolUninstallAsync()
    {
        SetBusy(true);
        EnsureDirs();

        Log("== dotnet tool uninstall ==");
        var args = $"tool uninstall {PackageId} --tool-path \"{ToolPath}\"";
        var code = await RunDotNetAsync(args);
        Log($"ExitCode: {code}");
        SetBusy(false);
    }

    private async Task RunCommandAsync()
    {
        SetBusy(true);
        EnsureDirs();

        var exe = Path.Combine(ToolPath, $"{CommandName}.exe");
        if (!File.Exists(exe))
        {
            Err($"Command not found. Install first: {exe}");
            SetBusy(false);
            return;
        }

        Log("== an1 command test ==");
        // 例：an1 dml generate --env Dev01
        var code = await RunProcessAsync(
            fileName: exe,
            arguments: "dml generate --env Dev01",
            onOut: Log,
            onErr: Err,
            ct: _cts.Token);

        Log($"ExitCode: {code}");
        SetBusy(false);
    }

    private void EnsureDirs()
    {
        Directory.CreateDirectory(ToolPath);
    }

    private Task<int> RunDotNetAsync(string arguments)
        => RunProcessAsync("dotnet", arguments, Log, Err, _cts.Token);

    private static async Task<int> RunProcessAsync(
        string fileName,
        string arguments,
        Action<string> onOut,
        Action<string> onErr,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var p = new Process { StartInfo = psi };

        p.OutputDataReceived += (_, e) => { if (e.Data != null) onOut(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) onErr(e.Data); };

        if (!p.Start())
            throw new InvalidOperationException($"Failed to start process: {fileName}");

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        await p.WaitForExitAsync(ct);
        return p.ExitCode;
    }
}
