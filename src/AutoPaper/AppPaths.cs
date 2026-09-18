namespace AutoPaper;

/// <summary>
/// 所有文件位置的唯一来源。
/// </summary>
public static class AppPaths
{
    public const string FolderName = "AutoPaper";
    public const string ConfigFileName = "config.yaml";
    public const string LogFileName = "autopaper.log";
    public const string WallpapersFolderName = "wallpapers";

    /// <summary>配置目录：所有平台统一是 &lt;用户目录&gt;/.config/AutoPaper。</summary>
    public static string ConfigDirectory { get; } = ResolveConfigDirectory(
        OperatingSystem.IsWindows(),
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
        AppContext.BaseDirectory);

    public static string DefaultConfigPath => Path.Combine(ConfigDirectory, ConfigFileName);
    public static string LogFilePath => Path.Combine(ConfigDirectory, LogFileName);
    public static string WallpapersDirectory => Path.Combine(ConfigDirectory, WallpapersFolderName);

    /// <summary>写文件之前先调用它。</summary>
    public static void EnsureConfigDirectory() => Directory.CreateDirectory(ConfigDirectory);

    /// <summary>
    /// 算出配置目录：<b>所有平台统一</b>是 &lt;用户目录&gt;/.config/AutoPaper。
    /// Windows 上也就是 C:\Users\&lt;用户名&gt;\.config\AutoPaper，刻意不用 %APPDATA%。
    ///
    /// 这样三个平台的路径完全一致，文档、配置文件、抄来抄去的命令都不用分平台写。
    ///
    /// 参数全部传进来而不是直接读环境，是为了能单测——真去改进程级环境变量会干扰并行跑的测试。
    /// </summary>
    internal static string ResolveConfigDirectory(
        bool isWindows,
        string? xdgConfigHome,
        string? home,
        string fallback)
    {
        // XDG_CONFIG_HOME 是 Unix 的东西，Windows 上不认
        if (!isWindows)
        {
            // XDG 规范要求必须是绝对路径，相对路径一律忽略
            var xdg = NonEmpty(xdgConfigHome);
            if (xdg is not null && Path.IsPathRooted(xdg))
                return Path.Combine(xdg, FolderName);
        }

        // 连用户目录都拿不到（比如 HOME 没设且 passwd 里也查不到）就退回 exe 所在目录，
        // 至少还能用。实际几乎不会走到这里。
        var root = NonEmpty(home) ?? fallback;
        return Path.Combine(root, ".config", FolderName);
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
