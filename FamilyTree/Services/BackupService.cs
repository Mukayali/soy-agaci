using System.Diagnostics;
using System.Text;

namespace FamilyTree.Services;

public class BackupService : IBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IConfiguration configuration, ILogger<BackupService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BackupResult> CreateBackupAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new BackupResult { Success = false, ErrorMessage = "Bağlantı dizesi bulunamadı." };
        }

        var parts = ParseConnectionString(connectionString);

        if (!parts.TryGetValue("database", out var database) || string.IsNullOrWhiteSpace(database))
        {
            return new BackupResult { Success = false, ErrorMessage = "Bağlantı dizesinde veritabanı adı bulunamadı." };
        }

        var host = parts.GetValueOrDefault("server") ?? parts.GetValueOrDefault("host") ?? "localhost";
        var port = parts.GetValueOrDefault("port") ?? "3306";
        var user = parts.GetValueOrDefault("user") ?? parts.GetValueOrDefault("uid") ?? parts.GetValueOrDefault("userid");
        var password = parts.GetValueOrDefault("password") ?? parts.GetValueOrDefault("pwd") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(user))
        {
            return new BackupResult { Success = false, ErrorMessage = "Bağlantı dizesinde kullanıcı adı bulunamadı." };
        }

        var arguments = $"--host={host} --port={port} --user={user} --single-transaction --routines --triggers --databases {database}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "mysqldump",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Şifreyi komut satırı argümanı olarak değil, ortam değişkeni ile aktarıyoruz
        // (aksi halde `ps aux` çıktısında görünür olurdu).
        startInfo.EnvironmentVariables["MYSQL_PWD"] = password;

        using var process = new Process { StartInfo = startInfo };

        var stdoutBuffer = new MemoryStream();
        var stderrBuilder = new StringBuilder();

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogError(ex, "mysqldump çalıştırılamadı. Sunucuda mysqldump yüklü ve PATH'te olmalıdır.");
            return new BackupResult { Success = false, ErrorMessage = "mysqldump çalıştırılamadı. Sunucuda MySQL istemci araçlarının (mysqldump) kurulu olduğundan emin olun." };
        }

        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuffer);
        var stderrTask = process.StandardError.ReadToEndAsync();

        var completed = await Task.Run(() => process.WaitForExit(60_000));

        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // yoksay: süreç zaten sonlanmış olabilir
            }

            return new BackupResult { Success = false, ErrorMessage = "Yedekleme zaman aşımına uğradı (60 sn)." };
        }

        await stdoutTask;
        stderrBuilder.Append(await stderrTask);

        if (process.ExitCode != 0)
        {
            _logger.LogError("mysqldump başarısız oldu (ExitCode={ExitCode}): {StdErr}", process.ExitCode, stderrBuilder.ToString());
            return new BackupResult { Success = false, ErrorMessage = "Yedekleme başarısız oldu. Sunucu loglarını kontrol edin." };
        }

        return new BackupResult
        {
            Success = true,
            Data = stdoutBuffer.ToArray(),
            FileName = $"{database}-yedek-{DateTime.UtcNow:yyyyMMdd-HHmmss}.sql",
        };
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim().ToLowerInvariant();
            var value = segment[(separatorIndex + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }
}
