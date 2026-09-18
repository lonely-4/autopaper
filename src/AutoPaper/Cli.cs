namespace AutoPaper;

/// <summary>命令行入口：解析参数，分发到 Commands。手写解析，不引第三方包。</summary>
public static class Cli
{
    public static int Run(string[] rawArgs)
    {
        string? configPath = null;
        var dryRun = false;
        var quiet = false;
        var verbose = false;
        var force = false;
        var positional = new List<string>();

        for (var i = 0; i < rawArgs.Length; i++)
        {
            switch (rawArgs[i])
            {
                case "--config":
                    if (i + 1 >= rawArgs.Length)
                        return UsageError("--config 后面要跟一个路径。");
                    configPath = rawArgs[++i];
                    break;

                case "--dry-run": dryRun = true; break;
                case "-q" or "--quiet": quiet = true; break;
                case "-v" or "--verbose": verbose = true; break;
                case "--force": force = true; break;

                case "-h" or "--help":
                    PrintHelp();
                    return 0;

                case "-V" or "--version":
                    PrintVersion();
                    return 0;

                default:
                    if (rawArgs[i].StartsWith('-'))
                        return UsageError($"不认识的选项：{rawArgs[i]}");
                    positional.Add(rawArgs[i]);
                    break;
            }
        }

        if (positional.Count == 0)
        {
            PrintHelp();
            return 1;
        }

        var options = new CliOptions(configPath, dryRun, quiet, verbose, force);
        var command = positional[0].ToLowerInvariant();
        var rest = positional.Skip(1).ToList();

        if (command == "preview" && rest.Count > 1)
            return UsageError("preview 最多只接受一个日期参数。");

        if (command != "preview" && rest.Count > 0)
            return UsageError($"{command} 不接受额外参数：{string.Join(" ", rest)}");

        switch (command)
        {
            case "apply": return Commands.Apply(options);
            case "preview": return Commands.Preview(options, rest.FirstOrDefault());
            case "check": return Commands.Check(options);
            case "init": return Commands.Init(options);
            case "install": return Commands.Install(options);
            case "uninstall": return Commands.Uninstall();
            case "help": PrintHelp(); return 0;
            case "version": PrintVersion(); return 0;
            default:
                Console.Error.WriteLine($"不认识的命令：{command}");
                Console.Error.WriteLine();
                PrintHelp();
                return 1;
        }
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("运行 autopaper help 看用法。");
        return 1;
    }

    private static void PrintVersion()
    {
        var version = typeof(Cli).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        Console.WriteLine($"autopaper {version}");
        Console.WriteLine($"运行平台   {Environment.OSVersion.Platform} / .NET {Environment.Version}");
        Console.WriteLine($"壁纸设置   {(Wallpaper.IsSupported ? "可用" : "在当前平台不可用（只有 Windows 支持）")}");
        Console.WriteLine($"配置目录   {AppPaths.ConfigDirectory}");
        Console.WriteLine($"默认配置   {AppPaths.DefaultConfigPath}");
        Console.WriteLine($"日志文件   {AppPaths.LogFilePath}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            autopaper — 按日期自动更换 Windows 壁纸

            用法： autopaper <命令> [选项]

            把图片放进壁纸目录，用文件名决定什么时候用哪张：
              1 2 3 4 5 6 7     周一 … 周日（周期性）
              2026-01-01        只在那一年的那天
              12-25             每年的这一天
              default           都没有匹配时兜底
            优先级：指定日期 > 周期性 > default > 不动

            命令：
              apply                应用今天的壁纸（计划任务调用这个）
              preview [日期]        查看某天会用哪张图，不改壁纸
              check                体检：文件命名、缺失、格式风险
              init                 生成 config.yaml 并建好壁纸目录
              install              注册计划任务：登录时 + 每天
              uninstall            删除计划任务
              version              显示版本和路径
              help                 显示本帮助

            选项：
              --config <路径>      指定配置文件
                                   （默认先找当前目录的 config.yaml，
                                     再找 %APPDATA%\AutoPaper\config.yaml；
                                     Linux/macOS 上则是 ~/.config/AutoPaper/config.yaml）
              --dry-run            apply 只打印结果，不真的换壁纸
              -q, --quiet          apply 只写日志（计划任务用）
              -v, --verbose        输出更详细
              --force              init 覆盖已存在的配置
              -h, --help           显示本帮助

            例子：
              autopaper init
              autopaper check
              autopaper preview 2026-01-01
              autopaper apply --dry-run
              autopaper install
            """);
    }
}
