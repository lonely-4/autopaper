using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AutoPaper;

/// <summary>配置有问题时抛出，消息直接面向使用者。</summary>
public sealed class ConfigException(string message) : Exception(message);

/// <summary>specialDays 里的一条。Year 为 null 表示每年都算。</summary>
public sealed record SpecialDay(int? Year, int Month, int Day, string Image, int Line)
{
    public bool Matches(DateOnly date) => Year is null
        ? date.Month == Month && date.Day == Day
        : date.Year == Year && date.Month == Month && date.Day == Day;

    public string Describe() => Year is null
        ? $"{Month:D2}-{Day:D2}"
        : $"{Year:D4}-{Month:D2}-{Day:D2}";
}

/// <summary>config.yaml 解析并校验之后的结果。</summary>
public sealed class AutoPaperConfig
{
    /// <summary>默认一轮七天，也就是一周一个循环。</summary>
    public static readonly int[] DefaultRange = [1, 2, 3, 4, 5, 6, 7];

    /// <summary>0001-01-01 是周一，所以默认 1=周一 … 7=周日。</summary>
    public static readonly DateOnly DefaultRangeStartDay = new(1, 1, 1);

    public required string ConfigPath { get; init; }
    public required string BaseDirectory { get; init; }
    public IReadOnlyList<int> Range { get; init; } = DefaultRange;
    public DateOnly RangeStartDay { get; init; } = DefaultRangeStartDay;
    public WallpaperStyle Style { get; init; } = WallpaperStyle.Fill;
    public IReadOnlyList<SpecialDay> SpecialDays { get; init; } = [];

    /// <summary>壁纸目录：配置目录下的 wallpapers/。</summary>
    public string WallpapersDirectory => Path.Combine(BaseDirectory, AppPaths.WallpapersFolderName);

    /// <summary>算出某一天落在循环里的哪个槽位。</summary>
    public int SlotFor(DateOnly date)
    {
        var count = Range.Count;
        var offset = date.DayNumber - RangeStartDay.DayNumber;

        // 先取模再加一次，避免 rangeStartDay 设在未来时出现负数
        var index = ((offset % count) + count) % count;
        return Range[index];
    }

    /// <summary>配置里为该日期指定的壁纸。具体某一年优先于"每年"。</summary>
    public SpecialDay? SpecialFor(DateOnly date) =>
        SpecialDays.FirstOrDefault(d => d.Year is not null && d.Matches(date))
        ?? SpecialDays.FirstOrDefault(d => d.Year is null && d.Matches(date));

    /// <summary>specialDays 里的图片路径：相对路径按壁纸目录解析。</summary>
    public string ToImagePath(string image) =>
        Path.GetFullPath(Path.IsPathRooted(image) ? image : Path.Combine(WallpapersDirectory, image));
}

/// <summary>
/// 读取 config.yaml。
/// 这里只用 YamlDotNet 的表示模型层（纯节点树），不用它的反序列化器——
/// 那条路依赖反射，跟裁剪/AOT 冲突；手写映射反而更简单，也能给出精确到行号的报错。
/// </summary>
public static class ConfigLoader
{
    private static readonly string[] KnownKeys = ["range", "rangeStartDay", "style", "specialDays"];

    /// <summary>按顺序找配置：显式路径 -&gt; 当前目录 -&gt; 用户配置目录。</summary>
    public static string ResolvePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return Path.GetFullPath(explicitPath);

