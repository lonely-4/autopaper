namespace AutoPaper;

/// <summary>极简日志，主要给后台跑的 apply 用。写在配置目录里，那里一定可写。</summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;

    public static string FilePath => AppPaths.LogFilePath;

    public static void Write(string message)
    {
        try
        {
            AppPaths.EnsureConfigDirectory();
            Rotate();
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch
        {
            // 写日志失败绝不能影响主流程
        }
    }

    private static void Rotate()
    {
        var info = new FileInfo(FilePath);
        if (info.Exists && info.Length > MaxBytes)
            File.Move(FilePath, FilePath + ".old", overwrite: true);
    }
}
