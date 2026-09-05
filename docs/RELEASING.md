# Release guide / 发布指南

1. Update `VERSION` and `CHANGELOG.md`, add bilingual notes under `docs/releases/`, and update any version-specific download names in both READMEs.
2. Run `./build.ps1 -Test -UiSmoke -Package` on Windows. Inspect both language previews and verify that the package includes only the EXE, launcher and documentation.
3. Commit the version, code and documentation. Push `main` after confirming all local checks pass.
4. Create and push an annotated tag matching `VERSION`. Publish the files built from that commit:

```powershell
$releaseVersion = (Get-Content -LiteralPath VERSION -Raw).Trim()
git tag -a "v$releaseVersion" -m "Release v$releaseVersion"
git push origin "v$releaseVersion"
gh release create "v$releaseVersion" "dist/TheyAreBillionsSaveManager-v$releaseVersion-windows.zip" dist/TheyAreBillionsSaveManager.exe dist/SHA256SUMS.txt --verify-tag --title "v$releaseVersion" --notes-file "docs/releases/v$releaseVersion.md"
```

5. Check the public release page and asset hashes after upload. Do not include real game saves, snapshot folders, settings or local logs.

更新版本和双语说明 → 本机构建及测试 → 检查界面与包内容 → 确认测试通过 → 提交并推送 → 创建标签并发布 → 核对公开下载和校验值。

The local build produces the release assets; publishing a public Release is an explicit maintainer action.
