namespace AutoPaper;

/// <summary>
/// 所有文件位置的唯一来源。
/// ApplicationData 在 Windows 上是 %APPDATA%，在 Linux/macOS 上是 ~/.config（遵循 XDG_CONFIG_HOME），
/// 所以两边都落在各自平台的正统位置，不需要写平台判断。
/// </summary>
public static class AppPaths
{
    public const string FolderName = "AutoPaper";
    public const string ConfigFileName = "config.yaml";
    public const string LogFileName = "autopaper.log";
    public const string WallpapersFolderName = "wallpapers";

    /// <summary>配置目录：Windows 是 %APPDATA%\AutoPaper，Linux/macOS 是 ~/.config/AutoPaper。</summary>
    public static string ConfigDirectory { get; } = ResolveConfigDirectory();

    public static string DefaultConfigPath => Path.Combine(ConfigDirectory, ConfigFileName);
    public static string LogFilePath => Path.Combine(ConfigDirectory, LogFileName);
    public static string WallpapersDirectory => Path.Combine(ConfigDirectory, WallpapersFolderName);

    /// <summary>写文件之前先调用它。</summary>
    public static void EnsureConfigDirectory() => Directory.CreateDirectory(ConfigDirectory);

    private static string ResolveConfigDirectory()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        // 极端情况下（比如 HOME 没设置）拿不到，退回 exe 所在目录，至少还能用
        if (string.IsNullOrEmpty(root))
            root = AppContext.BaseDirectory;

        return Path.Combine(root, FolderName);
    }
}
