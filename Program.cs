using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace NovaCrypto;

/// <summary>Security-focused cryptographic utilities. Hashes are not encryption.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("git", StringComparison.OrdinalIgnoreCase))
            return GitCli.RunAsync(args.Skip(1).ToArray()).GetAwaiter().GetResult();
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, eventArgs) => AppDiagnostics.Report(eventArgs.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => AppDiagnostics.Report(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown fatal error"));
            Application.Run(new GitMainForm());
            return 0;
        }
        try { return Cli.Run(args); }
        catch (ArgumentException ex) { Console.Error.WriteLine($"error: {ex.Message}"); return 2; }
        catch (CryptographicException) { Console.Error.WriteLine("error: invalid or corrupt cryptographic data."); return 2; }
        catch (IOException ex) { Console.Error.WriteLine($"error: unable to read or write the file: {ex.Message}"); return 1; }
        catch (UnauthorizedAccessException) { Console.Error.WriteLine("error: access to the requested file was denied."); return 1; }
    }
}

static class AppDiagnostics
{
    public static void Report(Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaGit", "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "crash.log"), $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* Logging must not cause another crash. */ }
        MessageBox.Show("NovaGit encountered an error. Details were saved to %LocalAppData%\\NovaGit\\logs\\crash.log.\n\n" + exception.Message, "NovaGit", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

static class Cli
{
    private const int MaxRandomBytes = 4096;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const int Pbkdf2Iterations = 600_000;

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h") { Help(); return 0; }
        switch (args[0].ToLowerInvariant())
        {
            case "hash-file": HashFile(args); break;
            case "hash-str": HashString(args); break;
            case "hex-encode": Console.WriteLine(CryptoUtils.BytesToHex(Utf8Arg(args, 1))); break;
            case "hex-decode": Console.WriteLine(Encoding.UTF8.GetString(ParseHex(Arg(args, 1)))); break;
            case "base64-encode": Console.WriteLine(Convert.ToBase64String(Utf8Arg(args, 1))); break;
            case "base64-decode": Console.WriteLine(Encoding.UTF8.GetString(ParseBase64(Arg(args, 1)))); break;
            case "checksum": Checksum(args); break;
            case "random": Random(args); break;
            case "guid": Console.WriteLine(Guid.NewGuid().ToString("D")); break;
            case "hmac": Hmac(args); break;
            case "verify": return Verify(args);
            case "password-hash": PasswordHash(args); break;
            case "password-verify": PasswordVerify(args); break;
            default: throw new ArgumentException($"Unknown command '{args[0]}'. Use --help.");
        }
        return 0;
    }

    static void HashFile(string[] args)
    {
        var path = Arg(args, 1); var algorithm = AlgorithmOption(args, 2);
        Console.WriteLine($"{algorithm}: {CryptoUtils.FileHash(path, algorithm)}");
    }
    static void HashString(string[] args)
    {
        var algorithm = AlgorithmOption(args, 2);
        Console.WriteLine($"{algorithm}: {CryptoUtils.Hash(Utf8Arg(args, 1), algorithm)}");
    }
    static void Checksum(string[] args)
    {
        var path = Arg(args, 1);
        Console.WriteLine($"SHA-256: {CryptoUtils.FileHash(path, HashKind.Sha256)}");
        Console.WriteLine($"SHA-512: {CryptoUtils.FileHash(path, HashKind.Sha512)}");
    }
    static void Random(string[] args)
    {
        if (!int.TryParse(Arg(args, 1), out var length) || length is < 1 or > MaxRandomBytes)
            throw new ArgumentException($"Random length must be between 1 and {MaxRandomBytes} bytes.");
        Console.WriteLine(CryptoUtils.BytesToHex(RandomNumberGenerator.GetBytes(length)));
    }
    static void Hmac(string[] args)
    {
        var text = Utf8Arg(args, 1); var algorithm = AlgorithmOption(args, 2);
        var keyAt = Array.IndexOf(args, "--key");
        if (keyAt < 0 || keyAt + 1 >= args.Length) throw new ArgumentException("Use hmac <text> --key <key> [--sha256|--sha512].");
        var key = Encoding.UTF8.GetBytes(args[keyAt + 1]);
        try { Console.WriteLine(CryptoUtils.BytesToHex(CryptoUtils.Hmac(key, text, algorithm))); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    static int Verify(string[] args)
    {
        var path = Arg(args, 1); var at = Array.IndexOf(args, "--hash");
        if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Use verify <file> --hash <sha256-hex>.");
        var expected = ParseHex(args[at + 1]);
        if (expected.Length != 32) throw new ArgumentException("Verification hash must be a 64-character SHA-256 hexadecimal value.");
        var actual = CryptoUtils.FileHashBytes(path, HashKind.Sha256);
        try
        {
            var matched = CryptographicOperations.FixedTimeEquals(actual, expected);
            Console.WriteLine(matched ? "Verified" : "Mismatch");
            return matched ? 0 : 1;
        }
        finally { CryptographicOperations.ZeroMemory(actual); CryptographicOperations.ZeroMemory(expected); }
    }
    static void PasswordHash(string[] args)
    {
        var password = Utf8Arg(args, 1); var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        try
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeyBytes);
            try { Console.WriteLine($"pbkdf2-sha256${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}"); }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        finally { CryptographicOperations.ZeroMemory(password); CryptographicOperations.ZeroMemory(salt); }
    }
    static void PasswordVerify(string[] args)
    {
        var password = Utf8Arg(args, 1); var encoded = Arg(args, 2).Split('$');
        if (encoded.Length != 4 || encoded[0] != "pbkdf2-sha256" || !int.TryParse(encoded[1], out var iterations) || iterations < 100_000)
            throw new ArgumentException("Invalid password-hash record.");
        var salt = ParseBase64(encoded[2]); var expected = ParseBase64(encoded[3]);
        try
        {
            var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            try { Console.WriteLine(CryptographicOperations.FixedTimeEquals(derived, expected) ? "Verified" : "Mismatch"); }
            finally { CryptographicOperations.ZeroMemory(derived); }
        }
        finally { CryptographicOperations.ZeroMemory(password); CryptographicOperations.ZeroMemory(salt); CryptographicOperations.ZeroMemory(expected); }
    }
    static HashKind AlgorithmOption(string[] args, int start) => args.Skip(start).Any(x => x == "--sha512") ? HashKind.Sha512 : HashKind.Sha256;
    static string Arg(string[] args, int index) => args.Length > index && !string.IsNullOrWhiteSpace(args[index]) ? args[index] : throw new ArgumentException("Missing required argument.");
    static byte[] Utf8Arg(string[] args, int index) => new UTF8Encoding(false, true).GetBytes(Arg(args, index));
    static byte[] ParseHex(string value) { if (value.Length % 2 != 0 || value.Any(x => !Uri.IsHexDigit(x))) throw new ArgumentException("Invalid hexadecimal value."); return Convert.FromHexString(value); }
    static byte[] ParseBase64(string value) { try { return Convert.FromBase64String(value); } catch (FormatException) { throw new ArgumentException("Invalid Base64 value."); } }
    static void Help() => Console.WriteLine("""
NovaCrypto commands (SHA-256 is the default):
  hash-file <path> [--sha256|--sha512]
  hash-str <text> [--sha256|--sha512]
  checksum <path> | verify <path> --hash <sha256-hex>
  hmac <text> --key <key> [--sha256|--sha512]
  password-hash <password> | password-verify <password> <record>
  hex-encode/decode <value> | base64-encode/decode <value>
  random <1-4096 bytes> | guid

Do not pass real passwords or keys in a shared terminal history; use a secret manager or a secure input method in production.
""");
}

public enum HashKind { Sha256, Sha512 }

public static class CryptoUtils
{
    public static string BytesToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    public static byte[] HexToBytes(string hex) => Convert.FromHexString(hex);
    public static string Hash(ReadOnlySpan<byte> data, HashKind algorithm) => BytesToHex(HashBytes(data, algorithm));
    public static string FileHash(string path, HashKind algorithm) => BytesToHex(FileHashBytes(path, algorithm));
    public static byte[] FileHashBytes(string path, HashKind algorithm)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131_072, FileOptions.SequentialScan);
        using var hash = CreateAlgorithm(algorithm);
        return hash.ComputeHash(stream);
    }
    public static byte[] Hmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, HashKind algorithm) => algorithm == HashKind.Sha512 ? HMACSHA512.HashData(key, data) : HMACSHA256.HashData(key, data);
    static byte[] HashBytes(ReadOnlySpan<byte> data, HashKind algorithm) => algorithm == HashKind.Sha512 ? SHA512.HashData(data) : SHA256.HashData(data);
    static HashAlgorithm CreateAlgorithm(HashKind algorithm) => algorithm == HashKind.Sha512 ? SHA512.Create() : SHA256.Create();
}
