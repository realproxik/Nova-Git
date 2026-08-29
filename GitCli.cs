using System.Diagnostics;

namespace NovaCrypto;

/// <summary>Command-line companion that forwards structured arguments to the installed Git executable.</summary>
static class GitCli // idk
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine("NovaGit CLI\nUsage: dotnet run -- git <git-command> [arguments]\n\nExamples:\n  dotnet run -- git status\n  dotnet run -- git add Program.cs\n  dotnet run -- git commit -m \"message\"\n  dotnet run -- git diff\n  dotnet run -- git pull\n  dotnet run -- git push\n  dotnet run -- git fsck --full\n  dotnet run -- git gc");
            return 0;
        }
        var start = new ProcessStartInfo("git") { WorkingDirectory = Directory.GetCurrentDirectory(), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git could not be started. Install Git for Windows and add it to PATH.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
        Console.Write(await stdout); Console.Error.Write(await stderr); return process.ExitCode;
    }
}
