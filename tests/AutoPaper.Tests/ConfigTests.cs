using AutoPaper;

namespace AutoPaper.Tests;

/// <summary>config.yaml 的解析与校验。</summary>
public class ConfigTests
{
    [Fact]
    public void 空配置文件全部走默认值()
    {
        var config = TestHelpers.Load("");

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, config.Range);
        Assert.Equal(new DateOnly(1, 1, 1), config.RangeStartDay);
        Assert.Equal(WallpaperStyle.Fill, config.Style);
        Assert.Empty(config.SpecialDays);
    }

    [Fact]
    public void 只有注释的文件也走默认值()
    {
        var config = TestHelpers.Load("""
            # 什么都没配
            # 全靠默认
            """);

        Assert.Equal(7, config.Range.Count);
    }

    [Fact]
    public void 配置项留空时走默认值而不是报错()
    {
        // "key:" 后面只跟注释是很自然的写法（init 生成的模板就是这样），
        // YAML 解析出来是个空标量，应该等同于"没配这一项"。
        // 这条要是挂了，用户 init 完立刻 check 就会报错。
        var config = TestHelpers.Load("""
            range:
              # - 1
            rangeStartDay:
              # 2026-01-05
            style:
              # fill
            specialDays:
              # "12-25": christmas.jpg
            """);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, config.Range);
        Assert.Equal(new DateOnly(1, 1, 1), config.RangeStartDay);
        Assert.Equal(WallpaperStyle.Fill, config.Style);
        Assert.Empty(config.SpecialDays);
    }

    [Fact]
    public void 显式写null或空映射也走默认值()
    {
        var config = TestHelpers.Load("""
            range: null
            style: ~
            specialDays: {}
            """);

        Assert.Equal(7, config.Range.Count);
        Assert.Equal(WallpaperStyle.Fill, config.Style);
        Assert.Empty(config.SpecialDays);
    }

    [Fact]
    public void 找不到文件时报错并提示先init()
    {
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load("/definitely/not/here.yaml"));

        Assert.Contains("找不到配置文件", ex.Message);
        Assert.Contains("init", ex.Message);
    }

    [Fact]
    public void YAML语法错误时带出位置()
    {
        var ex = TestHelpers.LoadError("""
            range: [1, 2
            """);

        Assert.Contains("不是合法的 YAML", ex.Message);
        Assert.Contains("行", ex.Message);
    }

    [Fact]
    public void 顶层不是映射时报错()
    {
        var ex = TestHelpers.LoadError("""
            - 1
            - 2
            """);

        Assert.Contains("顶层", ex.Message);
    }

    [Fact]
    public void 配置项写错字会报错并列出可用的()
    {
        var ex = TestHelpers.LoadError("""
            rang: [1, 2, 3]
            """);

        Assert.Contains("rang", ex.Message);
        Assert.Contains("range", ex.Message);   // 提示里应列出正确拼写
    }

    [Fact]
    public void range不是列表时报错()
    {
        var ex = TestHelpers.LoadError("""
            range: 7
            """);

        Assert.Contains("range", ex.Message);
        Assert.Contains("列表", ex.Message);
    }

    [Fact]
    public void range为空时报错()
    {
        var ex = TestHelpers.LoadError("""
            range: []
            """);

        Assert.Contains("空列表", ex.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void range里不是正整数时报错(string bad)
    {
        var ex = TestHelpers.LoadError($"""
            range: [1, {bad}]
            """);

        Assert.Contains("正整数", ex.Message);
    }

    [Fact]
    public void rangeStartDay格式不对时报错()
    {
        var ex = TestHelpers.LoadError("""
            rangeStartDay: "2026/01/05"
            """);

        Assert.Contains("yyyy-MM-dd", ex.Message);
    }

    [Fact]
    public void rangeStartDay带引号和不带引号都认()
    {
        Assert.Equal(
            new DateOnly(1, 1, 1),
            TestHelpers.Load("""
                rangeStartDay: "0001-01-01"
                """).RangeStartDay);

        Assert.Equal(
            new DateOnly(1, 1, 1),
            TestHelpers.Load("""
                rangeStartDay: 0001-01-01
                """).RangeStartDay);
    }

    [Fact]
    public void style不合法时报错()
    {
        var ex = TestHelpers.LoadError("""
            style: 斜着放
            """);

        Assert.Contains("style", ex.Message);
    }

    [Theory]
    [InlineData("fill", WallpaperStyle.Fill)]
    [InlineData("FIT", WallpaperStyle.Fit)]
    [InlineData("stretch", WallpaperStyle.Stretch)]
    [InlineData("center", WallpaperStyle.Center)]
    [InlineData("tile", WallpaperStyle.Tile)]
    [InlineData("span", WallpaperStyle.Span)]
    public void style解析正确(string value, WallpaperStyle expected)
    {
        Assert.Equal(expected, TestHelpers.Load($"style: {value}").Style);
    }

    [Fact]
    public void specialDays不是映射时报错()
    {
        var ex = TestHelpers.LoadError("""
            specialDays:
              - 2026-01-01
            """);

        Assert.Contains("specialDays", ex.Message);
    }

    [Theory]
    [InlineData("明天")]
    [InlineData("13-01")]
    [InlineData("02-30")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    public void specialDays日期不合法时报错(string bad)
    {
        var ex = TestHelpers.LoadError($"""
            specialDays:
              "{bad}": a.jpg
            """);

        Assert.Contains("不是合法日期", ex.Message);
    }

    [Fact]
    public void specialDays支持某一年和每年两种写法()
    {
        var config = TestHelpers.Load("""
            specialDays:
              "2026-01-01": newyear.jpg
              "12-25": christmas.jpg
            """);

        Assert.Equal(2, config.SpecialDays.Count);

        var yearly = config.SpecialDays.Single(d => d.Year is null);
        Assert.Equal((12, 25), (yearly.Month, yearly.Day));
        Assert.Equal("12-25", yearly.Describe());

        var exact = config.SpecialDays.Single(d => d.Year is not null);
        Assert.Equal("2026-01-01", exact.Describe());
    }

    [Fact]
    public void specialDays里二月二十九合法()
    {
        var config = TestHelpers.Load("""
            specialDays:
              "02-29": leap.jpg
            """);

        Assert.Null(config.SpecialDays[0].Year);
        Assert.True(config.SpecialDays[0].Matches(TestHelpers.D("2024-02-29")));
        Assert.False(config.SpecialDays[0].Matches(TestHelpers.D("2025-02-28")));
    }

    [Fact]
    public void specialDays里同一个日期用不同写法写两次会被发现()
    {
        // "2026-01-01" 和 "2026-1-1" 是两个不同的 YAML key，但指的是同一天
        var ex = TestHelpers.LoadError("""
            specialDays:
              "2026-01-01": a.jpg
              "2026-1-1": b.jpg
            """);

        Assert.Contains("重复", ex.Message);
    }

    [Fact]
    public void specialDays里键完全重复时会被拒绝()
    {
        // 完全相同的 key 在 YAML 解析层就被拦下了
        var ex = TestHelpers.LoadError("""
            specialDays:
              "12-25": a.jpg
              "12-25": b.jpg
            """);

        Assert.Contains("YAML", ex.Message);
    }

    [Fact]
    public void specialDays没写图片时报错()
    {
        var ex = TestHelpers.LoadError("""
            specialDays:
              "12-25": ""
            """);

        Assert.Contains("没有指定图片", ex.Message);
    }

    [Fact]
    public void 块列表写法也能解析()
    {
        var config = TestHelpers.Load("""
            range:
              - 3
              - 1
              - 2
            """);

        Assert.Equal(new[] { 3, 1, 2 }, config.Range);
    }

    [Fact]
    public void 注释和行内写法可以混用()
    {
        var config = TestHelpers.Load("""
            range: [1, 2, 3]   # 三天一轮
            style: fit         # 适应屏幕
            """);

        Assert.Equal(3, config.Range.Count);
        Assert.Equal(WallpaperStyle.Fit, config.Style);
    }

    [Fact]
    public void specialDays的图片路径相对壁纸目录解析()
    {
        var (config, dir) = TestHelpers.LoadWithDir("""
            specialDays:
              "12-25": christmas.jpg
            """);

        Assert.Equal(Path.Combine(dir, "wallpapers", "christmas.jpg"), config.ToImagePath("christmas.jpg"));
    }

    [Fact]
    public void 配置目录就是配置文件所在目录()
    {
        var (config, dir) = TestHelpers.LoadWithDir("");

        Assert.Equal(dir, config.BaseDirectory);
        Assert.Equal(Path.Combine(dir, "wallpapers"), config.WallpapersDirectory);
    }
}
