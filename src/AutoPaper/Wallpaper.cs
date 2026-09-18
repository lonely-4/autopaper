using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AutoPaper;

/// <summary>壁纸填充方式。对应注册表里的 WallpaperStyle / TileWallpaper。</summary>
public enum WallpaperStyle { Center, Tile, Stretch, Fit, Fill, Span }

/// <summary>真正把图片设成壁纸。目前只有 Windows 实现。</summary>
public static class Wallpaper
{
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    private const int MaxAttempts = 3;

    public static bool IsSupported => OperatingSystem.IsWindows();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [SupportedOSPlatform("windows")]
    public static void Set(string imagePath, WallpaperStyle style)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("设置壁纸目前只实现了 Windows。");

        var full = Path.GetFullPath(imagePath);
        if (!File.Exists(full))
            throw new FileNotFoundException($"图片不存在：{full}", full);

        ApplyStyleToRegistry(style);

        // 刚登录时桌面可能还没准备好，失败就短暂重试几次
        for (var attempt = 1; ; attempt++)
        {
            if (SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, full, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                return;

            var error = Marshal.GetLastWin32Error();
            if (attempt >= MaxAttempts)
                throw new InvalidOperationException($"调用 SystemParametersInfo 设置壁纸失败（Win32 错误码 {error}）。");

            Thread.Sleep(500);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyStyleToRegistry(WallpaperStyle style)
    {
        var (styleValue, tile) = style switch
        {
            WallpaperStyle.Center => ("0", "0"),
            WallpaperStyle.Tile => ("0", "1"),
            WallpaperStyle.Stretch => ("2", "0"),
            WallpaperStyle.Fit => ("6", "0"),
            WallpaperStyle.Span => ("22", "0"),
            _ => ("10", "0"),   // Fill
        };

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)
            ?? throw new InvalidOperationException(@"打不开注册表项 HKCU\Control Panel\Desktop。");

        key.SetValue("WallpaperStyle", styleValue, RegistryValueKind.String);
        key.SetValue("TileWallpaper", tile, RegistryValueKind.String);
    }
}
