namespace AutoPaper;

public sealed record CliOptions(string? ConfigPath, bool DryRun, bool Quiet, bool Verbose, bool Force);

/// <summary>各个子命令的实现。除 apply 之外都不碰系统，便于在任何平台上运行。</summary>
public static class Commands
{
    /// <summary>把今天该用的壁纸设上。计划任务调用的就是它。</summary>
    public static int Apply(CliOptions options)
    {
        var path = ConfigLoader.ResolvePath(options.ConfigPath);

        AutoPaperConfig config;
        try
        {
            config = ConfigLoader.Load(path);
        }
        catch (ConfigException ex)
        {
            Log.Write($"失败：{ex.Message}");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var library = WallpaperLibrary.Scan(config.WallpapersDirectory);
        var resolution = library.Resolve(config, today);

        if (resolution.Image is null)
        {
            // 没图可换不是错误，用非 0 退出码只会让计划任务一直报红
            var message = $"{today:yyyy-MM-dd} 没有匹配的壁纸（{resolution.Source}），保持当前壁纸。";
            Log.Write(message);
            if (!options.Quiet) Console.WriteLine(message);
            return 0;
        }

        if (!File.Exists(resolution.Image))
        {
            var message = $"{today:yyyy-MM-dd} 选中的图片不存在：{resolution.Image}（{resolution.Source}）";
            Log.Write($"失败：{message}");
            Console.Error.WriteLine(message);
            Console.Error.WriteLine("运行 autopaper check 可以一次列出所有问题。");
            return 1;
        }

        if (options.DryRun)
        {
            var message = $"[试运行] {today:yyyy-MM-dd} -> {resolution.Image}（{resolution.Source}，style={config.Style}）";
            Log.Write(message);
            if (!options.Quiet) Console.WriteLine(message);
            return 0;
        }

        // 平台分析器要求这里显式判断，顺带给出比抛异常更好的提示
        if (!OperatingSystem.IsWindows())
        {
            const string message = "设置壁纸目前只支持 Windows，当前平台上 apply 不可用（preview 和 check 仍然可用）。";
            Log.Write($"失败：{message}");
            Console.Error.WriteLine(message);
            return 1;
        }

        try
        {
            Wallpaper.Set(resolution.Image, config.Style);
        }
        catch (Exception ex)
        {
            Log.Write($"失败：{ex.Message}");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var done = $"{today:yyyy-MM-dd} 已应用 {Path.GetFileName(resolution.Image)}（{resolution.Source}）";
        Log.Write(done);
        if (!options.Quiet || options.Verbose) Console.WriteLine(done);
        return 0;
    }

    /// <summary>看某一天会用哪张图，但不改壁纸。调试主要靠它。</summary>
    public static int Preview(CliOptions options, string? dateText)
    {
        var path = ConfigLoader.ResolvePath(options.ConfigPath);

        AutoPaperConfig config;
        try
        {
            config = ConfigLoader.Load(path);
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        DateOnly date;
        if (string.IsNullOrWhiteSpace(dateText))
        {
            date = DateOnly.FromDateTime(DateTime.Now);
        }
        else if (!DateOnly.TryParseExact(dateText.Trim(), "yyyy-MM-dd", out date))
        {
            Console.Error.WriteLine($"日期格式应为 yyyy-MM-dd，实际是 \"{dateText}\"。例如 2026-01-01。");
            return 1;
        }

        var library = WallpaperLibrary.Scan(config.WallpapersDirectory);
        var resolution = library.Resolve(config, date);

        Console.WriteLine($"配置文件   {config.ConfigPath}");
        Console.WriteLine($"壁纸目录   {config.WallpapersDirectory}");
        Console.WriteLine($"日期       {date:yyyy-MM-dd}（{WeekdayName(date.DayOfWeek)}）");
        Console.WriteLine($"循环槽位   {config.SlotFor(date)}");

        if (resolution.Image is null)
        {
            Console.WriteLine("结果       没有匹配的壁纸，apply 不会做任何修改");
            return 0;
        }

        Console.WriteLine($"选中来源   {resolution.Source}");
        Console.WriteLine($"图片       {Path.GetFileName(resolution.Image)}");
        Console.WriteLine($"绝对路径   {resolution.Image}");
        Console.WriteLine($"文件存在   {(File.Exists(resolution.Image) ? "是" : "否  <-- 需要注意")}");
        Console.WriteLine($"填充样式   {config.Style}");
        return 0;
    }

    /// <summary>把配置、目录和所有文件都体检一遍。</summary>
    public static int Check(CliOptions options)
    {
        var path = ConfigLoader.ResolvePath(options.ConfigPath);

        AutoPaperConfig config;
        try
        {
            config = ConfigLoader.Load(path);
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var library = WallpaperLibrary.Scan(config.WallpapersDirectory);
        var problems = new List<string>();
        var warnings = new List<string>();

        Console.WriteLine($"配置文件   {config.ConfigPath}");
        Console.WriteLine($"壁纸目录   {config.WallpapersDirectory}");
        Console.WriteLine($"循环       [{string.Join(", ", config.Range)}]，起点 {config.RangeStartDay:yyyy-MM-dd}（{WeekdayName(config.RangeStartDay.DayOfWeek)}），长度 {config.Range.Count} 天");
        Console.WriteLine($"填充样式   {config.Style}");
        Console.WriteLine();

        // 周期性槽位
        Console.WriteLine("周期性槽位：");
        var missingSlots = new List<int>();

        foreach (var slot in config.Range.Distinct().Order())
        {
            if (library.Slots.TryGetValue(slot, out var files) && files.Count > 0)
            {
                Console.WriteLine($"  [ok]    槽位 {slot,-3} → {files[0].FileName}");

                if (files.Count > 1)
                {
                    var names = string.Join("、", files.Select(f => f.FileName));
                    warnings.Add($"槽位 {slot} 有多个扩展名：{names}（按扩展名字母序用了 {files[0].FileName}）");
                }

                WarnIfRiskyFormat(files[0], warnings);
            }
            else
            {
                missingSlots.Add(slot);
                Console.WriteLine($"  [缺失]  槽位 {slot,-3} 需要 {slot}.<图片格式> 或 {slot:D2}.<图片格式>");
            }
        }

        if (missingSlots.Count > 0)
        {
            var days = string.Join("、", missingSlots);
            warnings.Add(library.DefaultNamed.Count > 0
                ? $"槽位 {days} 没有对应的图片，那些天会使用 default"
                : $"槽位 {days} 没有对应的图片，也没有 default，那些天不会改变壁纸");
        }

        Console.WriteLine();
        Console.WriteLine("兜底：");
        if (library.DefaultNamed.Count > 0)
        {
            Console.WriteLine($"  [ok]    default → {library.DefaultNamed[0].FileName}");
            WarnIfRiskyFormat(library.DefaultNamed[0], warnings);
        }
        else
        {
            Console.WriteLine("  [无]    没有 default.*");
            warnings.Add("没有 default.*，建议放一张作为兜底，这样缺图的日子也有壁纸可换");
        }

        // specialDays
        if (config.SpecialDays.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("指定日期（specialDays）：");

            foreach (var special in config.SpecialDays)
            {
                var full = config.ToImagePath(special.Image);

                if (File.Exists(full))
                {
                    Console.WriteLine($"  [ok]    {special.Describe(),-12} → {Path.GetFileName(full)}");
                }
                else
                {
                    Console.WriteLine($"  [缺失]  {special.Describe(),-12} → {special.Image}");
                    Console.WriteLine($"          找不到：{full}（配置第 {special.Line} 行）");
                    problems.Add($"specialDays 里 {special.Describe()} 指向的图片不存在：{full}");
                }
            }
        }

        // 文件名日期
        if (library.DateNamed.Count > 0 || library.YearlyNamed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("文件名日期：");

            foreach (var candidate in library.YearlyNamed)
                Console.WriteLine($"  [ok]    每年 {candidate.Month:D2}-{candidate.Day:D2} → {candidate.FileName}");

            foreach (var candidate in library.DateNamed)
                Console.WriteLine($"  [ok]    {candidate.Date:yyyy-MM-dd} → {candidate.FileName}");
        }

        // 目录里认不出来的文件。这个最容易让人困惑，必须报出来；
        // 但 specialDays 直接引用的文件不算"没认出来"——它们本来就不需要遵守命名约定。
        var recognized = library.All
            .Select(candidate => Path.GetFullPath(candidate.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedBySpecialDays = config.SpecialDays
            .Select(special => Path.GetFullPath(config.ToImagePath(special.Image)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unrecognized = Directory.Exists(config.WallpapersDirectory)
            ? Directory.EnumerateFiles(config.WallpapersDirectory)
                .Where(file => !recognized.Contains(Path.GetFullPath(file)))
                .Where(file => !referencedBySpecialDays.Contains(Path.GetFullPath(file)))
                .Select(Path.GetFileName)
                .Order()
                .ToList()
            : [];

        if (unrecognized.Count > 0)
        {
            warnings.Add(
                $"有 {unrecognized.Count} 个文件不符合命名约定，不会被自动选中：{string.Join("、", unrecognized)}\n" +
                "        约定是 default / yyyy-MM-dd / MM-dd / 数字（1、2、3…）加图片扩展名");
        }

        // 一张图都没有，这个工具就什么也做不了，属于硬错误
        if (library.All.Count == 0)
            problems.Add($"壁纸目录里没有任何可用的图片：{config.WallpapersDirectory}");

        if (warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("提醒：");
            foreach (var warning in warnings)
                Console.WriteLine($"  - {warning}");
        }

        Console.WriteLine();
        if (problems.Count > 0)
        {
            Console.WriteLine("检查不通过：");
            foreach (var problem in problems)
                Console.WriteLine($"  - {problem}");
        }
        else if (warnings.Count > 0)
        {
            Console.WriteLine("检查通过，但有上面几条提醒。");
        }
        else
        {
            Console.WriteLine("检查通过。");
        }

        return problems.Count == 0 ? 0 : 1;
    }

    private static void WarnIfRiskyFormat(Candidate candidate, List<string> warnings)
    {
        if (WallpaperLibrary.WindowsReliableExtensions.Contains(candidate.Extension))
            return;

        warnings.Add(
            $"{candidate.FileName} 是 .{candidate.Extension} 格式，Windows 不一定能设为壁纸" +
            "（取决于系统有没有装对应编解码器），建议换用 png 或 jpg");
    }

    /// <summary>生成一份带注释的 config.yaml，并自动建好墙纸目录。</summary>
    public static int Init(CliOptions options)
    {
        var path = ConfigLoader.ResolvePath(options.ConfigPath);

        if (File.Exists(path) && !options.Force)
        {
            Console.Error.WriteLine($"配置文件已存在：{path}");
            Console.Error.WriteLine("要覆盖它请加 --force。");
            return 1;
        }

        var configDir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(configDir);

        var wallpapersDir = Path.Combine(configDir, AppPaths.WallpapersFolderName);
        Directory.CreateDirectory(wallpapersDir);

        File.WriteAllText(path, SampleConfig);

        Console.WriteLine($"已生成配置：{path}");
        Console.WriteLine($"壁纸目录：  {wallpapersDir}");
        Console.WriteLine();
        Console.WriteLine("接下来：");
        Console.WriteLine($"  1. 把图片放进 {AppPaths.WallpapersFolderName}/，命名成 1、2、3…7（1=周一，7=周日）");
        Console.WriteLine("     放一张 default 作为兜底，想指定某天就命名成 2026-01-01");
        Console.WriteLine("  2. autopaper check                体检一遍，看文件名有没有写错");
        Console.WriteLine("  3. autopaper preview 2026-01-01   看某天会用哪张");
        Console.WriteLine("  4. autopaper apply --dry-run      确认今天会选哪张");
        Console.WriteLine("  5. autopaper install              注册计划任务，之后就全自动了");
        return 0;
    }

    public static int Install(CliOptions options)
    {
        var path = ConfigLoader.ResolvePath(options.ConfigPath);

        // 先确认配置能用，免得装完任务才发现配置是坏的
        try
        {
            ConfigLoader.Load(path);
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine("配置有问题，先修好再安装：");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        return Scheduler.Install(path);
    }

    public static int Uninstall() => Scheduler.Uninstall();

    public static string WeekdayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日",
    };

    private const string SampleConfig = """
        # AutoPaper 配置
        # 整份文件删掉也能正常工作，所有配置项都有默认值。

        # 周期性壁纸的循环顺序，默认一周一个循环。
        # 数组长度就是循环长度；同一个数字可以重复，表示连着几天用同一张。
        # 每个数字对应 wallpapers/ 里同名的图片，例如 3 对应 3.jpg 或 03.png。
        range: [1, 2, 3, 4, 5, 6, 7]

        # range 里第一个元素对应哪一天。
        # 默认 0001-01-01（那天正好是周一），所以 1=周一 … 7=周日。
        # 想让周日对应 1，就把它改成任意一个星期天。
        rangeStartDay: "0001-01-01"

        # 壁纸填充方式：fill / fit / stretch / center / tile / span
        style: fill

        # 指定某天用哪张图，优先级高于上面的周期性壁纸。
        #   "2026-01-01"  只在 2026 年的那天生效
        #   "12-25"       每年这天都生效
        # 图片路径相对 wallpapers/ 目录，也可以写绝对路径。
        # 下面这行表示暂时不指定任何日期；删掉整行也一样。
        specialDays: {}
        """;
}
