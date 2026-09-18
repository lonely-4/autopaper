#!/usr/bin/env bash
#
# 用 examples/ 里的示例配置把所有命令跑一遍，验证 CLI 的真实行为。
# 完全不碰系统设置（apply 一律带 --dry-run），所以 Linux 和 Windows 上都能跑，
# CI 里两个平台共用这一份脚本。
#
#   dotnet build -c Release
#   ./scripts/smoke.sh

set -uo pipefail

cd "$(dirname "$0")/.."

DLL="src/AutoPaper/bin/Release/net10.0/autopaper.dll"
CFG="examples/config.yaml"
APP=(dotnet "$DLL")

if [[ ! -f "$DLL" ]]; then
  echo "找不到 $DLL" >&2
  echo "先跑：dotnet build -c Release" >&2
  exit 1
fi

pass=0
fail=0

# check <说明> <期望退出码> <期望输出里的子串，-表示不检查> <命令...>
check() {
  local desc=$1 expected_code=$2 needle=$3
  shift 3

  local out code
  out=$("$@" 2>&1)
  code=$?

  if [[ $code -ne $expected_code ]]; then
    fail=$((fail + 1))
    echo "FAIL  $desc"
    echo "      期望退出码 $expected_code，实际 $code"
    printf '%s\n' "$out" | sed 's/^/      | /'
    return
  fi

  if [[ "$needle" != "-" ]] && ! grep -qF -- "$needle" <<<"$out"; then
    fail=$((fail + 1))
    echo "FAIL  $desc"
    echo "      输出里没有出现：$needle"
    printf '%s\n' "$out" | sed 's/^/      | /'
    return
  fi

  pass=$((pass + 1))
  echo "ok    $desc"
}

# check_empty <说明> <命令...>：要求命令没有任何输出（--quiet 的行为）
check_empty() {
  local desc=$1
  shift

  local out
  out=$("$@" 2>&1)

  if [[ -n "$out" ]]; then
    fail=$((fail + 1))
    echo "FAIL  $desc"
    printf '%s\n' "$out" | sed 's/^/      | /'
    return
  fi

  pass=$((pass + 1))
  echo "ok    $desc"
}

echo "== 示例配置应该完全干净 =="
check "check 通过且没有任何提醒" 0 "检查通过。" "${APP[@]}" check --config "$CFG"

echo
echo "== init 生成的配置要能直接用（新用户走的第一条路） =="
# 这里曾经挂过：生成的模板里 specialDays 后面只有注释，YAML 解析成 null 导致报错。
init_dir=$(mktemp -d)
check "init 生成配置和目录" 0 "已生成配置" "${APP[@]}" init --config "$init_dir/config.yaml"
cp examples/wallpapers/*.png "$init_dir/wallpapers/"
check "init 完立刻 check 就通过" 0 "检查通过" "${APP[@]}" check --config "$init_dir/config.yaml"
check "init 的配置能正常挑图" 0 "1.png" "${APP[@]}" preview 2026-01-05 --config "$init_dir/config.yaml"

echo
echo "== 周期性：1=周一 … 7=周日 =="
check "2026-01-05 周一 -> 1.png"  0 "1.png" "${APP[@]}" preview 2026-01-05 --config "$CFG"
check "2026-01-06 周二 -> 2.png"  0 "2.png" "${APP[@]}" preview 2026-01-06 --config "$CFG"
check "2026-01-11 周日 -> 7.png"  0 "7.png" "${APP[@]}" preview 2026-01-11 --config "$CFG"
check "下一周同样的槽位"           0 "1.png" "${APP[@]}" preview 2026-01-12 --config "$CFG"

echo
echo "== 优先级：指定日期 > 周期 =="
check "2026-01-01 用指定日期那张"   0 "2026-01-01.png" "${APP[@]}" preview 2026-01-01 --config "$CFG"
check "2026-01-01 来源是 specialDays" 0 "specialDays" "${APP[@]}" preview 2026-01-01 --config "$CFG"
check "2026-12-25 用 12-25 那张"    0 "12-25.png" "${APP[@]}" preview 2026-12-25 --config "$CFG"

echo
echo "== 具体年份不该泄漏到别的年份 =="
# 2027-01-01 是周五 -> 槽位 5，应该用 5.png 而不是 2026-01-01.png
check "2027-01-01 落回周期槽位" 0 "5.png" "${APP[@]}" preview 2027-01-01 --config "$CFG"
check "2027-01-01 不用去年的图" 0 "周期槽位" "${APP[@]}" preview 2027-01-01 --config "$CFG"
# 12-25 是"每年"，所以 2027 年照样命中
check "2027-12-25 仍然命中每年那张" 0 "12-25.png" "${APP[@]}" preview 2027-12-25 --config "$CFG"

echo
echo "== apply 只做试运行，绝不改壁纸 =="
check "apply --dry-run 成功" 0 "试运行" "${APP[@]}" apply --dry-run --config "$CFG"
check_empty "apply --dry-run --quiet 完全静默" "${APP[@]}" apply --dry-run --quiet --config "$CFG"

echo
echo "== 其他命令 =="
check "version 打印版本"  0 "autopaper" "${APP[@]}" version
check "help 打印用法"     0 "用法"      "${APP[@]}" help

echo
echo "== 配置目录必须是 <用户目录>/.config/AutoPaper（Windows 上也不例外） =="
# 这是明确要求过的：不用 %APPDATA%。用 grep 而不是 check，因为要断言"不该出现什么"。
version_out=$("${APP[@]}" version 2>&1)

if grep -qE 'AppData|Roaming' <<<"$version_out"; then
  fail=$((fail + 1))
  echo "FAIL  配置目录不该落在 %APPDATA% 下"
  printf '%s\n' "$version_out" | sed 's/^/      | /'
else
  pass=$((pass + 1))
  echo "ok    没有用 %APPDATA%"
fi

# 分隔符在 Windows 上是反斜杠，所以只断言目录结构不写死斜杠
if grep -qF '.config' <<<"$version_out" && grep -qF 'AutoPaper' <<<"$version_out"; then
  pass=$((pass + 1))
  echo "ok    配置目录落在 .config/AutoPaper 下"
else
  fail=$((fail + 1))
  echo "FAIL  配置目录不在 .config/AutoPaper 下"
  printf '%s\n' "$version_out" | sed 's/^/      | /'
fi

echo
echo "== 出错时必须报错并返回 1 =="
check "配置文件不存在" 1 "找不到配置文件" "${APP[@]}" check --config /definitely/not/here.yaml
check "日期格式写错"   1 "yyyy-MM-dd"    "${APP[@]}" preview 2026/01/05 --config "$CFG"

bad_cfg=$(mktemp)
printf 'range: [1, 2\n' > "$bad_cfg"
check "YAML 语法错误" 1 "不是合法的 YAML" "${APP[@]}" check --config "$bad_cfg"

typo_cfg=$(mktemp)
printf 'rang: [1, 2, 3]\n' > "$typo_cfg"
check "配置项拼错字" 1 "range" "${APP[@]}" check --config "$typo_cfg"

missing_cfg=$(mktemp)
printf 'specialDays:\n  "2099-01-01": nope.png\n' > "$missing_cfg"
check "specialDays 指向不存在的图" 1 "不存在" "${APP[@]}" check --config "$missing_cfg"

echo
echo "------------------------------------------------------------"
if [[ $fail -eq 0 ]]; then
  echo "全部通过：$pass 项"
  exit 0
fi

echo "通过 $pass 项，失败 $fail 项"
exit 1
