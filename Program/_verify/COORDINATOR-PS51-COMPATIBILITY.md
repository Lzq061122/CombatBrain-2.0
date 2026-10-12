# 共享协调入口 PS 5.1 编码修复

日期：2026-10-12。范围仅为共享 `E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1` 的编码兼容，不是 P1 或 OBS 阶段验收。

## 已复现及修复

- 用 `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`（5.1.26100.9444，默认代码页 936）对实际文件 ParseFile，修复前报 5 个解析错误，包括第 18 行 TerminatorExpectedAtEndOfString。
- 文件原为 UTF-8 无 BOM。只在原始字节前添加 `EF BB BF`；从 1428 B 变为 1431 B，去掉这 3 字节后与旧文件逐字节相同，未改文本、逻辑、换行、中文消息或锁规则。
- 修复前 SHA256：`A1C2B49FFFFE3E50A04A188B64750AD916F18C1081F6DF203FEB16988A013E76`。
- 修复后 SHA256：`11BE056DC824539E5D501881C8FAE1277FF0E2DFBFA8A572E173E95CDC3A5A8D`。
- 修改时以共享锁独占句柄保护这次维护，未写其元数据；未操作三个观测开发工作树。该共享脚本位于三仓库工作树之外，主线本次 Git 提交只保存本记录，不冒充已将共享脚本纳入版本控制。

## PS 5.1 验证

- 实际协调入口、主线 `run-p1-exclusive.ps1`、`_p1-exclusive-stage.ps1` 的 ParseFile 错误均为 0。
- 将修复后的入口及原有无游戏测试辅助文件复制到临时目录，入口副本摘要与实际文件一致。只把临时测试驱动的 shell 固定为 Windows PowerShell 5.1；原 `verify-lock.py` 未改。运行 exit 0，首次执行、占用检测、第二个并发被拒、异常报告、异常后重试、残留文件下已释放状态检测六项均 true，`game_started=false`。
- 临时测试目录：`C:\Users\ADMINI~1\AppData\Local\Temp\cb2-ps51-coordinator-1g30jihe`。测试使用独立临时锁，没有覆盖实际共享锁元数据。
- 主线 P/N 包装入口各自经 PS 5.1 `-WhatIf` 执行，均为 RESULT: WHATIF、exit 0。对前后快照复算，实际共享锁、主线构建 DLL、两套已部署 DLL、P1/资格两个开关文件均无变化；结束时 Bannerlord 进程为 0。

本聊天可找到 Codex 自带 pwsh，不能据此推定另一个开发 Harness 的 PATH 相同；本次修复与验证不依赖其使用 pwsh。兼容性阻塞已解除，但没有启动游戏或验证 P1 两处修复的 Host 实际执行。开发方源码、测试和包装脚本的未提交改动保留原样；后续按既定共享入口串行复验 P/N，不能因本记录自动登记 P1 通过。
