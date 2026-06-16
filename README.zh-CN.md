# Codex 额度液态小组件

这是一个 Windows 桌面悬浮小组件，用简约的进度面板实时显示 Codex 的额度使用情况和重置时间。拖到屏幕边缘后会平滑吸附，并露出一条绿色液态小尾巴，小尾巴会显示 5 小时额度进度。

[English README](README.md)

## 功能

- 通过本机 Codex `app-server` 实时读取额度。
- 显示 5 小时额度、每周额度、重置时间和倒计时。
- 无边框圆角 WinForms 窗口，支持平滑拖动和吸附动画。
- 支持吸附到显示器上、下、左、右边缘。
- 吸附后露出绿色液态小尾巴，显示 5 小时额度进度。
- 右键菜单支持查看详情、刷新和退出。
- Windows 托盘图标支持显示、隐藏、刷新和退出。
- 自动保存窗口位置和吸附状态。

## 环境要求

- Windows 10 或更新版本。
- PowerShell 5.1，用于通过 `Add-Type` 构建程序。
- Windows 自带的 .NET Framework WinForms 支持。
- Node.js 已加入 `PATH`，或通过 `NODE_EXE` 指定完整 `node.exe` 路径。
- 本机已安装 Codex 桌面版或 CLI。

额度轮询脚本会自动从常见 OpenAI Codex 安装目录查找 `codex.exe`。如果查找失败，可以手动设置：

```powershell
$env:CODEX_EXE = "C:\Path\To\codex.exe"
```

## 快速开始

构建可执行文件：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

运行软件：

```powershell
.\CodexQuota.exe
```

也可以双击：

```text
StartQuotaLiquidOrb.bat
```

仓库中也包含预编译的 `CodexQuota.exe`，方便直接下载测试。如果 exe 不存在，双击这个 bat 时会在首次启动前自动构建 exe，然后再打开软件。

## 使用方式

- 按住鼠标左键拖动小组件。
- 拖到显示器边缘附近松手会自动吸附。
- 点击绿色小尾巴会弹出完整面板。
- 双击面板查看额度详情。
- 右键面板或托盘图标可以刷新、隐藏或退出。

## 文件说明

- `CodexQuotaApp.cs` - WinForms 桌面小组件源码。
- `CodexQuota.exe` - 预编译 Windows 可执行文件，方便直接测试。
- `quota-poller.mjs` - 常驻 Node.js 额度轮询脚本。
- `quota-probe.mjs` - 一次性额度探测脚本，方便调试。
- `build.ps1` - 构建 `CodexQuota.exe`。
- `StartQuotaLiquidOrb.bat` - 启动构建后的程序。

以下运行时文件不会提交到 git：

- `quota-live.json`
- `orb-window.json`

## 调试

运行一次性额度探测：

```powershell
node .\quota-probe.mjs .\quota-live.json
Get-Content .\quota-live.json
```

如果 Node 或 Codex 安装在自定义位置：

```powershell
$env:NODE_EXE = "C:\Path\To\node.exe"
$env:CODEX_EXE = "C:\Path\To\codex.exe"
.\CodexQuota.exe
```

## 说明

这个项目只和本机 Codex app-server 通信，不会要求或保存 GitHub 密码、OpenAI 密码或 API key。
