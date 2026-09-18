using AutoPaper;

// 让中文输出在 Windows 终端里也正常显示；没有控制台时忽略
try
{
    if (!Console.IsOutputRedirected)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
}
catch
{
    // 忽略：某些宿主环境不允许改编码
}

return Cli.Run(args);
