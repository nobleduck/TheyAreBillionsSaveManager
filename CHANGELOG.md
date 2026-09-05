# Changelog / 更新记录

## 0.2.1 — 2026-09-05

- Fix idle button flicker caused by the once-per-second game-state check temporarily disabling controls.
- Keep operation locks during automatic snapshots and restores; game start/exit still changes restore availability.
- Add a UI regression check for stable controls across repeated idle polls and real game-state transitions.

- 修复每秒检查游戏状态时反复禁用、启用按钮造成的闪烁。
- 保留自动备份和回退期间的操作保护，游戏启动、退出时仍正确更新回退按钮。
- 增加空闲轮询与游戏状态切换的界面回归检查。

## 0.2.0 — 2026-09-05

- English and Simplified Chinese UI, current Windows display-language detection, instant language switching and saved preferences.
- Localized restore status, application errors and legacy snapshot reasons; filenames and snapshot data remain unchanged.
- Bilingual documentation, versioned portable ZIP, standalone EXE, SHA-256 checksums and a repeatable Windows build script.
- Regression coverage for language persistence, failed-status switching, Unicode filenames and both UI layouts.

- 增加中英文界面、系统显示语言识别、即时切换和语言记忆。
- 翻译回退状态、工具错误和旧快照原因，保留原始文件名及数据。
- 提供双语文档、便携包、独立 EXE、校验文件和 Windows 构建脚本。
- 增加语言持久化、失败状态切换、中文文件名及双语界面的回归检查。

## 0.1.0 — 2026-09-05

- Initial local version: paired snapshots, automatic backup retention, Steam-aware restore preparation and log-based load verification.
- 首个本地版本：配对快照、自动备份保留、处理 Steam 启动同步的回退流程和读档日志验证。
