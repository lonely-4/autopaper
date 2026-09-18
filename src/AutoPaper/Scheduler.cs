using System.Diagnostics;

namespace AutoPaper;

/// <summary>用 Windows 任务计划程序注册两个任务：登录时跑一次 + 每天跑一次。</summary>
public static class Scheduler
{
    public const string LogonTaskName = "AutoPaper-Logon";
    public const string DailyTaskName = "AutoPaper-Daily";
    private const string DailyTime = "00:05";

    public static int Install(string configPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("install 只能在 Windows 上运行。");
            return 1;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"定位不到 autopaper.exe（当前进程路径：{exe}）。");
            Console.Error.WriteLine("请直接运行发布出来的 exe，不要用 dotnet run。");
            return 1;
        }

        // /TR 里的 exe 路径必须带引号，ArgumentList 会负责转义
        var payload = $"\"{exe}\" apply --quiet --config \"{configPath}\"";

        var logon = Run("schtasks", "/Create", "/TN", LogonTaskName, "/TR", payload, "/SC", "ONLOGON", "/F");
        if (logon.Code != 0) return ReportFailure($"{LogonTaskName} 创建失败", logon);

        var daily = Run("schtasks", "/Create", "/TN", DailyTaskName, "/TR", payload, "/SC", "DAILY", "/ST", DailyTime, "/F");
        if (daily.Code != 0) return ReportFailure($"{DailyTaskName} 创建失败", daily);

        Console.WriteLine("已注册计划任务：");
        Console.WriteLine($"  {LogonTaskName}   每次登录后应用当天壁纸");
        Console.WriteLine($"  {DailyTaskName}   每天 {DailyTime} 应用当天壁纸");
        Console.WriteLine($"  使用配置：{configPath}");
        Console.WriteLine();
        Console.WriteLine("第 3 步会自动重试（刚登录时桌面可能还没就绪）。");
        Console.WriteLine($"运行日志：{Log.FilePath}");
        return 0;
    }

    public static int Uninstall()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("uninstall 只能在 Windows 上运行。");
            return 1;
        }

        // 任务本来就不存在时 schtasks 会报错，这里当成正常情况处理，保证可以重复执行
        var logon = Run("schtasks", "/Delete", "/TN", LogonTaskName, "/F");
        var daily = Run("schtasks", "/Delete", "/TN", DailyTaskName, "/F");

        Console.WriteLine(logon.Code == 0
            ? $"已删除 {LogonTaskName}"
            : $"{LogonTaskName} 不存在，跳过");
        Console.WriteLine(daily.Code == 0
            ? $"已删除 {DailyTaskName}"
            : $"{DailyTaskName} 不存在，跳过");
        return 0;
    }

    private static int ReportFailure(string what, (int Code, string Out, string Err) result)
    {
        Console.Error.WriteLine($"{what}（退出码 {result.Code}）。");
        if (!string.IsNullOrWhiteSpace(result.Out)) Console.Error.WriteLine(result.Out.Trim());
        if (!string.IsNullOrWhiteSpace(result.Err)) Console.Error.WriteLine(result.Err.Trim());
        Console.Error.WriteLine("如果一直失败，可以手动执行同一条 schtasks 命令，或用「任务计划程序」界面添加。");
        return 1;
    }

    private static (int Code, string Out, string Err) Run(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null)
            return (1, "", $"启动不了 {file}。");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }
}
