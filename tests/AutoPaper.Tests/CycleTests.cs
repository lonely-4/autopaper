using AutoPaper;

namespace AutoPaper.Tests;

/// <summary>循环槽位换算。默认应当是 1=周一 … 7=周日。</summary>
public class CycleTests
{
    private static AutoPaperConfig Default => TestHelpers.Load("");

    [Theory]
    [InlineData("2026-01-05", 1)]   // 周一
    [InlineData("2026-01-06", 2)]   // 周二
    [InlineData("2026-01-07", 3)]   // 周三
    [InlineData("2026-01-08", 4)]   // 周四
    [InlineData("2026-01-09", 5)]   // 周五
    [InlineData("2026-01-10", 6)]   // 周六
    [InlineData("2026-01-11", 7)]   // 周日
    public void 默认情况下周一是一周日是七(string date, int expected)
    {
        Assert.Equal(expected, Default.SlotFor(TestHelpers.D(date)));
    }

    [Fact]
    public void 默认锚点确实是周一()
    {
        Assert.Equal(DayOfWeek.Monday, AutoPaperConfig.DefaultRangeStartDay.DayOfWeek);
    }

    [Fact]
    public void 每一周都重复同样的槽位()
    {
        var monday = TestHelpers.D("2026-01-05");
        for (var week = 0; week < 5; week++)
            Assert.Equal(1, Default.SlotFor(monday.AddDays(week * 7)));
    }

    [Fact]
    public void 把起始日放在周日就变成周日是一()
    {
        var config = TestHelpers.Load("""
            rangeStartDay: "2026-01-11"
            """);

        Assert.Equal(DayOfWeek.Sunday, TestHelpers.D("2026-01-11").DayOfWeek);
        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-11")));
        Assert.Equal(2, config.SlotFor(TestHelpers.D("2026-01-12")));
        Assert.Equal(7, config.SlotFor(TestHelpers.D("2026-01-17")));
    }

    [Fact]
    public void 可以改成三天一个循环()
    {
        var config = TestHelpers.Load("""
            range: [1, 2, 3]
            rangeStartDay: "2026-01-05"
            """);

        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-05")));
        Assert.Equal(2, config.SlotFor(TestHelpers.D("2026-01-06")));
        Assert.Equal(3, config.SlotFor(TestHelpers.D("2026-01-07")));
        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-08")));
    }

    [Fact]
    public void range里重复的数字表示连着几天用同一张()
    {
        var config = TestHelpers.Load("""
            range: [1, 1, 2]
            rangeStartDay: "2026-01-05"
            """);

        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-05")));
        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-06")));
        Assert.Equal(2, config.SlotFor(TestHelpers.D("2026-01-07")));
        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-08")));
    }

    [Fact]
    public void range可以重排顺序()
    {
        var config = TestHelpers.Load("""
            range: [3, 2, 1]
            rangeStartDay: "2026-01-05"
            """);

        Assert.Equal(3, config.SlotFor(TestHelpers.D("2026-01-05")));
        Assert.Equal(2, config.SlotFor(TestHelpers.D("2026-01-06")));
        Assert.Equal(1, config.SlotFor(TestHelpers.D("2026-01-07")));
    }

    [Fact]
    public void 起始日在未来时不会算出负数()
    {
        // 取模后不补一次的话这里会得到负数下标，直接抛异常
        var config = TestHelpers.Load("""
            rangeStartDay: "2030-01-01"
            """);

        var early = TestHelpers.D("2026-01-05");
        var slot = config.SlotFor(early);

        Assert.InRange(slot, 1, 7);
        Assert.Equal(slot, config.SlotFor(early.AddDays(7)));
    }
}
