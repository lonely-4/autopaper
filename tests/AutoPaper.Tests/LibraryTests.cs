using AutoPaper;

namespace AutoPaper.Tests;

/// <summary>
/// 文件名约定与优先级：
/// specialDays &gt; 文件名具体日期 &gt; 文件名每年日期 &gt; 周期槽位 &gt; default &gt; 不动。
/// </summary>
public class LibraryTests
{
    [Fact]
    public void 按槽位挑到对应的文件()
    {
        var (config, library) = TestHelpers.Setup("", "1.jpg", "3.jpg", "7.jpg");

        var monday = library.Resolve(config, TestHelpers.D("2026-01-05"));   // 周一 -> 1
        var wednesday = library.Resolve(config, TestHelpers.D("2026-01-07")); // 周三 -> 3

        Assert.EndsWith("1.jpg", monday.Image);
        Assert.EndsWith("3.jpg", wednesday.Image);
        Assert.Contains("周期槽位", monday.Source);
    }

    [Fact]
    public void 没有对应的槽位且没有default时不做任何修改()
    {
        var (config, library) = TestHelpers.Setup("", "1.jpg");

        // 周二是槽位 2，目录里没有 2
        var result = library.Resolve(config, TestHelpers.D("2026-01-06"));

        Assert.True(result.IsEmpty);
        Assert.Null(result.Image);
    }

    [Fact]
    public void 没有对应槽位时用default兜底()
    {
        var (config, library) = TestHelpers.Setup("", "1.jpg", "default.png");

        var result = library.Resolve(config, TestHelpers.D("2026-01-06"));

        Assert.EndsWith("default.png", result.Image);
        Assert.Contains("default", result.Source);
    }

    [Fact]
    public void 具体日期优先于周期槽位()
    {
        var (config, library) = TestHelpers.Setup("", "1.jpg", "2026-01-05.jpg");

        var result = library.Resolve(config, TestHelpers.D("2026-01-05"));

        Assert.EndsWith("2026-01-05.jpg", result.Image);
        Assert.Contains("文件名", result.Source);
    }

    [Fact]
    public void 每年日期优先于周期槽位()
    {
        var (config, library) = TestHelpers.Setup("", "1.jpg", "12-25.jpg");

        // 2026-12-25 是周五，槽位 5，目录里没有，所以本来会落空
        var result = library.Resolve(config, TestHelpers.D("2026-12-25"));

        Assert.EndsWith("12-25.jpg", result.Image);
    }

    [Fact]
    public void 具体日期优先于每年日期()
    {
        var (config, library) = TestHelpers.Setup("", "12-25.jpg", "2026-12-25.jpg");

        Assert.EndsWith("2026-12-25.jpg", library.Resolve(config, TestHelpers.D("2026-12-25")).Image);

        // 别的年份就轮到"每年"那张
        Assert.EndsWith("12-25.jpg", library.Resolve(config, TestHelpers.D("2027-12-25")).Image);
    }

    [Fact]
    public void 具体日期只在那一年的那天生效()
    {
        var (config, library) = TestHelpers.Setup("", "default.jpg", "2026-01-05.jpg");

        Assert.EndsWith("2026-01-05.jpg", library.Resolve(config, TestHelpers.D("2026-01-05")).Image);
        // 2027-01-05 也是周一，但不该再用那张
        Assert.EndsWith("default.jpg", library.Resolve(config, TestHelpers.D("2027-01-05")).Image);
    }

    [Fact]
    public void 配置里的specialDays优先级最高()
    {
        var (config, library) = TestHelpers.Setup("""
            specialDays:
              "2026-01-05": newyear.jpg
            """, "1.jpg", "2026-01-05.jpg");

        var result = library.Resolve(config, TestHelpers.D("2026-01-05"));

        Assert.EndsWith("newyear.jpg", result.Image);
        Assert.Contains("specialDays", result.Source);
    }

