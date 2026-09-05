# They Are Billions 存档回退助手

[English](README.en.md) | 简体中文

一个支持中文和英文的 Windows 桌面工具，帮助你保留存档快照，并从游戏自动备份或历史快照恢复生存模式进度。

**[下载最新版本](https://github.com/nobleduck/TheyAreBillionsSaveManager/releases/latest)** · [直接下载 EXE](https://github.com/nobleduck/TheyAreBillionsSaveManager/releases/latest/download/TheyAreBillionsSaveManager.exe)

## 直接使用

在 Release 的 **Assets** 中下载 `TheyAreBillionsSaveManager-v0.2.1-windows.zip` 并解压。GitHub 自动提供的 **Source code** 是源码，需要自己构建。

双击项目根目录中的 **`Start.cmd`**，或者运行 **`dist/TheyAreBillionsSaveManager.exe`**。程序使用 Windows 10 / 11 自带的 .NET Framework，不需要 Python、Node.js 或额外安装包。

1. 选择要恢复的**自动备份**或**历史快照**，核对下方的存档名称和保存时间。
2. 正常保存并退出游戏，点击 **启动游戏并回退**。
3. 游戏启动后先停在主菜单。看到工具显示 **“现在可以点继续”**，再在当前游戏里点击继续，加载对应存档。

工具只有在游戏日志确认目标文件已经加载后，才显示 **“已确认游戏加载了恢复点”**。之后正常游玩即可；结束时正常保存退出，并等待 Steam 云同步完成。

**回退过程中不要提前点击继续，也不要在文件准备好后重新启动游戏。** 如果误点，工具会停止并保留原件，退出游戏后再操作即可。

## 语言

首次打开时跟随当前 Windows 显示语言：中文环境使用简体中文，其他语言使用英文。右上角 **语言 / Language** 可随时切换并记住选择，无需重启。回退或备份执行期间暂时禁用切换。

界面、回退状态、工具自身的错误提示以及历史快照原因均支持两种语言；存档名称和文件内容保持原样。活动记录保留产生时的语言，Windows 自身的错误和系统对话框由系统语言决定。旧版本的设置和中文快照原因仍然兼容。

## 为什么要先启动游戏

Steam 在启动前会检查存档。如果只在游戏关闭时移开主存档，Steam 可能把云端的较新文件重新下载回来，游戏仍然会读取较新的进度。

这个工具执行的顺序是：

```text
保留目标快照 → 通过 Steam 启动 → 确认本次启动的主菜单
→ 再次备份当前状态 → 安全放置目标配对 → 等待用户读档 → 核对实际加载日志
```

不会关闭 Steam 云同步，也不会修改 Steam 账户、设置或其他游戏的存档。工具不会主动上传文件；游戏正常退出后的云同步由 Steam 自己执行。

## 快照与回退时间

- **主存档**：当前磁盘上的进度。
- **自动备份**：游戏生成的 `_Backup.zxsav` / `_Backup.zxcheck` 配对。
- **手动备份**：点击按钮，保留选中存档名当前在磁盘上的完整配对。
- **自动保留**：工具开启且选项勾选时，每 15 秒检查变化；同样的内容不会重复生成快照。
- **历史快照**：以前被工具保留下来的版本，可以按原文件名恢复。

工具只能保留游戏已经写盘的保存点，不能让游戏每 15 秒自动保存，也不能凭空恢复过去的任意时刻。实际回退可能是 3 分钟、7 分钟或更久，以列表里的保存时间为准；这里的时间差是现实时间，不是游戏内天数。

首次开启自动保留时，会为现有的完整存档建立快照。后续只处理检测到变化的存档。快照不自动清理，长期使用可以通过“打开快照目录”查看磁盘占用。

## 数据保存在什么地方

| 内容 | 默认位置 |
| --- | --- |
| 游戏存档 | Windows 文档目录下的 `My Games\They Are Billions\Saves` |
| 历史快照 | `%LOCALAPPDATA%\TheyAreBillionsSaveManager\Snapshots` |
| 工具设置 | `%LOCALAPPDATA%\TheyAreBillionsSaveManager\settings.json` |
| 回退事务与被移开的原件 | 游戏 `Saves` 目录同级的 `TABSaveManager-Recovery` |

每份快照有 `snapshot.json`，记录原始文件名、修改时间、长度和 SHA-256。回退时会核对文件内容，始终保留 `.zxsav` 与 `.zxcheck` 的配对以及原始 `_Backup` 名称。

如果想恢复到回退之前，在历史快照页选择原因标为 **“回退前安全备份”** 的主存档，再执行同样的恢复流程。不要独自重命名或混搭配对文件。

目录中的 `.pending` 表示尚未完成的快照，不会被当作可用恢复点。若程序在文件操作期间意外中断，原件和 `recovery.json` 留在 `TABSaveManager-Recovery` 中；已完成的历史快照仍可使用。

## 状态提示

| 提示 | 含义与处理 |
| --- | --- |
| 游戏正在运行 | 可以保留快照；回退前先正常退出游戏。 |
| 暂时不要点击继续 | 工具正在等待新启动会话的主菜单。 |
| 现在可以点击继续 | 文件已准备好；直接在当前游戏里加载对应进度。 |
| 已确认游戏加载了恢复点 | 日志确认目标路径已完成加载。 |
| 提前点击了继续 | 本次停止，退出游戏后重试。 |
| 存档配对不完整 / 仍在保存 | 等待游戏完成保存，稍后重试。 |
| 较新存档重新出现 | 可能发生了云同步或外部修改；本次不宣称成功。 |
| 等待超时 | 启动等待限时 3 分钟；读档验证限时 10 分钟。不会因此强制关闭游戏。 |

当前支持 Steam 版的这套存档和日志格式。遇到无法识别的版本或日志时会停止，避免在未知状态下处理文件。链接目录、目录联接和链接文件暂不支持。

## 从源码构建

在 Windows PowerShell 中进入项目目录：

```powershell
.\build.ps1 -Test
```

生成 `dist/TheyAreBillionsSaveManager.exe`。构建直接调用 Windows 自带的 .NET Framework `csc.exe`，无需 NuGet 或网络下载。

界面检查：

```powershell
.\build.ps1 -Test -UiSmoke
```

核心测试只在随机临时目录里使用测试文件，不操作真实游戏存档。界面测试检查两种语言、正常和最小窗口大小、语言切换及设置持久化，输出 `build/ui-preview-*.png`。

生成便携压缩包：

```powershell
.\build.ps1 -Test -Package
```

产物为 `dist/TheyAreBillionsSaveManager-v0.2.1-windows.zip`、独立 EXE 和 `SHA256SUMS.txt`。版本号统一由根目录 `VERSION` 控制。

构建脚本会执行检查并生成下载包。发布步骤见 [发布指南](docs/RELEASING.md)。

## 项目结构

```text
src/
  SaveStore.cs             配对文件、快照、哈希与恢复事务
  GameLog.cs               解析游戏日志
  RollbackCoordinator.cs   启动、主菜单、文件准备、读档确认
  WindowsGameHost.cs       Steam 启动与只读进程/日志检查
  MainForm.cs              中英文 WinForms 界面
  Localization.cs          翻译与语言选择
  LocalizedErrors.cs       支持切换语言的错误提示
  Models.cs / AppSettings.cs / Program.cs
tests/                    核心回归测试与界面检查
docs/superpowers/          设计与实现计划
build.ps1                 零依赖构建
Start.cmd                 双击启动入口
```

`.gitignore` 已排除真实存档、设置、快照、构建目录和可执行文件。下载包通过 GitHub Releases 分发。本项目尚未指定开源许可证。
