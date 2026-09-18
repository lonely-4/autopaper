using AutoPaper;

namespace AutoPaper.Tests;

/// <summary>路径规则：配置放用户配置目录，且目录要能自动创建。</summary>
public class AppPathsTests
{
    [Fact]
    public void 配置目录以AutoPaper结尾()
        => Assert.EndsWith("AutoPaper", AppPaths.ConfigDirectory);

    [Fact]
    public void 配置目录是绝对路径()
        => Assert.True(Path.IsPathRooted(AppPaths.ConfigDirectory));

    [Fact]
    public void 默认配置文件名是config点yaml()
        => Assert.Equal(Path.Combine(AppPaths.ConfigDirectory, "config.yaml"), AppPaths.DefaultConfigPath);

    [Fact]
    public void 日志和壁纸目录都在配置目录下()
    {
        Assert.Equal(AppPaths.ConfigDirectory, Path.GetDirectoryName(AppPaths.LogFilePath));
        Assert.Equal(AppPaths.ConfigDirectory, Path.GetDirectoryName(AppPaths.WallpapersDirectory));
    }

    [Fact]
    public void 显式指定的路径原样使用()
        => Assert.Equal(
            Path.GetFullPath("/tmp/somewhere/config.yaml"),
            ConfigLoader.ResolvePath("/tmp/somewhere/config.yaml"));

    [Fact]
    public void 没有显式路径且当前目录没有配置时回退到用户配置目录()
    {
        // 测试进程的工作目录里不该有 config.yaml。
        // "当前目录优先"那条分支会改动进程级状态，不适合放进并行跑的测试里，用 e2e 覆盖。
        Assert.False(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "config.yaml")));
        Assert.Equal(AppPaths.DefaultConfigPath, ConfigLoader.ResolvePath(null));
    }
}
