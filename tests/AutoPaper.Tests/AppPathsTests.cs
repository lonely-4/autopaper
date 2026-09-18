using AutoPaper;

namespace AutoPaper.Tests;

/// <summary>
/// 配置目录规则：<b>所有平台</b>都是 &lt;用户目录&gt;/.config/AutoPaper。
///
/// 这里有两个容易改坏的点，专门盯着：
///   1. Windows 上不能用 %APPDATA%，用户要的是 &lt;用户目录&gt;\.config
///   2. macOS 上不能靠 SpecialFolder.ApplicationData，.NET 8 起它返回
///      ~/Library/Application Support，那样路径就跟需求对不上了
/// </summary>
public class AppPathsTests
{
    // 用 Path.Combine 拼期望值，这样在哪个平台上跑都能对上分隔符
    private static string P(params string[] parts) => Path.Combine(parts);

    [Fact]
    public void Windows上配置目录是用户目录下的点config()
    {
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: true,
            xdgConfigHome: null,
            home: P("C:", "Users", "me"),
            fallback: P("C:", "exe"));

        Assert.Equal(P("C:", "Users", "me", ".config", "AutoPaper"), result);
    }

    [Fact]
    public void Windows上不用APPDATA()
    {
        // 防回归：只要有人把实现改回 %APPDATA%，这条就会红
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: true,
            xdgConfigHome: null,
            home: P("C:", "Users", "me"),
            fallback: P("C:", "exe"));

        Assert.DoesNotContain("AppData", result);
        Assert.DoesNotContain("Roaming", result);
        Assert.Contains(".config", result);
    }

    [Fact]
    public void Windows上忽略XDG环境变量()
    {
        // XDG_CONFIG_HOME 是 Unix 的东西，Windows 上不该受它影响
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: true,
            xdgConfigHome: P("C:", "somewhere", "else"),
            home: P("C:", "Users", "me"),
            fallback: P("C:", "exe"));

        Assert.Equal(P("C:", "Users", "me", ".config", "AutoPaper"), result);
    }

    [Fact]
    public void 三个平台的路径规则一致()
    {
        // 同一个"用户目录"概念，两个分支下算出来的相对结构必须相同
        var onWindows = AppPaths.ResolveConfigDirectory(true, null, P("home", "me"), "/fallback");
        var onUnix = AppPaths.ResolveConfigDirectory(false, null, P("home", "me"), "/fallback");

        Assert.Equal(onWindows, onUnix);
        Assert.Equal(P("home", "me", ".config", "AutoPaper"), onWindows);
    }

    [Fact]
    public void Unix上默认就是点config()
    {
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: false,
            xdgConfigHome: null,
            home: P("/home", "me"),
            fallback: "/fallback");

        Assert.Equal(P("/home", "me", ".config", "AutoPaper"), result);
    }

    [Fact]
    public void Unix上设了XDG_CONFIG_HOME就跟它走()
    {
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: false,
            xdgConfigHome: P("/home", "me", "my-config"),
            home: P("/home", "me"),
            fallback: "/fallback");

        Assert.Equal(P("/home", "me", "my-config", "AutoPaper"), result);
    }

    [Fact]
    public void Unix上XDG_CONFIG_HOME是相对路径时忽略它()
    {
        // XDG 规范要求必须是绝对路径
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: false,
            xdgConfigHome: "relative/path",
            home: P("/home", "me"),
            fallback: "/fallback");

        Assert.Equal(P("/home", "me", ".config", "AutoPaper"), result);
    }

    [Fact]
    public void Unix上XDG_CONFIG_HOME是空白时忽略它()
    {
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: false,
            xdgConfigHome: "   ",
            home: P("/home", "me"),
            fallback: "/fallback");

        Assert.Equal(P("/home", "me", ".config", "AutoPaper"), result);
    }

    [Fact]
    public void macOS走的也是点config而不是Library()
    {
        // 防回归的重点：改回 SpecialFolder.ApplicationData 的话，
        // macOS 会变成 ~/Library/Application Support/AutoPaper，跟需求对不上。
        var result = AppPaths.ResolveConfigDirectory(
            isWindows: false,
            xdgConfigHome: null,
            home: P("/Users", "me"),
            fallback: "/fallback");

        Assert.Equal(P("/Users", "me", ".config", "AutoPaper"), result);
        Assert.DoesNotContain("Application Support", result);
        Assert.DoesNotContain("Library", result);
    }

    [Fact]
    public void 连用户目录都拿不到时退回exe目录()
    {
        var onUnix = AppPaths.ResolveConfigDirectory(false, null, null, "/fallback");
        var onWindows = AppPaths.ResolveConfigDirectory(true, null, null, P("C:", "exe"));

        Assert.Equal(P("/fallback", ".config", "AutoPaper"), onUnix);
        Assert.Equal(P("C:", "exe", ".config", "AutoPaper"), onWindows);
    }

    [Fact]
    public void 当前环境里的配置目录符合本平台规则()
    {
        // 前面几条测的是纯函数，这条测真正跑起来用的是哪个目录
        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify);

        var expected = !OperatingSystem.IsWindows()
                       && Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                       && Path.IsPathRooted(xdg)
            ? Path.Combine(xdg, "AutoPaper")
            : Path.Combine(home, ".config", "AutoPaper");

        Assert.Equal(expected, AppPaths.ConfigDirectory);
    }

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
