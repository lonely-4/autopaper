namespace AutoPaper;

/// <summary>某一天最终选中的图片，以及它是被哪条规则选中的（用于 preview、check 和日志）。</summary>
public sealed record Resolution(string? Image, string Source)
{
    public bool IsEmpty => Image is null;
}

/// <summary>
/// 壁纸目录里的一个候选文件。
/// 命名约定：default / yyyy-MM-dd / MM-dd / 槽位号，四种之一。
/// </summary>
public sealed record Candidate(
    string Path,
    string Stem,
    string Extension,
    DateOnly? Date = null,
    int? Month = null,
    int? Day = null,
    int? Slot = null)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>同一槽位/日期出现多个扩展名时，按扩展名字母序决定用哪个。</summary>
    public static int CompareByExtensionThenName(Candidate a, Candidate b)
    {
        var byExtension = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase);
        return byExtension != 0
            ? byExtension
            : string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 扫描 wallpapers 目录，按优先级挑出某一天该用的图片。
/// 纯文件系统逻辑，不碰 Windows API，所以在任何平台上都能开发和测试。
/// </summary>
public sealed class WallpaperLibrary
{
    /// <summary>会被当作壁纸候选的扩展名。</summary>
    public static readonly IReadOnlySet<string> RecognizedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bmp", "dib", "gif", "jpg", "jpeg", "jpe", "jfif",
            "png", "tif", "tiff", "webp", "avif", "heic", "heif",
        };

    /// <summary>
    /// Windows 能稳定设为壁纸的扩展名。
    /// webp / avif / heic 要看系统装没装对应编解码器，所以不在这个集合里，check 会提醒。
    /// </summary>
    public static readonly IReadOnlySet<string> WindowsReliableExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bmp", "dib", "gif", "jpg", "jpeg", "jpe", "jfif", "png", "tif", "tiff",
        };

    private readonly List<Candidate> _all = [];
    private readonly List<Candidate> _byDate = [];
    private readonly List<Candidate> _byYearly = [];
    private readonly List<Candidate> _byDefault = [];
    private readonly Dictionary<int, List<Candidate>> _bySlot = [];

    public string Directory { get; private init; } = "";

    /// <summary>目录里所有被认出来的候选文件。</summary>
    public IReadOnlyList<Candidate> All => _all;

    public IReadOnlyList<Candidate> DateNamed => _byDate;
    public IReadOnlyList<Candidate> YearlyNamed => _byYearly;
    public IReadOnlyList<Candidate> DefaultNamed => _byDefault;

    /// <summary>槽位号 -&gt; 该槽位的候选，已按扩展名字母序排好。</summary>
    public IReadOnlyDictionary<int, List<Candidate>> Slots => _bySlot;

    public static WallpaperLibrary Scan(string directory)
    {
        var library = new WallpaperLibrary { Directory = directory };

        if (!System.IO.Directory.Exists(directory))
            return library;

        foreach (var path in System.IO.Directory.EnumerateFiles(directory))
        {
            var extension = System.IO.Path.GetExtension(path);
            if (extension.Length < 2)
                continue;   // 没有扩展名，跳过

            var extensionName = extension[1..];
            if (!RecognizedExtensions.Contains(extensionName))
                continue;   // 不是图片，跳过

            var stem = System.IO.Path.GetFileNameWithoutExtension(path).Trim();
            if (stem.Length == 0)
                continue;

            if (stem.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                library.Add(library._byDefault, new Candidate(path, stem, extensionName));
            }
            else if (DateOnly.TryParseExact(stem, "yyyy-MM-dd", out var date))
            {
                library.Add(library._byDate, new Candidate(path, stem, extensionName, Date: date));
            }
            else if (TryParseMonthDay(stem, out var month, out var day))
            {
                library.Add(library._byYearly, new Candidate(path, stem, extensionName, Month: month, Day: day));
            }
            else if (int.TryParse(stem, out var slot) && slot >= 1)
            {
                if (!library._bySlot.TryGetValue(slot, out var list))
                    library._bySlot[slot] = list = [];

                library.Add(list, new Candidate(path, stem, extensionName, Slot: slot));
            }

            // 其他命名一律忽略，不参与匹配
        }

        library._byDate.Sort(Candidate.CompareByExtensionThenName);
        library._byYearly.Sort(Candidate.CompareByExtensionThenName);
        library._byDefault.Sort(Candidate.CompareByExtensionThenName);

        foreach (var list in library._bySlot.Values)
            list.Sort(Candidate.CompareByExtensionThenName);

        return library;
    }

    private void Add(List<Candidate> list, Candidate candidate)
    {
        list.Add(candidate);
        _all.Add(candidate);
    }

    /// <summary>按优先级挑出这一天该用的图片。全部落空时返回空结果，表示"不做任何修改"。</summary>
    public Resolution Resolve(AutoPaperConfig config, DateOnly date)
    {
        // 1. 配置里显式指定的日期，优先级最高
        if (config.SpecialFor(date) is { } special)
            return new Resolution(config.ToImagePath(special.Image), $"specialDays {special.Describe()}");

        // 2. 文件名就是这一天：2026-01-01.jpg
        var exact = _byDate.FirstOrDefault(c => c.Date == date);
        if (exact is not null)
            return new Resolution(exact.Path, $"文件名 {exact.Stem}");

        // 3. 文件名是每年的这个月日：12-25.jpg
        var yearly = _byYearly.FirstOrDefault(c => c.Month == date.Month && c.Day == date.Day);
        if (yearly is not null)
            return new Resolution(yearly.Path, $"文件名 {yearly.Stem}");

        // 4. 周期槽位
        var slot = config.SlotFor(date);
        if (_bySlot.TryGetValue(slot, out var slotFiles) && slotFiles.Count > 0)
            return new Resolution(slotFiles[0].Path, $"周期槽位 {slot}（{slotFiles[0].FileName}）");

        // 5. 兜底
        if (_byDefault.Count > 0)
            return new Resolution(_byDefault[0].Path, $"default（{_byDefault[0].FileName}）");

        return new Resolution(null, "没有匹配的文件");
    }

    /// <summary>解析 "12-25" 这种"月-日"。用闰年判断，所以 02-29 是合法的。</summary>
    private static bool TryParseMonthDay(string stem, out int month, out int day)
    {
        month = 0;
        day = 0;

        var parts = stem.Split('-');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out var parsedMonth) || !int.TryParse(parts[1], out var parsedDay))
            return false;

        if (parsedMonth is < 1 or > 12 || parsedDay < 1 || parsedDay > DateTime.DaysInMonth(2000, parsedMonth))
            return false;

        month = parsedMonth;
        day = parsedDay;
        return true;
    }
}
