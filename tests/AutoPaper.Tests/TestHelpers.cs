using AutoPaper;

namespace AutoPaper.Tests;

internal static class TestHelpers
{
    /// <summary>把 YAML 写进临时目录再加载。</summary>
    public static AutoPaperConfig Load(string yaml)
    {
        var (config, _) = LoadWithDir(yaml);
        return config;
    }

    /// <summary>同上，但把临时目录一并返回，便于建壁纸文件和测相对路径。</summary>
    public static (AutoPaperConfig Config, string Dir) LoadWithDir(string yaml)
    {
        var dir = Directory.CreateTempSubdirectory("autopaper-test-");
        var path = Path.Combine(dir.FullName, AppPaths.ConfigFileName);
        File.WriteAllText(path, yaml);
        return (ConfigLoader.Load(path), dir.FullName);
    }

    /// <summary>期望配置加载失败，返回异常以便检查提示内容。</summary>
    public static ConfigException LoadError(string yaml) =>
        Assert.Throws<ConfigException>(() => Load(yaml));

    public static DateOnly D(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd");

    /// <summary>建一个壁纸目录并放上指定文件名的空文件，然后扫描。</summary>
    public static WallpaperLibrary Library(AutoPaperConfig config, params string[] fileNames)
    {
        var directory = config.WallpapersDirectory;
        Directory.CreateDirectory(directory);

        foreach (var name in fileNames)
            File.WriteAllText(Path.Combine(directory, name), "");

        return WallpaperLibrary.Scan(directory);
    }

    /// <summary>建好目录和文件，返回配置和扫描结果。</summary>
    public static (AutoPaperConfig Config, WallpaperLibrary Library) Setup(string yaml, params string[] fileNames)
    {
        var config = Load(yaml);
        return (config, Library(config, fileNames));
    }
}
