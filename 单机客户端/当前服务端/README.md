# 单机服务端成品

双击上一级的 `a开始游戏.exe`，使用服务端窗口中的「单机登录」页进入游戏。无需独立登录框，也不需要运行 CMD 或 PowerShell。移动游戏时复制整个「单机客户端」文件夹。

| 系统 | 自动选择的程序 |
| --- | --- |
| Windows 10 / 11，64 位 | `launcher` 与 `server-merged/bin`，自带 .NET 8 |
| Windows 7 SP1，64 位 | `launcher-win7` 与 `server-merged/bin-win7`，自带 .NET 6.0.36 和 Universal CRT |

Win7 需要 SP1 及 KB3063858（或已提供 AddDllDirectory 的 KB2533623）系统更新。缺少这些系统组件时入口会提示；随包运行库不能替代操作系统补丁。Win7 兼容包未在 Win7 实机上验证；.NET 6 已停止维护。此成品不支持 32 位 Windows。

两套程序共享 `server-merged/data/game.db` 的账号、角色与 `server-merged/data/native` 的地宫存档，只能同时运行一套。停止服务使用管理窗口中的「停止服务端」。正常启动不会重建已删除的原版预留账号。

`config/profile.ini` 保留原生地宫兼容参数。`launchsettings.json` 使用相对路径，移动整个游戏目录无需修改。日志为 `launcher.log`、`server-merged/server.log` 和 `server-merged/server-error.log`。

成品包含当前运行所需文件。源码与构建工具在工程根目录的「当前服务端」和「登录器源码」，游玩时不需要复制它们。