    [Fact]
    public void specialDays里的每年写法也能命中()
    {
        var (config, library) = TestHelpers.Setup("""
            specialDays:
              "12-25": christmas.jpg
            """, "default.jpg");

        Assert.EndsWith("christmas.jpg", library.Resolve(config, TestHelpers.D("2026-12-25")).Image);
        Assert.EndsWith("christmas.jpg", library.Resolve(config, TestHelpers.D("2031-12-25")).Image);
        Assert.EndsWith("default.jpg", library.Resolve(config, TestHelpers.D("2026-12-26")).Image);
    }

    [Fact]
    public void specialDays里具体年份优先于每年()
    {
        var (config, library) = TestHelpers.Setup("""
            specialDays:
              "12-25": every-year.jpg
              "2026-12-25": only-2026.jpg
            """, "default.jpg");

        Assert.EndsWith("only-2026.jpg", library.Resolve(config, TestHelpers.D("2026-12-25")).Image);
        Assert.EndsWith("every-year.jpg", library.Resolve(config, TestHelpers.D("2027-12-25")).Image);
    }

    [Theory]
    [InlineData("3.jpg")]
    [InlineData("03.jpg")]
    [InlineData("3.png")]
    public void 槽位文件零填充和任意扩展名都认(string fileName)
    {
        var (config, library) = TestHelpers.Setup("", fileName);

        // 2026-01-07 是周三 -> 槽位 3
        var result = library.Resolve(config, TestHelpers.D("2026-01-07"));

        Assert.EndsWith(fileName, result.Image);
    }

    [Fact]
    public void 同一槽位多个扩展名时按扩展名字母序取第一个()
    {
        var (config, library) = TestHelpers.Setup("", "3.jpg", "3.png", "3.bmp");

        // bmp < jpg < png
        var result = library.Resolve(config, TestHelpers.D("2026-01-07"));

        Assert.EndsWith("3.bmp", result.Image);
    }

    [Fact]
    public void 同一槽位多个扩展名的排序不受大小写影响()
    {
        var (config, library) = TestHelpers.Setup("", "3.PNG", "3.jpg");

        // jpg < png，跟大小写无关
        Assert.EndsWith("3.jpg", library.Resolve(config, TestHelpers.D("2026-01-07")).Image);
    }

    [Fact]
    public void 不认识的命名会被忽略()
    {
        var (config, library) = TestHelpers.Setup("", "monday.jpg", "周一.png", "default.jpg");

        // monday.jpg 和 周一.png 都不符合约定，只有 default 被认出来
        Assert.Single(library.All);
        Assert.EndsWith("default.jpg", library.Resolve(config, TestHelpers.D("2026-01-05")).Image);
    }

    [Fact]
    public void 非图片扩展名会被忽略()
    {
        var (config, library) = TestHelpers.Setup("", "1.txt", "1.jpg");

        Assert.Single(library.All);
        Assert.EndsWith("1.jpg", library.Resolve(config, TestHelpers.D("2026-01-05")).Image);
    }

    [Fact]
    public void 没有扩展名的文件会被忽略()
    {
        var (config, library) = TestHelpers.Setup("", "1", "default.jpg");

        Assert.Single(library.All);
        Assert.EndsWith("default.jpg", library.Resolve(config, TestHelpers.D("2026-01-05")).Image);
    }

    [Fact]
    public void webp之类的格式也能被认出来()
    {
        var (config, library) = TestHelpers.Setup("", "1.webp");

        Assert.EndsWith("1.webp", library.Resolve(config, TestHelpers.D("2026-01-05")).Image);
    }

    [Fact]
    public void 壁纸目录不存在时扫描结果为空()
    {
        var config = TestHelpers.Load("");

        var library = WallpaperLibrary.Scan(config.WallpapersDirectory);

        Assert.Empty(library.All);
        Assert.True(library.Resolve(config, TestHelpers.D("2026-01-05")).IsEmpty);
    }

    [Fact]
    public void 槽位号可以大于七()
    {
        var (config, library) = TestHelpers.Setup("""
            range: [10, 11, 12]
            rangeStartDay: "2026-01-05"
            """, "10.jpg", "11.jpg", "12.jpg");

        Assert.EndsWith("10.jpg", library.Resolve(config, TestHelpers.D("2026-01-05")).Image);
        Assert.EndsWith("11.jpg", library.Resolve(config, TestHelpers.D("2026-01-06")).Image);
    }
}
