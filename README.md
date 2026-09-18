# AutoPaper

按日期自动换 Windows 壁纸。**不用写规则**——把图片按文件名放好就行。

```
wallpapers/
  1.png            周一
  2.png            周二
  3.png            周三
  4.png            周四
  5.png            周五
  6.png            周六
  7.png            周日
  2026-01-01.png   只在 2026 年元旦用这张
  12-25.png        每年 12 月 25 日都用这张
  default.png      上面都没匹配上时用这张
```

装一次，之后每天开机自动换，不用管。

- Windows 10 / 11
- 单个 exe，**不需要装 .NET 运行时**
- 配置就一个 `config.yaml`，全部有默认值，删掉也能跑

---

## 快速开始

1. 从 [Releases](../../releases) 下载 `autopaper-*-win-x64-selfcontained.zip`，解压出 `autopaper.exe`。

2. 生成配置和壁纸目录：

   ```powershell
   .\autopaper.exe init
   ```

   它会建好 `%APPDATA%\AutoPaper\config.yaml` 和 `%APPDATA%\AutoPaper\wallpapers\`。

3. 把图片丢进 `wallpapers\`，按上面的约定命名。

4. 先体检一遍，确认文件名没写错：

   ```powershell
   .\autopaper.exe check
   ```

5. 看一眼今天会用哪张（不改任何设置）：

   ```powershell
   .\autopaper.exe preview
   ```

6. 注册计划任务，之后就全自动了：

   ```powershell
   .\autopaper.exe install
   ```

   会注册两个任务：**登录时**跑一次，以及**每天 00:05** 跑一次。不需要管理员权限，任务装在当前用户下。

想卸载：`.\autopaper.exe uninstall`。

---

## 文件名约定

图片放在 `wallpapers\` 目录里，**文件名决定什么时候用它**：

| 文件名 | 含义 |
| --- | --- |
| `1.png` … `7.png` | 周期性：按 `range` 循环（默认 1=周一 … 7=周日） |
| `03.png` | 同上，前导零随便写，`03` 和 `3` 是一回事 |
| `2026-01-01.png` | **只在 2026 年 1 月 1 日**用这张 |
| `12-25.png` | **每年** 12 月 25 日都用这张 |
| `default.png` | 上面都没匹配上时的兜底 |

同一张图想给几天用，不用复制文件——改 `range` 就行（见下）。

**扩展名**支持 `bmp` `dib` `gif` `jpg` `jpeg` `jpe` `jfif` `png` `tif` `tiff`，另外 `webp` `avif` `heic` `heif` 也认，但**能不能设成壁纸取决于系统装没装对应编解码器**，`check` 会提醒你。

**同一个槽位放了多个扩展名**（比如 `3.jpg` 和 `03.png`），按扩展名字母序取第一个。注意字母序意味着 `.bmp` 排在 `.jpg` 前面，所以 `3.bmp` 会赢过 `3.jpg`——而 bmp 体积通常是 jpg 的十倍，别不小心留着。

**不认识的命名会被忽略**，`check` 会列出来，不会默默什么都不做。

---

## 优先级

从高到低，先匹配上的赢：

| 优先级 | 来源 | 例子 |
| --- | --- | --- |
| 1 | `specialDays` 里指定某一年 | `"2026-01-01": newyear.png` |
| 2 | `specialDays` 里指定每年 | `"12-25": christmas.png` |
| 3 | 文件名是具体日期 | `2026-01-01.png` |
| 4 | 文件名是每年的月日 | `12-25.png` |
| 5 | `range` 算出来的周期槽位 | `3.png` |
| 6 | `default` | `default.png` |
| 7 | 都没有 | **不改动壁纸**，保持现状 |

实测效果（用的是 `examples/` 里那份配置）：

| 日期 | 星期 | 槽位 | 选中 | 走的哪条 |
| --- | --- | --- | --- | --- |
| 2026-01-05 | 一 | 1 | `1.png` | 周期 |
| 2026-01-11 | 日 | 7 | `7.png` | 周期 |
| 2026-01-01 | 四 | 4 | `2026-01-01.png` | 具体日期压过周期 |
| 2026-12-25 | 五 | 5 | `12-25.png` | 每年 |
| **2027**-01-01 | 五 | 5 | `5.png` | 具体年份**不会**泄漏到别的年份 |
| **2027**-12-25 | 六 | 6 | `12-25.png` | "每年"继续生效 |

`specialDays` 比同名的文件名日期优先级更高，因为它是你**在配置里显式写的**，而且可以指向任意路径（不用为了复用去重命名或复制文件）。

---

## 配置文件

位置：

- Windows：`%APPDATA%\AutoPaper\config.yaml`
- Linux / macOS：`~/.config/AutoPaper/config.yaml`

查找顺序：`--config` 指定的路径 → 当前目录下的 `config.yaml` → 上面那个用户配置目录。

```yaml
# 周期性壁纸的循环顺序。默认一周一轮，对应 wallpapers/ 里的 1 … 7。
range: [1, 2, 3, 4, 5, 6, 7]