        // 当前目录优先，方便开发和"整个文件夹拷走"的用法
        var inCwd = Path.GetFullPath(AppPaths.ConfigFileName);
        return File.Exists(inCwd) ? inCwd : AppPaths.DefaultConfigPath;
    }

    public static AutoPaperConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new ConfigException($"找不到配置文件：{path}\n可以先运行 autopaper init 生成一份。");

        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath)!;
        var root = ParseRoot(fullPath);

        if (root is null)
            return new AutoPaperConfig { ConfigPath = fullPath, BaseDirectory = baseDirectory };

        var range = (IReadOnlyList<int>)AutoPaperConfig.DefaultRange;
        var rangeStartDay = AutoPaperConfig.DefaultRangeStartDay;
        var style = WallpaperStyle.Fill;
        IReadOnlyList<SpecialDay> specialDays = [];

        foreach (var (keyNode, valueNode) in root.Children)
        {
            var key = ScalarText(keyNode, "配置项名称");

            switch (key)
            {
                case "range":
                    range = ParseRange(valueNode);
                    break;
                case "rangeStartDay":
                    rangeStartDay = ParseDate(valueNode, key);
                    break;
                case "style":
                    style = ParseStyle(valueNode);
                    break;
                case "specialDays":
                    specialDays = ParseSpecialDays(valueNode);
                    break;
                default:
                    throw new ConfigException(
                        $"第 {keyNode.Start.Line} 行：不认识的配置项 \"{key}\"。\n" +
                        $"  可用的配置项：{string.Join("、", KnownKeys)}");
            }
        }

        return new AutoPaperConfig
        {
            ConfigPath = fullPath,
            BaseDirectory = baseDirectory,
            Range = range,
            RangeStartDay = rangeStartDay,
            Style = style,
            SpecialDays = specialDays,
        };
    }

    /// <summary>顶层映射节点；空文件返回 null（全部走默认值）。</summary>
    private static YamlMappingNode? ParseRoot(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var stream = new YamlStream();
            stream.Load(reader);

            if (stream.Documents.Count == 0)
                return null;

            return stream.Documents[0].RootNode as YamlMappingNode
                   ?? throw new ConfigException(
                       $"配置文件的顶层应该是一组 key: value，实际不是（{path}）。");
        }
        catch (YamlException ex)
        {
            throw new ConfigException(
                $"配置文件不是合法的 YAML：{path}\n" +
                $"  第 {ex.Start.Line} 行 第 {ex.Start.Column} 列：{ex.Message}");
        }
    }

    private static string ScalarText(YamlNode node, string what)
    {
        if (node is not YamlScalarNode scalar || scalar.Value is null)
            throw new ConfigException($"第 {node.Start.Line} 行：{what} 应该是一个值。");
        return scalar.Value;
    }

    /// <summary>
    /// "key:" 后面什么都不写（或者只跟注释），YAML 会解析成一个空标量。
    /// 这跟"根本没配这一项"是一回事——对应用户"把内容全注释掉"的常见写法，
    /// 所以各个解析函数遇到它就返回自己的默认值，而不是报错。
    /// </summary>
    private static bool IsNullOrEmptyScalar(YamlNode node) =>
        node is YamlScalarNode scalar &&
        (string.IsNullOrWhiteSpace(scalar.Value)
         || scalar.Value is "~"
         || scalar.Value.Equals("null", StringComparison.OrdinalIgnoreCase));

    private static int[] ParseRange(YamlNode node)
    {
        // "range:" 后面只跟注释就等于没配，用默认的一周一轮
        if (IsNullOrEmptyScalar(node))
            return AutoPaperConfig.DefaultRange;

        if (node is not YamlSequenceNode sequence)
            throw new ConfigException(
                $"第 {node.Start.Line} 行：range 应该是一个列表，例如 [1, 2, 3, 4, 5, 6, 7]。");

        if (sequence.Children.Count == 0)
            throw new ConfigException($"第 {node.Start.Line} 行：range 不能是空列表。");

        var values = new int[sequence.Children.Count];
        for (var i = 0; i < values.Length; i++)
        {
            var child = sequence.Children[i];
            var text = ScalarText(child, "range 里的每个元素");

            if (!int.TryParse(text, out var value) || value < 1)
                throw new ConfigException($"第 {child.Start.Line} 行：range 里的 \"{text}\" 不是正整数。");

            values[i] = value;
        }

        return values;
    }

    private static DateOnly ParseDate(YamlNode node, string field)
    {
        if (IsNullOrEmptyScalar(node))
            return AutoPaperConfig.DefaultRangeStartDay;

        var text = ScalarText(node, field);
        if (!DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", out var date))
            throw new ConfigException(
                $"第 {node.Start.Line} 行：{field} 的格式应为 yyyy-MM-dd，实际是 \"{text}\"。");

        return date;
    }

    private static WallpaperStyle ParseStyle(YamlNode node)
    {
        if (IsNullOrEmptyScalar(node))
            return WallpaperStyle.Fill;

        var text = ScalarText(node, "style").Trim().ToLowerInvariant();

        return text switch
        {
            "fill" => WallpaperStyle.Fill,
            "fit" => WallpaperStyle.Fit,
            "stretch" => WallpaperStyle.Stretch,
            "center" => WallpaperStyle.Center,
            "tile" => WallpaperStyle.Tile,
            "span" => WallpaperStyle.Span,
            _ => throw new ConfigException(
                $"第 {node.Start.Line} 行：style 只能是 fill / fit / stretch / center / tile / span，实际是 \"{text}\"。"),
        };
    }

    private static List<SpecialDay> ParseSpecialDays(YamlNode node)
    {
        // "specialDays:" 后面什么都不写（比如示例配置里全是注释）等于不指定任何特殊日期
        if (IsNullOrEmptyScalar(node))
            return [];

        if (node is not YamlMappingNode mapping)
            throw new ConfigException(
                $"第 {node.Start.Line} 行：specialDays 应该是一组 \"日期: 图片\"。");

        var result = new List<SpecialDay>();
        var seen = new HashSet<(int? Year, int Month, int Day)>();

        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            var key = ScalarText(keyNode, "specialDays 的日期");
            var image = ScalarText(valueNode, "specialDays 的图片路径").Trim();
            var line = (int)keyNode.Start.Line;

            if (image.Length == 0)
                throw new ConfigException($"第 {line} 行：specialDays 的 \"{key}\" 没有指定图片。");

            var (year, month, day) = ParseSpecialKey(key, line);

            if (!seen.Add((year, month, day)))
                throw new ConfigException($"第 {line} 行：specialDays 里的 \"{key}\" 重复了。");

            result.Add(new SpecialDay(year, month, day, image, line));
        }

        return result;
    }

    /// <summary>specialDays 的 key 支持 "2026-01-01"（某一年）和 "12-25"（每年）。</summary>
    private static (int? Year, int Month, int Day) ParseSpecialKey(string key, int line)
    {
        var parts = key.Trim().Split('-');

        if (parts.Length == 3
            && int.TryParse(parts[0], out var year)
            && int.TryParse(parts[1], out var month)
            && int.TryParse(parts[2], out var day)
            && year >= 1
            && month is >= 1 and <= 12
            && day >= 1 && day <= DateTime.DaysInMonth(year, month))
        {
            return (year, month, day);
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], out var yearlyMonth)
            && int.TryParse(parts[1], out var yearlyDay)
            && yearlyMonth is >= 1 and <= 12
            && yearlyDay >= 1 && yearlyDay <= DateTime.DaysInMonth(2000, yearlyMonth))
        {
            return (null, yearlyMonth, yearlyDay);
        }

        throw new ConfigException(
            $"第 {line} 行：specialDays 的 \"{key}\" 不是合法日期。\n" +
            "  支持 2026-01-01（只在那一年）和 12-25（每年）两种写法。");
    }
}
