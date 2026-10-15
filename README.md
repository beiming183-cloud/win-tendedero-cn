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

如果快捷键被其他程序占用，可用托盘图标打开，在设置中更换组合键。

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

## 来源与许可

此项目基于 [mohabujubara/snapline](https://github.com/mohabujubara/snapline) 的固定提交 `6a1f2425aea144f692787033188b0d480dd09bf4`，应用中文补丁并改为独立的 **win-tendedero** 名称和图标。上游源自 macOS 的 [Tendedero](https://github.com/alejandrobujan/tendedero)。

这是 Windows 衍生版，不是 macOS 原版发行包。代码采用 MIT 许可；原作者署名及字体许可见 [LICENSE](LICENSE) 和 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