# range 里第一个元素从哪天算起。默认 0001-01-01，那天正好是周一，
# 所以默认 1=周一 … 7=周日。改成任意一个星期天就变成周日=1。
rangeStartDay: "0001-01-01"

# fill（铺满，默认）/ fit（适应）/ stretch（拉伸）/ center（居中）/ tile（平铺）/ span（跨屏）
style: fill

# 指定某天用哪张图，路径相对 wallpapers/ 目录（写绝对路径也行）
specialDays:
  "2026-01-01": 2026-01-01.png
  "12-25": 12-25.png
```

### `range` 不只是"星期几"

它就是个循环队列，长度随便定：

```yaml
range: [1, 2, 3]              # 三天一轮
range: [1, 1, 2]              # 1 号连用两天，再换 2 号
range: [3, 2, 1]              # 换个顺序
range: [1, 2, 3, 4, 5]        # 五天一轮，周末不动（配合 default 或干脆不换）
```

想**周末固定用某一套图**？让循环长度是 7 的倍数，再配合 `specialDays` 或文件名的 `06-01` 之类写法。这个是刻意的取舍：AutoPaper 不打算做通用规则引擎，规则越少越好懂。

### 配置项写错会怎样

会直接报错，并且告诉你正确拼写和行号——不会静默忽略：

```
第 3 行：不认识的配置项 "rang"。
  可用的配置项：range、rangeStartDay、style、specialDays
```

---

## 命令

| 命令 | 作用 |
| --- | --- |
| `autopaper apply` | 应用今天的壁纸。计划任务调用的就是它 |
| `autopaper preview [日期]` | 看某天会用哪张图，**不改任何设置**。日期格式 `yyyy-MM-dd`，不写就是今天 |
| `autopaper check` | 体检：文件名、缺失的图、格式风险、拼错的配置项 |
| `autopaper init` | 生成 `config.yaml` 和壁纸目录。已存在时不覆盖，要覆盖加 `--force` |
| `autopaper install` | 注册计划任务（登录时 + 每天 00:05） |
| `autopaper uninstall` | 删掉计划任务 |
| `autopaper version` | 显示版本和各个路径 |
| `autopaper help` | 显示帮助 |

选项：

| 选项 | 说明 |
| --- | --- |
| `--config <路径>` | 指定配置文件 |
| `--dry-run` | `apply` 只打印会做什么，不真的换 |
| `-q`, `--quiet` | `apply` 只写日志。计划任务用这个，免得弹窗 |
| `-v`, `--verbose` | 输出更详细 |
| `--force` | `init` 覆盖已存在的配置 |

### `check` 会告诉你什么

```
$ autopaper check
配置文件   C:\Users\me\AppData\Roaming\AutoPaper\config.yaml
壁纸目录   C:\Users\me\AppData\Roaming\AutoPaper\wallpapers
循环       [1, 2, 3, 4, 5, 6, 7]，起点 0001-01-01（周一），长度 7 天
填充样式   Fill

周期性槽位：
  [ok]    槽位 1   → 1.png
  [缺失]  槽位 2   需要 2.<图片格式> 或 02.<图片格式>
  [ok]    槽位 3   → 3.png
  ...

兜底：
  [ok]    default → default.png

提醒：
  - 槽位 2 没有对应的图片，那些天会使用 default
  - 有 1 个文件不符合命名约定，不会被自动选中：monday.png
        约定是 default / yyyy-MM-dd / MM-dd / 数字（1、2、3…）加图片扩展名

