# win-tendedero

简体中文的 Windows 晾截图工具：把截图挂在屏幕顶部，方便复制、拖放、标注和临时收纳。

**[下载 Windows x64 便携版](https://github.com/beiming183-cloud/win-tendedero-cn/releases/latest)**

解压后双击 `win-tendedero.exe`，无需额外安装 .NET 运行库。程序位于任务栏通知区域，右键图标打开中文菜单和设置。

## 使用

- `Win+Shift+S` 截图，自动挂到晾衣绳。
- 鼠标停在屏幕顶部，或按默认快捷键 `Ctrl+Alt+T`，显示或收起晾衣绳。
- 单击截图复制，长按打开标注，双击打开原图；拖到应用中分享副本。
- 可配置监视文件夹、便签、夹子样式、绳子颜色、主题和快捷键。
- 默认简体中文，也可选择 English、阿拉伯语或跟随 Windows。

## 调整唤出和快捷键

右键托盘图标 → **设置**：

- **唤出等待**：0–1500 毫秒，默认 200 毫秒。越短越灵敏，0 表示立即唤出。
- **顶部触发范围**：2–24 像素，默认 8 像素，随 Windows 显示缩放调整。沿顶部移动不再重置等待；点击、拖拽和全屏应用仍会抑制唤出。
- **快捷键**：点“更改”，直接按下组合键。Esc 或“取消”放弃录入；已被占用的组合键会显示提示，并保留原来可用的快捷键。

这些更改会立即生效并保存。旧版未提供调节界面时的默认半秒等待会升级为 200 毫秒；自行设置过的其他等待值会保留。即使没有截图，也能从顶部唤出。

支持 Windows 10 1809 及更高版本、Windows 11，当前发布 Windows x64 版本。

## 设置与旧版数据

新设置位于 `%LOCALAPPDATA%\win-tendedero\settings.json`。

首次运行且新设置不存在时，会从 `%LOCALAPPDATA%\Snapline` 或 `%LOCALAPPDATA%\Clothesline` 复制已有设置和应用数据，并切换到简体中文。旧目录和截图原文件保留，已有截图路径不改写；新目录已有设置时不会覆盖。

## 从源码构建

需要 Windows 和 .NET 8 SDK。在 PowerShell 中运行：

```powershell
.\build.ps1
```

完成品位于 `artifacts`，包括自包含的 `win-tendedero.exe` 和便携 ZIP。也可以显式指定 SDK：

```powershell
.\build.ps1 -DotnetPath 'D:\Codex\Tools\dotnet-sdk\dotnet.exe'
```

GitHub Actions 会在推送后编译并保留可下载的构建产物。正式发布包在 Releases 中提供。

Windows 本地回归检查：

```powershell
dotnet run --project tests/WinTendedero.Tests -c Release
```

检查使用独立临时设置目录，不修改当前使用中的配置。

## 来源与许可

此项目基于 [mohabujubara/snapline](https://github.com/mohabujubara/snapline) 的固定提交 `6a1f2425aea144f692787033188b0d480dd09bf4`，应用中文补丁并改为独立的 **win-tendedero** 名称和图标。上游源自 macOS 的 [Tendedero](https://github.com/alejandrobujan/tendedero)。

这是 Windows 衍生版，不是 macOS 原版发行包。代码采用 MIT 许可；原作者署名及字体许可见 [LICENSE](LICENSE) 和 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
