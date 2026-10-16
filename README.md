# win-tendedero

win-tendedero 是一款参考 [Tendedero](https://github.com/alejandrobujan/tendedero) 和 [Snapline](https://github.com/mohabujubara/snapline) 开发的 Windows 截图暂存与整理应用。把截图挂在屏幕顶部，随手复制、拖放、标注，用完再收起。

**[下载 Windows x64 便携版](https://github.com/beiming183-cloud/win-tendedero-cn/releases/latest)**

解压后双击 `win-tendedero.exe`，无需额外安装 .NET 运行库。程序位于任务栏通知区域，右键图标打开中文菜单和设置。

## 使用

- `Win+Shift+S` 截图，自动挂到晾衣绳。
- 鼠标停在屏幕顶部，或按默认快捷键 `Ctrl+Alt+T`，显示或收起晾衣绳。
- 单击截图复制，长按打开标注，双击打开原图；拖到应用中分享副本。
- 左侧文件夹打开全部截图。重启清空绳子，截图保留；取下卡片不会删除图片。
- 卡片默认放大至 140%，可调至 100–200%；绳上默认保留 24 张，可设 4–64 张，超出一屏用箭头或滚轮翻页。
- 设置 → 外观提供晴空玻璃、暖纸原木、冷调金属、蜜桃软糖、薄荷奶油五套搭配，也可分别选卡片材质、绳子材质、夹子和自选颜色。
- **Q 弹程度**可调 0–100。沿绳拖动时立即跟随鼠标，松手回弹；明显向外拉才进入拖放分享。拉整条绳子到屏幕边缘也会轻轻回弹。
- 新截图默认使用轻盈回弹入场，也可在设置中选择从截图位置飞入。
- 可配置监视文件夹、便签、夹子样式、绳子颜色、主题和快捷键。
- 默认简体中文，也可选择 English、阿拉伯语或跟随 Windows。

## 调整唤出和快捷键

右键托盘图标 → **设置**：

- **唤出等待**：0–1500 毫秒，默认 200 毫秒。越短越灵敏，0 表示立即唤出。
- **顶部触发范围**：2–24 像素，默认 8 像素，随 Windows 显示缩放调整。沿顶部移动不再重置等待；点击、拖拽和全屏应用仍会抑制唤出。
- **快捷键**：点“更改”，直接按下组合键。Esc 或“取消”放弃录入；已被占用的组合键会显示提示，并保留原来可用的快捷键。

这些更改会立即生效并保存。旧版未提供调节界面时的默认半秒等待会升级为 200 毫秒；自行设置过的其他等待值会保留。即使没有截图，也能从顶部唤出。

## 截图保存与显示

设置中可以更改截图保存位置。新用户默认保存到“图片\\win-tendedero”，已有自定义目录继续使用。更改位置时会复制当前目录内的截图，保留原文件和同名文件；复制失败则继续使用原位置。

截图原文件保存在文件夹中，绳子只保留本次运行的内容。来自监视文件夹的截图会复制到保存目录，不移动源文件。屏幕只创建当前页的卡片，缩略图仅缓存当前页和相邻页，翻回其他页时在后台重新加载；收起后停止非必要摆动。只有明确选择“移到回收站”才删除图片。

目录监听在启动时建立一次文件列表，之后直接处理新增、修改、重命名和删除通知，不再每三秒扫描整个目录。监听失败或通知缓冲区溢出时才重新建立监听并补扫；恢复失败时每十五秒重试。

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

## 许可

win-tendedero 采用 MIT 许可。完整许可与第三方声明见 [LICENSE](LICENSE) 和 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