检查通过，但有上面几条提醒。
```

它把问题分成两档，方便丢进 CI：

- **检查不通过（退出码 1）**：`specialDays` 指向的图片不存在、壁纸目录里一张图都没有
- **提醒（退出码 0）**：槽位缺图、没有 `default`、文件名不符合约定、扩展名重复、格式有风险

槽位缺图只是提醒而**不是**错误——你明确说了有 `default` 就用 `default`，那是正常状态，不该让检查失败。

---

## 常见问题

**今天该换的图没换？**

```powershell
autopaper preview          # 今天选中了哪张
autopaper check            # 有没有文件缺失或者命名写错
```

再看日志：`%APPDATA%\AutoPaper\autopaper.log`。

**开机没有自动换？**

```powershell
schtasks /Query /TN AutoPaper-Logon
schtasks /Query /TN AutoPaper-Daily
```

如果没有，重新 `autopaper install`。注意计划任务是**按用户**注册的，换用户登录要重新装。

**想立刻换一张试试？**

```powershell
autopaper apply --dry-run   # 先看会选哪张
autopaper apply             # 真的换
```

**壁纸设置成功但看起来没变？**

Windows 会把图片转码成 `TranscodedWallpaper`，遇到不支持的格式可能**静默失败**。用 `check` 看有没有格式提醒，稳妥起见用 `png` 或 `jpg`。

**没有管理员权限能用吗？**

能。改壁纸和注册用户级计划任务都不需要管理员权限。

---

## 从源码构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```bash
git clone https://github.com/lonely-4/autopaper.git
cd autopaper
dotnet build -c Release
dotnet test  -c Release
./scripts/smoke.sh            # 用 examples/ 跑一遍所有命令，不碰系统设置
```

日常调试：

```bash
dotnet run --project src/AutoPaper -- check --config examples/config.yaml
dotnet run --project src/AutoPaper -- preview 2026-01-01 --config examples/config.yaml
```

发布 Windows 单文件（**必须在 Windows 上执行，Native AOT 不支持跨操作系统编译**）：

```bash
# 自包含 + 单文件 + 裁剪，目标机器不需要装 .NET
dotnet publish src/AutoPaper -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=true -p:InvariantGlobalization=true \
  -o dist/selfcontained

# Native AOT，启动最快、体积最小，需要 MSVC 工具链
dotnet publish src/AutoPaper -c Release -r win-x64 --self-contained true \
  -p:PublishAot=true -p:InvariantGlobalization=true \
  -o dist/aot
```

### 在其他平台上

`preview` 和 `check` 在任何平台都能跑（它们只读文件系统）。只有 `apply` 和 `install` / `uninstall` 依赖 Windows API，在别的平台上会明确告诉你不可用。

---

## CI / CD

- **CI**（`.github/workflows/ci.yml`）：`ubuntu-latest` 和 `windows-latest` 各跑一遍单元测试，然后两个平台共用同一份 `scripts/smoke.sh` 跑 CLI 冒烟测试。另外有一个 Windows 任务实测 `bmp` / `png` / `jpg` / `gif` / `tif` 到底哪些格式能真的设成壁纸（`scripts/wallpaper-matrix.ps1`）。
- **CD**（`.github/workflows/release.yml`）：推 `v*` 标签时，在 `windows-latest` 上构建自包含和 AOT 两个版本，跑一遍确认 exe 能用，然后发到 GitHub Release。

发版：

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## 设计取舍

- **不做规则引擎。** 文件名就是规则。想加"每月第一个周一"这类玩法的话，这个工具不是为你准备的——那样迟早会变成一个只有作者看得懂的配置文件。
- **YAML 只用节点树解析，不用反射反序列化。** 为的是让裁剪和 AOT 都能用（YamlDotNet 的表示模型层零反射），顺便换来了精确到行号的报错。
- **`apply` 不会自动创建配置。** 后台任务静默地生成文件是坏事，找不到配置就直接报错让你先 `init`。
- **`DayOfWeek` 的坑。** .NET 里 `DayOfWeek.Sunday == 0`，直接强转会把"1=周一"搞错一天。这里用 `0001-01-01`（正好是周一）当默认锚点，避开硬编码星期几。
