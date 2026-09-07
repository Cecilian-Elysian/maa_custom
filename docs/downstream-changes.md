# 下游改动文件清单

本仓库相对上游 `MaaAssistantArknights/MaaAssistantArknights` 的所有改动文件清单，由 `tools/gen-downstream-changes.py` 自动从 [`LOG.md`](../LOG.md) 的实施完成表格提取。

## 用法

在修改任何文件 **之前** 先查阅此清单：

- [OK] **不在清单中** = 上游原装代码，改动需谨慎（可能破坏上游兼容性）
- [TGT] **在清单中** = 本仓库改动过，确认你的改动是否与已有下游逻辑冲突
- [HOT] **高敏感**（被改 >= 3 次）= 多轮 feat/fix 反复动过，改动前优先读相关 LOG 段落

## 维护

每次 feat/fix 合并后，重跑脚本刷新：

```
py tools/gen-downstream-changes.py
```

共扫描 474 个表格行，聚合出 65 个唯一源文件路径。

## 仓库根（2 个文件）

### [HOT] `.gitignore` (x6)

| 操作 | 说明 |
|------|------|
| 鍒犻櫎 | 绉婚櫎 `LOG.md`銆乣AGENTS.md` 蹇界暐瑙勫垯 |
| 淇敼 | 娉ㄩ噴鏀逛负 `# Feature/fix working documents (local only, never committed)` |
| 淇敼 | (a) 杩藉姞 `/installer-build.log`锛?.6 MB 涓存椂鏃ュ織涓嶅叆浠擄級锛?b) 淇绗?510 琛岃鍒?`DependencySetup_渚濊禆搴撳畨瑁?bat` 璇激 `tools/`锛屾敼涓?`install/DependencySetup_*.bat`锛堝彧灞忚斀 install/ 鍓湰锛? |
| 淇敼 | 鏈熬杩藉姞 `/installer/`锛堜骇鐗╀笉鍏ヤ粨锛夛紝涓?`install/` 涓€鑷翠笉姹℃煋 git |
| 淇敼 | 杩藉姞杩愯鏃剁紦瀛樺拷鐣ヨ鍒欙紱杩藉姞 `.crush/` / `.claude/` / `.cursor/` 瑙勫垯锛涜拷鍔?`LOG.md` / `AGENTS.md` 蹇界暐 |
| 保留 fork + 接上游 | fork `.crush`/`.claude`/`.cursor` 规则 + 上游 `.claude/*` |

### [TGT] `VERSION` (x2)

| 操作 | 说明 |
|------|------|
| git rm | 浠?release 鑴氭湰璇诲彇锛屼笉鍐嶉渶瑕? |
| 鏂板缓 | 鍐呭 `v6.14.0-fork.20260714`锛屼綔涓?`MAA_HASH_VERSION` 鍜?zip 鏂囦欢鍚嶅崟涓€鏉ユ簮 |

## `.github/`（1 个文件）

### [TGT] `.github/workflows/release-fork.yml` 

| 操作 | 说明 |
|------|------|
| git rm | fork 鐨?GitHub CI锛屾湰鍦拌繍琛屼笉闇€瑕? |

## `docs/`（7 个文件）

### [TGT] `docs/5 语/protocol/integration.md` 

| 操作 | 说明 |
|------|------|
| 双轨字段 | fork `expedite_min_level` 扩展段 + 上游 `expedite`/`expedite_times` |

### [HOT] `docs/downstream-changes.md` (x13)

| 操作 | 说明 |
|------|------|
| 鏂板缓 | 棣栨杩愯浜х墿锛?6 涓敮涓€婧愭枃浠讹紝瑕嗙洊 220 琛?LOG.md 琛ㄦ牸銆備粨搴撴牴锛坄.gitignore`/`VERSION`锛? `.github/` + `docs/` + `resource/`锛坄tasks.json` [HOT]锛? `src/` 26 鏂囦欢锛坄TaskQueueViewModel.cs` [HOT] 23 娆★級+ `tools/` 5 鏂囦欢 |
| `py tools/gen-downstream-changes.py` | 自动刷新清单（36 → 45 文件，[HOT] 阈值更新） |
| 查阅 | 确认 `RecruitHistoryService.cs` 已被 v6 改动过 |
| `py tools/gen-downstream-changes.py` | 自动刷新清单 |
| 查阅 | 确认 `[HOT] AutoRecruitTask.cpp (x15)`、`[HOT] AutoRecruitTask.h (x4)`、`[HOT] resource/tasks/tasks.json (x6)` |
| `py tools/gen-downstream-changes.py` | 自动刷新清单（45 个文件，扫描自 326 LOG.md 表格行） |
| 查阅 | 确认 `AccountSwitchTask.h/.cpp` / `TaskQueueViewModel.cs` / `StartUpSettingsUserControlModel.cs` 已被多次改动 |
| `py tools/gen-downstream-changes.py` | 自动刷新清单（50 → 51 文件） |
| 查阅 | 确认 `RecruitScreenshotMonitor.{h,cpp}` 是 fork 私有（882d9b64bf 引入） |
| `py tools/gen-downstream-changes.py` | 自动刷新清单（51 → 51 文件，352 LOG.md 表格行） |
| `py tools/gen-downstream-changes.py` | 自动刷新清单（51 文件，352 LOG.md 表格行） |
| 重生成 | `py tools/gen-downstream-changes.py`：366→392 表格行、52→58 文件（commit `16a40b0442`） |
| 自动 | gen-downstream-changes.py 重生成 |

### [TGT] `docs/en-us/protocol/integration.md` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | 浜旇鍚屾锛氳嫳鏂? |
| 同上（英文） | 同上 |

### [TGT] `docs/ja-jp/protocol/integration.md` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | 浜旇鍚屾锛氭棩鏂? |
| 同上（日文） | 同上 |

### [TGT] `docs/ko-kr/protocol/integration.md` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | 浜旇鍚屾锛氶煩鏂? |
| 同上（韩文） | 同上 |

### [HOT] `docs/zh-cn/protocol/integration.md` (x3)

| 操作 | 说明 |
|------|------|
| 淇敼 | +1 瀛楁璇存槑 `expedite_min_level`,鍚?0/4/5/6 璇箟 |
| 淇敼 | `::: field name="expedite_min_level"` 瀛楁鍧楋紝`0 = 涓嶉檺`锛宍4/5/6 = 浠呭搴旀槦绾у強浠ヤ笂鍔犳€ |
| 新增 `auto_upgrade_3star_with_4star` 字段说明段 | 协议文档 |

### [TGT] `docs/zh-tw/protocol/integration.md` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | 浜旇鍚屾锛氱箒涓? |
| 同上（繁体） | 同上 |

## `resource/`（6 个文件）

### [TGT] `resource/minitouch/x86/minitouch` 

| 操作 | 说明 |
|------|------|
| 补图 | 34260 B，fork Initial commit 起缺失；`git checkout master --` 恢复（commit `bc3f4a2a2a`） |

### [HOT] `resource/tasks/tasks.json` (x8)

| 操作 | 说明 |
|------|------|
| 淇敼 | `LoginOther.next` 杩藉姞 `"AccountManagerPageConfirm"`; 鏂板 `AccountManagerPageConfirm` task锛坄baseTask: AccountManagerListAccount`, `action: DoNothing`锛? |
| 淇敼 | `AccountManagerOfficial` 涓?`AccountManagerBili` 鐨?`text` 浠?`["鐧诲綍璁板綍"]` 鈫?`["鐧诲綍璁板綍", "涓婃鐧诲綍"]`锛汥oc 鍚屾鏇存柊 |
| 淇敼 | `SwitchAccount@StartToWakeUp.next` 杩藉姞 `SwitchAccount@StartToWakeUpOCR` 鍏滃簳锛涙柊澧?`SwitchAccount@StartToWakeUpOCR` OCR 浠诲姟 |
| cherry-pick from `784d9005f6` | `AccountManagerOfficial` 鐢?`{"roi":[570,165,140,80]}` 琛ュ叏涓?`{"Doc":"...","algorithm":"OcrDetect","text":["鐧诲綍璁板綍"],"roi":[237,50,771,242]}`锛堜笌 `AccountManagerBili` 瀵归綈锛? |
| 寰呬慨鏀? | `AccountManagerOfficial` 琛?OcrDetect 璇嗗埆銆岀櫥褰曡褰曘€? |
| 淇敼 | `AccountManagerOfficial` 鐢?`{"roi":[570,165,140,80]}` 琛ュ叏涓?`{"Doc":"瀹樻柟鏈嶈处鍙峰垏鎹㈢晫闈㈣瘑鍒紝涓?B 鏈嶇粺涓€ OCR銆岀櫥褰曡褰曘€?,"algorithm":"OcrDetect","text":["鐧诲綍璁板綍"],"roi":[237,50,771,242]}`锛堜笌 B 鏈?`AccountManagerBili` 瀵归綈锛? |
| 恢复 master | `git checkout master --` 账号切换区块：LoginOther.next 移除 AccountManagerPageConfirm，删除该任务定义，AccountManagerOfficial 还原为纯 roi 模板匹配（整文件与 master 仅此区块差异，已验 0 diff） |
| 修改 ROI（+7 -7） | DepotAllTab `[452,0,281,134]`→`[450,0,300,138]`；DepotMaterialTab `[1026,0,254,138]`→`[945,0,300,138]`；DepotMaterialTabClicked `[1026,0,254,138]`→`[945,0,300,138]`；与上游 `da5ccfe4ed` diff 逐值一致；JSON 校验通过（941 tasks） |

### [TGT] `resource/template/Depot/DepotAllTab.png` 

| 操作 | 说明 |
|------|------|
| 上游重截替换 | 2116 B → 2061 B，181x34 → 77x34（取上游 `30ff4de669` 优化终版） |

### [TGT] `resource/template/Depot/DepotMaterialTab.png` 

| 操作 | 说明 |
|------|------|
| 上游重截替换 | 6366 B → 4171 B，170x38 → 131x33（同上） |

### [TGT] `resource/template/Depot/DepotMaterialTabClicked.png` 

| 操作 | 说明 |
|------|------|
| 上游重截替换 | 1482 B → 1636 B，165x40 → 120x34（同上） |

### [TGT] `resource/template/WakeUp/AccountManager/AccountManagerPageConfirm.png` 

| 操作 | 说明 |
|------|------|
| 删除 | fork 独有占位模板（AccountManagerListAccount.png 复制品），master 无此文件，已验模板目录与 master 对齐 |

## `src/`（42 个文件）

### [TGT] `src/MaaCore/Assistant.cpp` 

| 操作 | 说明 |
|------|------|
| 淇敼 | `AllTasksCompleted` 鍚庣珛鍗宠 `m_thread_idle=true`锛屼慨澶嶇浜岃疆 `AsstStart` 鍥犵珵鎬佽繑鍥?false 鐨?bug |

### [HOT] `src/MaaCore/Task/Infrast/InfrastReceptionTask.cpp` (x3)

| 操作 | 说明 |
|------|------|
| 接上游 + cherry-pick | 上游已含 No1..No7（#16054）；保留 fork `vacancy_cnt==0` 早返回 |
| 修改 | `proc_clue_vacancy` 控制流改造 — `vacancy_cnt==0` 提前 `return true`；`confirm_task != nullptr` 单独判断；引入 `click_performed` 仅真实点击时 `return true`；OCR analyze 失败 / `chars_to_number` 解析失败 / `available != vacancy_cnt` / `confirm_task` 缺失各打 `Log.warn` + fallback 到 legacy 循环 |
| legacy 循环强化 | 迭代顶部加 `image = ctrler()->get_image()` 防陈旧截图；放置线索后 `Matcher(InfrastReceptionIcon)` 检测关闭右侧面板 |

### [HOT] `src/MaaCore/Task/Interface/RecruitTask.cpp` (x7)

| 操作 | 说明 |
|------|------|
| 淇敼 | +1 鍙傛暟瑙ｆ瀽 `expedite_min_level`(榛樿 0);閾惧紡璋冪敤閫忎紶缁?AutoRecruitTask |
| 瑙ｆ瀽 `expedite_min_level` 鍙傛暟 | 鏂板弬鏁伴€忎紶 |
| 涓存椂璇婃柇鏃ュ織 | `[fix/expedite-threshold/diag] Recruit params: expedite=..., expedite_min_level=...`锛岀敤浜庡畾浣?WPF鈫扟SON鈫扖++ 閾捐矾鏄惁姝ｇ‘閫忎紶 |
| 鍥炴粴 | 璇婃柇鏃ュ織 `git checkout --` 杩樺師锛屼笉鍏ュ簱 |
| 解析 `auto_upgrade_3star_with_4star` 参数（默认 `true`）+ 链式调用 `.set_auto_upgrade_3star_with_4star(...)` | 接口层透传 |
| 双轨字段 | fork `expedite_min_level` + upstream `expedite_times` |
| 恢复 | `params.get("expedite_min_level", 0)` + `.set_expedite_min_level()` 透传 |

### [HOT] `src/MaaCore/Task/Interface/StartUpTask.cpp` (x3)

| 操作 | 说明 |
|------|------|
| 鏀瑰洖鍘熷簭 | `start_game 鈫?account_switch 鈫?start_up`锛況estart 寰幆涔熸敼鍥炲師搴? |
| 淇敼 | `StartUpTask::run` 閲嶆帓锛歚start_game 鈫?start_up 鈫?account_switch 鈫?start_up`锛?x restart_game 寰幆鍐呭悓鏍锋敼涓哄厛鐧诲綍鍐嶅垏鍙? |
| 淇敼 | `.set_task_delay(Config.get_options().task_delay * 2)` 鈫?`.set_task_delay(Config.get_options().task_delay)`锛屽垹 `* 2` |

### [HOT] `src/MaaCore/Task/Miscellaneous/AccountSwitchTask.cpp` (x7)

| 操作 | 说明 |
|------|------|
| 鍥為€€ | `set_retry_times(5)` 鈫?`set_retry_times(30)` + 娉ㄩ噴鏇存柊 |
| 淇敼 | `last_name` 鐧藉悕鍗曡拷鍔?`"AccountManagerPageConfirm"` |
| cherry-pick from `784d9005f6` | `navigate_to_start_page()` 鍔?`Log.info(... last matched task ...)` 璇婃柇鏃ュ織锛? 涓?`else if` 鍚堝苟涓哄崟 `if (... \\|\\| ... \\|\\| ... \\|\\| ...)` |
| 寰呬慨鏀? | `navigate_to_start_page()` 鍔犺瘖鏂棩蹇? |
| 淇敼 | `navigate_to_start_page()` 鍦?`get_last_task_name()` 涔嬪悗杩藉姞 `Log.info(__FUNCTION__, "last matched task:", last_name);`锛屼究浜庡悗缁瘑鍒け璐ユ椂瀹氫綅 |
| 淇敼 | 4 涓?`else if` 鍚堝苟涓哄崟 `if (... \|\| ... \\|\\| ... \\|\\| ...)`锛屽噺灏戝垎鏀祵濂? |
| pre-commit clang-format 合并 if 链多行 | 风格对齐（历史欠债，关联 fix/account-official-recognize） |

### [TGT] `src/MaaCore/Task/Miscellaneous/AccountSwitchTask.h` 

| 操作 | 说明 |
|------|------|
| 修改 | `set_account` 改为 Trim 首尾空白（` \\t\\r\\n`），承接上游 JSON / WPF 未清理的脏数据 |

### [HOT] `src/MaaCore/Task/Miscellaneous/AutoRecruitTask.cpp` (x16)

| 操作 | 说明 |
|------|------|
| 鍒犻櫎 | 绉婚櫎 `confirm()` 涔嬪墠鐨勫姞鎬ュ潡 |
| 鎻掑叆 | 鍦?`confirm()` 鎴愬姛鍚庛€乣return` 鍓嶆彃鍏ユ柊鍔犳€ュ潡, 鍚€岀珛鍗冲畬鎴愰渶鍦ㄤ富椤点€嶆敞閲? |
| 淇敼 | +1 setter 瀹炵幇 `set_expedite_min_level` |
| 淇敼 | `_run` 涓诲惊鐜Щ闄?`try_use_expedited` 灞€閮ㄥ彉閲?鍔犳€ュ垽瀹氭敼涓?*姣忔杩涘叆鍓嶉噸鏂版眰鍊?* `m_use_expedited && m_last_confirmed_min_level >= m_expedite_min_level`;鍔犳€ユ垚鍔熷悗绔嬪嵆閲嶇疆 `m_last_confirmed_min_level = 0` 闃叉闄堟棫鐘舵€佽澶嶇敤;鍔犳€ュけ璐ユ椂鏄惧紡閫€鍑轰互閬垮厤闃堝€?0 鏃舵寰幆 |
| 淇敼 | `recruit_one` 寮€澶撮噸缃?`m_last_confirmed_min_level = 0`,浠呭綋 `recruit_calc_task` 璧板埌 success / nothing_to_select 璺緞鏃舵墠浼氳閲嶆柊璧嬪€? |
| 淇敼 | `recruit_calc_task` 鍦?`nothing_to_select` 涓?`success` 涓ゆ潯杩斿洖璺緞鍓嶈祴鍊?`m_last_confirmed_min_level = final_combination.min_level` |
| 鏂板 `set_expedite_min_level` 瀹炵幇 | setter |
| `_run()` 绉婚櫎鏃х殑 `try_use_expedited` 鍧? | 鏀圭敱 `recruit_one()` 鍐呴€愭Ы鍒ゅ畾 |
| `recruit_calc_task()` 鍐欏叆 `m_last_confirmed_min_level` | 鍔犳€ュ喅绛栦緷鎹? |
| `recruit_one()` 鍔犳€ュ垎鏀? | 4鈽? 鏃?`recruit_now()` 鏇夸唬 `confirm()` |
| 淇敼 | `recruit_one()` 鍏ュ彛澶勮ˉ鍥?`m_last_confirmed_min_level = 0;`锛屾潨缁濅笂涓€妲戒綅闄堟棫鍊兼薄鏌撴湰妲戒綅鍔犳€ュ喅绛? |
| 淇敼 | 鍔犳€ユ垚鍔燂紙`recruit_now()` 鎴愬姛锛夊悗琛ュ洖 `m_last_confirmed_min_level = 0;`锛岄槻姝笅涓€妲戒綅璇垽 |
| 新增 setter 实现 | 链式调用 |
| 新增「4★ 潜力检测」循环 | `min_level==3 && max_level>=4` 时把 min_level/avg_level 重算到 ≥4★ 子集；与 519-535 行「3★ 视角修正」对称 |
| pre-commit clang-format 自动重排加急 Log.info 多行 | 风格对齐 |
| 恢复 | setter；`_run()` 移除 master `try_use_expedited` 块；`recruit_one()` 入口重置星级 + confirm() 后按门槛 `recruit_now()`（失败降级 9h）；`recruit_calc_task()` 写星级 |

### [HOT] `src/MaaCore/Task/Miscellaneous/AutoRecruitTask.h` (x5)

| 操作 | 说明 |
|------|------|
| 淇敼 | +1 setter 澹版槑 `set_expedite_min_level`;+2 鎴愬憳 `m_expedite_min_level`(榛樿 0)銆乣m_last_confirmed_min_level`(榛樿 0) |
| 鏂板 setter `set_expedite_min_level` | 鍔犳€ラ棬妲涙帴鍙? |
| 鏂板鎴愬憳 `m_expedite_min_level` / `m_last_confirmed_min_level` | 闂ㄦ鍊间笌鏈€杩戠‘璁ゆ槦绾? |
| 新增 setter `set_auto_upgrade_3star_with_4star` + 成员 `m_auto_upgrade_3star_with_4star = true` | 升级开关默认开启 |
| 恢复 | `+set_expedite_min_level` / `+m_expedite_min_level` / `+m_last_confirmed_min_level` |

### [TGT] `src/MaaCore/Task/Miscellaneous/RecruitScreenshotMonitor.cpp` (x2)

| 操作 | 说明 |
|------|------|
| 修改 | absdiff 条件增加 `m_last_frame.type() == gray.type()` 防御性检查 |
| 修改 | `image.copyTo(m_last_frame)` 改为 `gray.copyTo(m_last_frame)`，存单通道灰度图 |

### [TGT] `src/MaaUtils` 

| 操作 | 说明 |
|------|------|
| 瀛愭ā鍧楀垵濮嬪寲 | 寮曠敤涓婃父 `MaaXYZ/MaaUtils`锛圚EAD `0c2556cfc`锛夛紝鎻愪氦鑷?feat/fix 绱㈠紩 |

### [HOT] `src/MaaWpfGui/Configuration/Single/MaaTask/RecruitTask.cs` (x4)

| 操作 | 说明 |
|------|------|
| 淇敼 | +1 瀛楁 `ExpediteMinLevel`(榛樿 0) |
| +`ExpediteMinLevel` 灞炴€? | 閰嶇疆妯″瀷 |
| 新增 `AutoUpgrade3StarWith4Star` 字段（默认 `true`） | 配置模型 |
| 接上游 | 同上 |

### [HOT] `src/MaaWpfGui/Configuration/Single/MaaTask/StartUpTask.cs` (x4)

| 操作 | 说明 |
|------|------|
| 淇敼 | 鏂板 `LateStageRogueAndReclamation : bool = false`,榛樿鍏抽棴浠ヤ繚鎸佸悜鍚庡吋瀹? |
| 淇敼 | 娣诲姞 `AccountCycleEnabled` (bool, 榛樿 true) 鍜?`AccountNames` (List\<string\>, 榛樿 ["", ""]) |
| [TGT x2] | 不动（保留字段） |
| **不动** | `AccountName` / `AccountSwitchEnabled` 字段保留, 向后兼容旧 GUI 配置 + 轮换切号仍依赖 |

### [TGT] `src/MaaWpfGui/Constants/JsonDataKey.cs` (x2)

| 操作 | 说明 |
|------|------|
| 修改 | 新增桶 key 前缀辅助 |
| 修改 | 新增 `DefaultDataAccount = "_default"` 兜底桶常量 |

### [TGT] `src/MaaWpfGui/Helper/ListToStringConverter.cs` 

| 操作 | 说明 |
|------|------|
| 新增 | IValueConverter 把 IEnumerable 转字符串（DataGrid Tags 列用） |

### [TGT] `src/MaaWpfGui/MaaWpfGui.csproj` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | `SelfContained` 鏀逛负 `false`锛岀鐢?NetBeauty2 鎵撳寘锛堜笉鍏煎 .NET 10.0.300锛? |
| 淇敼 | 鐗堟湰鍙蜂粠 0.0.1 鏀逛负 6.14.0 |

### [HOT] `src/MaaWpfGui/Main/AsstProxy.cs` (x4)

| 操作 | 说明 |
|------|------|
| 淇敼 | `AllTasksCompleted` 鍥炶皟涓ˉ涓婅疆鎹㈡帹杩涢€昏緫锛氭甯稿畬鎴愭椂璋冪敤 `MarkAccountCompleted` + `GetCurrentCycleAccount` + `LinkStart`锛屽苟 `break` 璺宠繃鏍囧噯瀹屾垚鏃ュ織锛岄槻姝㈡柊涓€杞惎鍔ㄥ悗浠嶆墦鍑?鎵€鏈変换鍔″畬鎴? |
| 淇敼 | `AllTasksCompleted` 鍥炶皟璋?`AdvanceAccountCycle` 鏇夸唬 `SetStopped` |
| 修改 | `RecruitSlotCompleted` case 末尾写入 `RecruitHistoryEntry` + Dispatcher 通知 `RefreshRecruitHistoryView` |
| 接上游 + 保留 fork | 上游删旧 config List + SplitButton 修复；保留 fork 招募回调 |

### [TGT] `src/MaaWpfGui/Main/Bootstrapper.cs` (x2)

| 操作 | 说明 |
|------|------|
| 修改 | `OnStart` 调 `Instances.ToolboxViewModel.LoadRecruitHistory()` 启动时加载 |
| 接上游 + 保留 fork | 同上 |

### [TGT] `src/MaaWpfGui/Models/AccountCycleItem.cs` 

| 操作 | 说明 |
|------|------|
| 鏂板缓 | 杞崲璐﹀彿鏁版嵁妯″瀷锛圖isplayName / AccountName / IsSelected / IsCompleted / Index锛? |

### [TGT] `src/MaaWpfGui/Models/AccountCycleStep.cs` 

| 操作 | 说明 |
|------|------|
| 鏂板缓 | `record AccountCycleStep(string AccountName, int Phase)`,姝ラ鎵佸钩鍒楄〃鐨勮浇浣? |

### [HOT] `src/MaaWpfGui/Models/AsstTasks/AsstRecruitTask.cs` (x4)

| 操作 | 说明 |
|------|------|
| 淇敼 | +1 瀛楁 `ExpediteMinLevel`;`Serialize` 濮嬬粓鍐欏叆 `expedite_min_level` 鍒?params |
| +`ExpediteMinLevel` 灞炴€?+ 搴忓垪鍖? | DTO |
| 新增 DTO 字段 + `Serialize()` 写入 `auto_upgrade_3star_with_4star` | JSON 序列化 |
| 接上游 | Phase A 已删 `auto_upgrade_3star_with_4star` |

### [HOT] `src/MaaWpfGui/Models/DiagnosticInfo.cs` (x5)

| 操作 | 说明 |
|------|------|
| 新建 | 系统信息数据模型 + `Collect()` 静态收集方法：OS/.NET 版本/架构、GPU、管理员、Wine、MAA 版本（UI/Core/Resource） |
| 保留 | 仍被合并后的 `GenerateSupportPayload()` 调用（生成 `diagnostic.json`） |
| [TGT x2] | 新增 `Parts` 字段 + `PartInfo` 记录类型，分卷元数据写回 `diagnostic.json` |
| 新增 | `Parts` 属性 (List<PartInfo>) + `PartInfo` record (FileName / UncompressedSizeBytes / FileCount) |
| 整理 | `DateFilterInfo` / `AppInfo` / `SysInfo` / `GpuInfo` 各字段之间补空行符合 SA1516；`SysInfo.IsWine` / `GpuInfo.Description` 等加空行 |

### [HOT] `src/MaaWpfGui/Res/Localizations/en-us.xaml` (x12)

| 操作 | 说明 |
|------|------|
| 淇敼 | 鍚屼笂(鑻辨枃) |
| 淇敼 | 鍚屼笂锛堣嫳鏂囷級 |
| +`ExpediteMinLevel*` 6 涓?key | 浜旇鏈湴鍖? |
| 修改 | +13 个英文 localization key |
| 修改 | +1 key = "Select diagnostic package save location" |
| 修改 | 删除 7 个失效 key：`ExportDiagnosticPackage` / `ExportDiagnosticPackageButton` / `ExportDiagnosticPackageSuccessful` / `ExportDiagnosticPackageException` / `ExportDiagnosticPackageSelectLocation` / `DiagnosticIncludeGuiLog` / `DiagnosticIncludeCoreLog` |
| 修改 | 新增 2 个 key：`GenerateDiagnosticReport`（按钮文案）/ `GenerateDiagnosticReportSelectLocation`（保存对话框标题） |
| 修改 | 节注释 `<!-- DiagnosticExport -->` → `<!-- DiagnosticReport -->` |
| 同上（英文） | English |
| 保留 fork + 接上游 | 保留 `PasteClipboardCopilotSetTip` 等 fork 字符串 + 上游新 key |
| 同上 (英文) | 同上 |
| 修改 | 各 +2 key：`DataAccountLabel` / `DataAccountDefault` |

### [HOT] `src/MaaWpfGui/Res/Localizations/ja-jp.xaml` (x12)

| 操作 | 说明 |
|------|------|
| 淇敼 | 鍚屼笂(鏃ユ枃) |
| 淇敼 | 鍚屼笂锛堟棩鏂囷級 |
| +`ExpediteMinLevel*` 6 涓?key | 浜旇鏈湴鍖? |
| 修改 | +13 个日文 localization key |
| 修改 | +1 key = "診断パッケージの保存場所を選択" |
| 修改 | 删除 7 个失效 key：`ExportDiagnosticPackage` / `ExportDiagnosticPackageButton` / `ExportDiagnosticPackageSuccessful` / `ExportDiagnosticPackageException` / `ExportDiagnosticPackageSelectLocation` / `DiagnosticIncludeGuiLog` / `DiagnosticIncludeCoreLog` |
| 修改 | 新增 2 个 key：`GenerateDiagnosticReport`（按钮文案）/ `GenerateDiagnosticReportSelectLocation`（保存对话框标题） |
| 修改 | 节注释 `<!-- DiagnosticExport -->` → `<!-- DiagnosticReport -->` |
| 同上（日文） | 日本語 |
| 保留 fork + 接上游 | 保留 `PasteClipboardCopilotSetTip` 等 fork 字符串 + 上游新 key |
| 同上 (日文) | 同上 |
| 修改 | 各 +2 key：`DataAccountLabel` / `DataAccountDefault` |

### [HOT] `src/MaaWpfGui/Res/Localizations/ko-kr.xaml` (x12)

| 操作 | 说明 |
|------|------|
| 淇敼 | 鍚屼笂(闊╂枃) |
| 淇敼 | 鍚屼笂锛堥煩鏂囷級 |
| +`ExpediteMinLevel*` 6 涓?key | 浜旇鏈湴鍖? |
| 修改 | +13 个韩文 localization key |
| 修改 | +1 key = "진단 패키지 저장 위치 선택" |
| 修改 | 删除 7 个失效 key：`ExportDiagnosticPackage` / `ExportDiagnosticPackageButton` / `ExportDiagnosticPackageSuccessful` / `ExportDiagnosticPackageException` / `ExportDiagnosticPackageSelectLocation` / `DiagnosticIncludeGuiLog` / `DiagnosticIncludeCoreLog` |
| 修改 | 新增 2 个 key：`GenerateDiagnosticReport`（按钮文案）/ `GenerateDiagnosticReportSelectLocation`（保存对话框标题） |
| 修改 | 节注释 `<!-- DiagnosticExport -->` → `<!-- DiagnosticReport -->` |
| 同上（韩文） | 한국어 |
| 保留 fork + 接上游 | 保留 `PasteClipboardCopilotSetTip` 等 fork 字符串 + 上游新 key |
| 同上 (韩文) | 同上 |
| 修改 | 各 +2 key：`DataAccountLabel` / `DataAccountDefault` |

### [HOT] `src/MaaWpfGui/Res/Localizations/zh-cn.xaml` (x13)

| 操作 | 说明 |
|------|------|
| 淇敼 | +5 string key:`ExpediteMinLevelLabel` / `ExpediteMinLevelTip` / `ExpediteMinLevel_4Plus` / `ExpediteMinLevel_5Plus` / `ExpediteMinLevel_6Plus` |
| 淇敼 | +2 string key:`LateStageRogueAndReclamation` / `LateStageRogueAndReclamationTip` |
| 淇敼 | 娣诲姞 7 涓?AccountCycle 鏈湴鍖?key |
| +`ExpediteMinLevel*` 6 涓?key | 浜旇鏈湴鍖? |
| 修改 | +13 个中文 localization key（`ExportDiagnosticPackage*` / `DiagnosticDateRange` / `DiagnosticInclude*` / `DiagnosticLast*`） |
| 修改 | +1 key `ExportDiagnosticPackageSelectLocation` = "选择诊断包保存位置" |
| 修改 | 删除 7 个失效 key：`ExportDiagnosticPackage` / `ExportDiagnosticPackageButton` / `ExportDiagnosticPackageSuccessful` / `ExportDiagnosticPackageException` / `ExportDiagnosticPackageSelectLocation` / `DiagnosticIncludeGuiLog` / `DiagnosticIncludeCoreLog` |
| 修改 | 新增 2 个 key：`GenerateDiagnosticReport`（按钮文案）/ `GenerateDiagnosticReportSelectLocation`（保存对话框标题） |
| 修改 | 节注释 `<!-- DiagnosticExport -->` → `<!-- DiagnosticReport -->` |
| 新增 `AutoUpgrade3StarWith4Star` / `AutoUpgrade3StarWith4StarTip` 字符串 | 简体中文 |
| 保留 fork + 接上游 | 保留 `PasteClipboardCopilotSetTip` 等 fork 字符串 + 上游新 key |
| 修改 | 删 `AccountSwitch` / `AccountSwitchManualRun` / `AccountSwitchTip` / `AccountCycleEditMode` 4 key;新增 `AccountCycleTip` / `AccountCycleAddNewAccount` / `AccountCycleRemoveTip` / `AccountCycleRemoveConfirm` / `AccountCycleRemoveMessage` 5 key (顺序调整) |
| 修改 | 各 +2 key：`DataAccountLabel` / `DataAccountDefault` |

### [HOT] `src/MaaWpfGui/Res/Localizations/zh-tw.xaml` (x12)

| 操作 | 说明 |
|------|------|
| 淇敼 | 鍚屼笂(绻佷綋) |
| 淇敼 | 鍚屼笂锛堢箒浣撲腑鏂囷級 |
| +`ExpediteMinLevel*` 6 涓?key | 浜旇鏈湴鍖? |
| 修改 | +13 个繁中 localization key |
| 修改 | +1 key = "選擇診斷包儲存位置" |
| 修改 | 删除 7 个失效 key：`ExportDiagnosticPackage` / `ExportDiagnosticPackageButton` / `ExportDiagnosticPackageSuccessful` / `ExportDiagnosticPackageException` / `ExportDiagnosticPackageSelectLocation` / `DiagnosticIncludeGuiLog` / `DiagnosticIncludeCoreLog` |
| 修改 | 新增 2 个 key：`GenerateDiagnosticReport`（按钮文案）/ `GenerateDiagnosticReportSelectLocation`（保存对话框标题） |
| 修改 | 节注释 `<!-- DiagnosticExport -->` → `<!-- DiagnosticReport -->` |
| 同上（繁体） | 繁体中文 |
| 保留 fork + 接上游 | 保留 `PasteClipboardCopilotSetTip` 等 fork 字符串 + 上游新 key |
| 同上 (繁体) | 同上 |
| 修改 | 各 +2 key：`DataAccountLabel` / `DataAccountDefault` |

### [TGT] `src/MaaWpfGui/Res/Styles/Basic/Geometries.xaml` (x2)

| 操作 | 说明 |
|------|------|
| 修改 | 恢复 `<Geometry x:Key="ClipboardLinkSet20Regular">`（path data 原样复制自 upstream `e9d00b94af`），插入位置对齐 upstream（`ClipboardLink20Regular` 与 `FolderOpen20Regular` 之间） |
| 6-9 | `265bd875a4` \| 新增 `ClipboardLinkSet20Regular` Geometry（两段 path data：剪贴板 + 链接），1 file +4 |

### [TGT] `src/MaaWpfGui/Services/RecruitHistoryService.cs` 

| 操作 | 说明 |
|------|------|
| 新增方法 | `RecordSlotAsync` 用 `Task.Run` 异步执行 Save，避免阻塞 callback 线程 |

### [TGT] `src/MaaWpfGui/ViewModels/UI/CopilotViewModel.cs` 

| 操作 | 说明 |
|------|------|
| 接上游 + 保留 fork | 上游 `CopilotCodeType` 重构 + 保留 fork `IsAmbiguousCopilotCode` + `PasteClipboardCopilotSet` |

### [TGT] `src/MaaWpfGui/ViewModels/UI/RootViewModel.cs` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | 鐗堟湰姣旇緝鏃?`uiVersion` 涔?`TrimStart('v', 'V')`锛屼慨澶?UI 鍜?Core 鐗堟湰鍙蜂竴鑷翠粛寮硅鍛婄殑 bug |
| 淇敼 | 鐗堟湰姣旇緝蹇界暐 `v` 鍓嶇紑 |

### [HOT] `src/MaaWpfGui/ViewModels/UI/TaskQueueViewModel.cs` (x33)

| 操作 | 说明 |
|------|------|
| 淇敼 | `LinkStart` 鏀逛负 `RebuildCycleSteps` + 鍙?`CurrentStep` 鍐冲畾棣栦釜璐﹀彿 |
| 淇敼 | `LinkStartWithTasks` foreach 鏂板 Phase 杩囨护(`IsInCurrentPhase` 鐢?`lateStageOn` 闂搁棬,LateStage 鍏抽棴鏃?no-op) |
| 淇敼 | `AdvanceAccountCycle` 鍏ㄩ噺閲嶅啓:鎵佸钩姝ラ鎺ㄨ繘 + `needStartupSwitch` 鏄惧紡鍒囧彿 + 绌烘楠ら€掑綊璺宠繃 + `MarkAccountCompleted` 鎸?LateStage 鐘舵€佸樊寮傚寲瑙﹀彂 |
| 淇敼 | 鏂板闈欐€佸姪鎵?`IsInCurrentPhase(TaskType, int phase)` |
| 淇敼 | 鍚屼竴澶勭増鏈瘮杈冿紝琛ヤ笂 `uiVersion.TrimStart` |
| 淇敼 | `SetStopped` 涓?`IsCycling` 鐭矾鍒嗘敮澧炲姞"鏄惁琚己鍒跺仠姝?鍒ゆ柇锛歚runStopScript && _runningState.GetStopping()` 鏃惰惤绌?`IsCycling` 璧板畬鏁撮噸缃祦绋?娓?`Stopping` 鏍囧織;姝ｅ父杞崲鎺ㄨ繘璺緞淇濇寔涓嶅彉(鐩存帴 return)銆備慨澶嶇偣鍋滄鎸夐挳鍚?UI 姘歌繙鍗″湪"姝ｅ湪鍋滄"涓旀寜閽笉鍙敤鐨勯棶棰樸€? |
| 淇敼 | `AdvanceAccountCycle` 涓や釜澶辫触鍒嗘敮(`count == 0` 鏃犱换鍔¤闄勫姞銆乣AsstStart()` 澶辫触)鏀逛负璋冪敤 `SetStopped(runStopScript: false)`,缁熶竴閲嶇疆 `Stopping/Idle/IsCycling`銆備慨澶?鍒囨崲绗簩涓处鍙蜂换鍔″嚭閿?鍚庣姸鎬佸崱浣忋€佹寜閽彉鐏般€佹爣棰樹笉鎭㈠鐨勯棶棰樸€? |
| 淇敼 | `LinkStart` 琛ヤ笂 `AccountSwitchEnabled = true`锛沗TryStartNextCycleAccount` 澶勭悊 `cfg` 涓?null 鐨勮竟鐣屾儏鍐碉紱鍖呰９ try-catch 闃叉 `async void` 闈欓粯鍚炲紓甯革紱閫氳繃 `Execute.OnUIThreadAsync` 纭繚 UI 绾跨▼鎵ц |
| 淇敼 | 鏂板 `AdvanceAccountCycle()` 鏂规硶鏇夸唬 `SetStopped` 鍋氳疆鎹㈡帹杩涳紱`SetStopped` 鍓ョ杞崲閫昏緫锛屽彧澶勭悊鍋滄 |
| 淇敼 | `SetStopped` 鏂板杞崲閫昏緫锛氬畬鎴愪换鍔″悗璋冪敤 `MarkAccountCompleted` 鏍囪褰撳墠璐﹀彿瀹屾垚锛岃嫢杩樻湁鏈畬鎴愯处鍙峰垯鑷姩瑙﹀彂 `LinkStart` 缁х画涓嬩竴璐﹀彿 |
| 淇敼 | LinkStart 鍔犲叆杞崲鍒ゅ畾锛孲etStopped 鍚庤皟鐢?TryStartNextCycleAccount 鑷姩鎺ㄨ繘 |
| 淇敼 | `AdvanceAccountCycle` 鏂囨。娉ㄩ噴杩藉姞 fix/defer-rogue/1 娈佃惤 |
| 淇敼 | **A1**: 鎶?`prevStep = GetPreviousStep()` 绉诲埌 `nextStep == null` 鏃╅€€鍒嗘敮**涔嬪墠**;鏃╅€€鍒嗘敮閲屽厛璋冪敤 `MarkPreviousStepCompleted(prevStep)` 鍐?`return` |
| 淇敼 | **A1**: 鏅€氭帹杩涜矾寰勭Щ闄ゅ師 inline 鍧?鏀逛负璋冪敤 `MarkPreviousStepCompleted(prevStep)` |
| 淇敼 | **A1**: 鏂板绉佹湁鏂规硶 `MarkPreviousStepCompleted(AccountCycleStep?)`,璇箟涓庡師 inline 鍧椾竴鑷?`leftPhase2 \\|\\| lateStageOff`) |
| 淇敼 | **A8**: `LinkStart` 椤堕儴鍔?`if (startUpConfig.IsCycling) { Release; return; }` guard,闃叉 Stop 鍚庡啀娆＄偣鍑?/ 瀹氭椂鍣?/ 蹇嵎閿湪 cycle 涓噸缃繘搴? |
| 淇敼 | **#6**: `SetStopped` 灏?cycling 妫€鏌ョЩ鍒?idle 妫€鏌ヤ箣鍓?褰?`IsCycling=true && Idle=true`锛圠inkStartWithTasks 鏃╅€€璺緞锛夋椂娓呯悊 cycling 鐘舵€?璁╂甯稿仠姝㈡帴绠?闃叉杞崲姘镐箙鍗′綇 |
| 淇敼 | **#5**: `AdvanceAccountCycle` 鍏ュ彛鍔?`_logger.Information` 鏃ュ織璁板綍 stepIdx/prev/next 淇℃伅 |
| 淇敼 | **#5**: AdvanceAccountCycle 寰幆鍚庤拷鍔?`_logger.Information` 璁板綍 phase/switch/count/ret |
| 淇敼 | **#5**: AdvanceAccountCycle 涓?Append task 鏃惰褰?`[CycleAdv] Append task #Idx` 鏃ュ織 |
| 淇敼 | **#5**: LinkStartWithTasks 涓?Append task 鏃惰褰?`[LinkStart] Append task #Idx` 鏃ュ織 |
| 淇敼 | **#2/#3**: AdvanceAccountCycle 鐨?Phase 浠诲姟寰幆鐢?foreach + `IndexOf` 鏀逛负 **for 寰幆** (`int index = i`),娑堥櫎閲嶅椤?椤哄簭鍙樻洿鏃剁殑绱㈠紩閿欒;鍚屾椂淇濇寔鍘熸湁 Phase 杩囨护/StartUp 璺宠繃/`SetTaskIds` 閫昏緫涓嶅彉 |
| 淇敼 | **#4**: AdvanceAccountCycle 鍒濆鏃ュ織杩藉姞 `idx={CurrentStepIndex}/{CurrentStepCount}` 鏄剧ず姝ラ浣嶇疆 |
| 修改 | `LinkStartWithTasks` 第一步 + `AdvanceAccountCycle` 步骤：cfg.AccountName / CurrentCycleAccountName Trim，切换日志 Trim，跨账号判定 `prevStep.AccountName?.Trim() != nextStep.AccountName?.Trim()` |
| 保留 fork import | fork `MaaWpfGui.Services.Notification` |
| 修改 | `AsstStart()` 失败 + `AsstRunning()` 兜底判定;失败分支加 `AsstStop()`;注释标记 `fix/account-cycle-start-race` |
| 修改 | 一行式短路实现,保持 append 失败旧语义;注释补充短路说明 |
| 提交 | `startOk = taskRet && (AsstStart() \|\| AsstRunning())` 兜底判定 + 失败分支 `AsstStop()` 清队列；注释带 `fix/account-cycle-start-race` 标记 |
| [HOT x28] | 无逻辑改动 |
| **不动** | `LinkStart:1931` / `AdvanceAccountCycle:2386` 的 `cfg.AccountSwitchEnabled = true` 保留 (SerializeTask 仍读此字段) |
| 修改 | downstream [HOT x30]：该文件曾被 feat/account_rotation / fix/account_rotation/6 / fix/account-cycle-start-race 改动，本次改动原因：LinkStart 与 AdvanceAccountCycle 两处各加一行 SwitchDataAccount 锚定调用 |
| 修改 | `LinkStart` 运行前锚定（downstream [HOT x30]：曾被 feat/account_rotation、fix/account_rotation/6、fix/account-cycle-start-race 改动；本次仅加锚定调用，不动既有逻辑） |
| 修改 | `AdvanceAccountCycle` 切号即切桶（同上敏感文件说明） |

### [HOT] `src/MaaWpfGui/ViewModels/UI/ToolboxViewModel.cs` (x10)

| 操作 | 说明 |
|------|------|
| 新增 60+ 行 | `RecruitHistoryEntries` ObservableCollection + `RecruitHistorySearchText` / `RecruitHistoryFilterOcrStatus` 过滤器 + `OpenRecruitScreenshot` / `EditRecruitOperator` / `ExportRecruitHistory` / `ImportRecruitHistory` / `ClearOldRecruitScreenshots` 五个命令 + `LoadRecruitHistory()` 启动入口 |
| 修改 | downstream: 该文件曾被 feat/recruit-history-tab 改动，本次改动原因：新增账号桶路由/切换/迁移/掉落基线保护 |
| 修改 | 新增 `using MaaWpfGui.Configuration.Single.MaaTask;`（读 StartUpTask.AccountName） |
| 修改 | ctor 加 `InitializeAccountScopedData()`（迁移+锚定初始桶）与 `RefreshDataAccountList()`；语言切换回调补 `RefreshDataAccountList()`（显示名本地化） |
| 新增 | `#region AccountScopedRecognitionData`：`_currentDataAccountKey/_currentDataAccountRaw/_knownDataAccountKeys` + `DataAccountList`/`SelectedDataAccount` + `AccountDataBucketKey`/`SanitizeAccountKey`/`ResolveConfiguredAccountName`/`InitializeAccountScopedData`/`MigrateLegacyRecognitionData`/`SwitchDataAccount`/`RefreshDataAccountList`/`GetDataAccountDisplayName`/`RegisterCurrentBucketIfNew` |
| 修改 | `SaveDepotDetails` 嵌 `account` 字段 + 按桶保存 + 新桶注册；`LoadDepotDetails` 从 `DepotBucketKey` 加载 |
| 修改 | `UpdateDepotFromDrops` 无基线守卫（跳过 + 日志） |
| 修改 | `StartDepot` 手动识别前 `SwitchDataAccount(ResolveConfiguredAccountName())` |
| 修改 | `SaveOperBoxDetails` 嵌 `account` 字段 + 按桶保存 + 新桶注册；`LoadOperBoxDetails` 从 `OperBoxBucketKey` 加载 |
| 修改 | `StartOperBox` 手动识别前锚定 |

### [HOT] `src/MaaWpfGui/ViewModels/UserControl/Settings/IssueReportUserControlModel.cs` (x30)

| 操作 | 说明 |
|------|------|
| 修改 | 新增诊断导出属性：`DiagnosticDateRange`(默认 7 天)、`IncludeConfig`/`IncludeCache`/`IncludeCustomResource` 三个 CheckBox、`DateRangeOption` record 与 `DateRangeOptions` 懒加载列表 |
| 修改 | 新增 `ExportDiagnosticPackage()` 方法：收集系统信息 → diagnostic.json → 逐日志文件按日期范围过滤行 → 可选目录复制 → zip 打包 → growl + 打开 reports 目录 |
| 修改 | 新增 `CopyFilteredLog()` 辅助方法：正则 `^\[\d{4}-\d{2}-\d{2}` 逐行解析日志时间戳，仅保留日期范围内行；非时间戳行（异常栈）自动保留 |
| 修改 | 新增 `using Microsoft.Win32;`（按字母顺序排在 `MaaWpfGui.Models` 之后、`Serilog` 之前，避免 SA1208/SA1210） |
| 修改 | `ExportDiagnosticPackage()` 顶部加 SaveFileDialog：Title 用本地化键 `ExportDiagnosticPackageSelectLocation`、Filter=`ZIP files (*.zip)\\|*.zip`、默认文件名=`{reportName}.zip`、初始目录=`PathsHelper.ReportsDir`、开启 OverwritePrompt + AddExtension + DefaultExt |
| 修改 | 把 `tempPath` 创建移到 SaveDialog 之后（取消导出时不创建无用临时目录） |
| 修改 | `if (saveDialog.ShowDialog() != true) return;` — 取消安全退出，不弹 growl |
| 修改 | `zipPath = saveDialog.FileName` 替代硬编码 `Path.Combine(ReportsDir, ...)` |
| 修改 | 删除 `using System.Text.RegularExpressions;`（不再需要行级日志过滤） |
| 修改 | 注释 `// ===== Diagnostic Export Properties =====` → `// ===== Diagnostic Report Properties (used by GenerateSupportPayload) =====` |
| 修改 | `_includeCustomResource` 默认值 `false` → `true`（保留原 GenerateSupportPayload 行为：始终包含自定义资源） |
| 修改 | `GenerateSupportPayload()` 重写：顶部加 SaveFileDialog 选保存位置（默认目录 `PathsHelper.ReportsDir`，默认文件名 `report_{MM-dd_HH-mm-ss}.zip`），生成 `diagnostic.json` 系统信息，原 config/resource/cache 复制改为按 `_includeConfig`/`_includeCache`/`_includeCustomResource` 条件复制，原 hardcoded 3 天 `threeDaysAgo` 改为 `_diagnosticDateRange`，完整 zip 输出路径改为 `saveDialog.FileName`，分卷输出目录改为 `userChosenDir/{name}_parts/` 紧贴用户选定位置 |
| 修改 | part01 增加 `Directory.EnumerateFiles(tempPath, "*", SearchOption.TopDirectoryOnly)` 包含 `diagnostic.json` 在分卷中 |
| 删除 | `ExportDiagnosticPackage()` 方法（约 100 行） |
| 删除 | `CopyFilteredLog()` 行级日志过滤方法（约 50 行） |
| [HOT x3] | `GenerateSupportPayload` 拆分为 7 个单一职责方法（TryPrepareExportContext / WriteDiagnosticJson / CopyAll / SplitIntoParts / CreateFullZip / Cleanup / ShowResult）；新增 `ExportContext`/`CopyResult` record；新增 `IsBusy`/`BusyStatusText` 属性；删除自动 `OpenReportsFolder()`；catch IOException/UnauthorizedAccessException 改为 `Log.Warning` + 累计失败列表 |
| 新增 | `MaxPartSizeBytes = 20L * 1024 * 1024` 常量 + GitHub Issue 附件上限注释 |
| 修改 | `_dateRangeOptions ??= InitDateRangeOptions()` → `Lazy<List<DateRangeOption>>`（并发安全） |
| 新增 | `IsBusy` / `IsNotBusy` / `BusyStatusText` 三个 PropertyChangedBase 属性 |
| 修改 | `ClearImageCache` 加注释解释 `yes=Cancel`/`no=Confirm` 是 HandyControl 自定义按钮文案机制，非 bug |
| 重写 | `GenerateSupportPayload()` 入口改 async void：判 IsBusy → TryPrepareExportContext (UI 线程) → Task.Run(ExecuteExportPipeline) → ShowGrowlSuccess/Error；finally 中 SafeDelete + 重置 IsBusy |
| 新增 | `ExecuteExportPipeline(ctx)` 后台流水线：CopyAll → WriteDiagnosticJson → SplitIntoParts → CreateFromDirectory |
| 新增 | `TryPrepareExportContext()` UI 线程：SaveFileDialog + tempPath 创建；SaveFileDialog 抛异常时也清理 tempPath（try/catch + throw） |
| 新增 | `WriteDiagnosticJson(ctx, fromDate, toDate)`：`DiagnosticInfo.Collect()` + WriteAllText |
| 新增 | `CopyAll(ctx, fromDate)` → CopyResult：debug（含日期过滤）→ resource（`_custom.`）→ config → cache；返回 CopyResult 含失败文件列表 |
| 新增 | `SplitIntoParts(ctx, fromDate)`：统一按 20MB 分卷；按文件名排序保证 part 内容稳定；单文件 >20MB 单独成卷；跳过 diagnostic.json（最后回填）；回填 Parts 字段到 diagnostic.json |
| 重写 | `CopyDirectoryWithLogging`：debug 根文件始终包含；子目录文件按 fromDate 过滤 LastWriteTime；catch IOException/UnauthorizedAccessException → Log.Warning + 累计到 result.FailedFiles（不再静默吞错） |
| 新增 | `ShowGrowlSuccess` / `ShowGrowlError` / `ReportBusyStatus` 三个 UI 线程辅助方法（Dispatcher.Invoke 投递） |
| 新增 | `SafeDelete(path)` 静默删除 tempPath，catch 异常仅 Log.Warning |
| 新增 | `ExportContext` record (FromDate / ToDate / ReportNameBase / TempPath / FullZipPath / PartsFolder) + `CopyResult` class (CopiedCount / FailedFiles) |

### [HOT] `src/MaaWpfGui/ViewModels/UserControl/TaskQueue/RecruitSettingsUserControlModel.cs` (x3)

| 操作 | 说明 |
|------|------|
| 淇敼 | +3 VM 鎴愬憳:`ExpediteMinLevelEnabled`(甯冨皵,setter 鎺у埗 0/4 鍒囨崲)銆乣ExpediteMinLevel`(int,setter 鐧藉悕鍗?0/4/5/6)銆乣ExpediteMinLevelOptions`(4/5/6 涓夋。 ComboBox 閫夐」);Serialize 闃舵鍐欏叆 `ExpediteMinLevel` |
| +`ExpediteMinLevelList` / `UseExpeditedMinLevel` / `UseExpeditedMinLevelVisible` | ViewModel |
| 新增 VM 属性 `AutoUpgrade3StarWith4Star` + `SerializeTask()` 写入 | 双向绑定 |

### [HOT] `src/MaaWpfGui/ViewModels/UserControl/TaskQueue/StartUpSettingsUserControlModel.cs` (x13)

| 操作 | 说明 |
|------|------|
| 淇敼 | (a) `LateStageRogueAndReclamation` VM 灞炴€?鐓ф惉 `AccountSwitchEnabled`);(b) 鏂板 `#region Late Stage` 鍚?`_cycleSteps` / `_currentStepIndex` / `RebuildCycleSteps` / `AdvanceStepIndex` / `CurrentStep` / `GetPreviousStep` / `CurrentPhase`;(c) `ResetCycle` 鍚屾娓呯┖姝ラ鍒楄〃 |
| 淇敼 | `GetCurrentCycleAccount` 绠€鍖栵細鍘绘帀 `_currentCycleIndex` 鐘舵€佽窡韪紝鏀逛负鐩存帴鍙栫涓€涓鍚堟潯浠剁殑璐﹀彿锛涘幓鎺?`ResetCycleIndex` 鏂规硶 |
| 淇敼 | `SyncAccountNamesToItems` 淇濈暀宸叉湁椤?`IsSelected` 鐘舵€侊紝鐢ㄦ埛鍙嚜鐢卞嬀閫夊弬涓庤疆鎹㈢殑璐﹀彿 |
| 淇敼 | 娣诲姞杞崲 CRUD銆丟etCurrentCycleAccount銆丮arkAccountCompleted銆丼yncAccountNamesToItems 绛夋柟娉? |
| 淇敼 | **#5**: 鏂板 `CurrentStepIndex` 鍏紑灞炴€ф敮鎸佹棩蹇? |
| 修改 | `SyncAccountNamesToItems` 入口对 `config.AccountNames[]` / `config.AccountName` 做 Trim，首条受影响打 INFO 日志；下移原「单账号复制到轮换列表第一项」逻辑 |
| 保留 fork | `using Stylet;` + variable name |
| [HOT x7] | 删 `#region Account Switch (Single)` + ShowEditSection/AccountCycleMode/ShowAddMode/ShowDeleteMode；RemoveAccount 加 MessageBox |
| 修改 | 加 `using System.Windows;` (MessageBoxButton/MessageBoxResult) |
| 修改 | 删 `#region Account Switch (Single)` 整段 (`AccountName` / `AccountSwitchEnabled` 属性 + `AccountSwitchManualRun` 命令, 共 ~60 行);留注释说明字段仍保留 |
| 修改 | 删 `ShowEditSection` / `_showEditSection` / `AccountCycleMode` / `_accountCycleMode` / `ShowAddMode` / `ShowDeleteMode` 共 4 属性 2 字段, 留注释 |
| 修改 | `SyncAccountNamesToItems` 迁移注释改为「向后兼容迁移」语义 |
| 修改 | `RemoveAccount` 加 `MessageBoxHelper.Show` 二次确认 (复用 `TaskQueueViewModel.RemoveTask:1568-1581` 模式); 弹窗文案 `AccountCycleRemoveMessage` 含 `{0}` 占位符 = 账号名 |

### [TGT] `src/MaaWpfGui/ViewModels/UserControl/TaskQueue/UserDataUpdateSettingsUserControlModel.cs` 

| 操作 | 说明 |
|------|------|
| 淇敼 | **#1**: cycle 涓?(`GetAccountSwitchEnabled()`) 璺宠繃 `IsTriggerDue` 妫€鏌?淇濊瘉姣忎釜璐﹀彿鐨?OperBox/Depot 瀛愪换鍔￠兘琚拷鍔? |

### [TGT] `src/MaaWpfGui/Views/UI/CopilotView.xaml` 

| 操作 | 说明 |
|------|------|
| 保留 fork UI | `PasteClipboardCopilotSet` 按钮 + 3 个 TooltipBlock |

### [TGT] `src/MaaWpfGui/Views/UI/TaskQueueView.xaml` 

| 操作 | 说明 |
|------|------|
| **不动** | 仍引用 `CurrentAccountLabel` (TODO deferred: 5 语 xaml 全缺, 不在本 fix 范围) |

### [HOT] `src/MaaWpfGui/Views/UI/ToolboxView.xaml` (x4)

| 操作 | 说明 |
|------|------|
| 新增 TabItem | 过滤栏 + Total TextBlock + DataGrid 9 列 + 3 操作按钮 |
| 修改 | downstream: 该文件曾被 feat/recruit-history-tab 改动，本次改动原因：OperBox/Depot 两个 Tab 各加账号查看下拉 |
| 修改 | OperBox Tab Row0 改横向 StackPanel：同步时间 + 账号下拉（downstream: 曾被 feat/recruit-history-tab 改动，本次仅加查看下拉） |
| 修改 | Depot Tab 同款账号下拉 |

### [HOT] `src/MaaWpfGui/Views/UserControl/Settings/IssueReportUserControl.xaml` (x5)

| 操作 | 说明 |
|------|------|
| 修改 | IssueReport 页面新增诊断导出区域：日期范围 ComboBox + 3 个 CheckBox + 导出按钮 |
| 修改 | 右侧 StackPanel 内插入新 UI：日期范围 Grid（ComboBox + TextBlock）+ 3 CheckBox（配置文件/缓存/自定义资源），按钮文案 `GenerateSupportPayload` → `GenerateDiagnosticReport` |
| 删除 | 独立"导出诊断包" StackPanel + Border（约 60 行） |
| [HOT x4] | 左右列重新布局：左列加说明 TextBlock；右列 ComboBox 宽度 120→140；3 CheckBox 加 TooltipBlock；按钮 `IsEnabled={Binding IsNotBusy}`；新增 `BusyStatusText` TextBlock |
| 重写 | 左右列重新布局：左列 `IssueReportLeftTitle` + Help/Issue 超链接 + 分隔线 + `IssueReportLeftHint` 灰色说明；右列 ComboBox 宽度 120→140 + Tooltip，3 CheckBox + TooltipBlock，按钮 `IsEnabled={Binding IsNotBusy}` + BusyStatusText TextBlock |

### [HOT] `src/MaaWpfGui/Views/UserControl/TaskQueue/RecruitSettingsUserControl.xaml` (x3)

| 操作 | 说明 |
|------|------|
| 淇敼 | 楂樼骇璁剧疆鍖烘湯灏捐拷鍔?CheckBox + ComboBox;鏁磋 Visibility 缁戝畾鍒?`UseExpeditedWithNull == true` |
| +闂ㄦ涓嬫媺妗? | UI |
| 新增 `StackPanel` 包裹 `CheckBox` + `TooltipBlock`，位于「3星 Tag 时的 Tag 倾向」区域下方 | UI 控件 |

### [HOT] `src/MaaWpfGui/Views/UserControl/TaskQueue/StartUpTaskUserControl.xaml` (x8)

| 操作 | 说明 |
|------|------|
| 淇敼 | AccountCycle 瀛愰潰鏉挎湯灏炬柊澧?CheckBox + TooltipBlock(闀?Wrap + MaxWidth CalcBinding 闃叉尋鍘? |
| 淇敼 | 娣诲姞/鍒犻櫎鎸夐挳鍥炬爣缁熶竴瀛楀彿鍜屽眳涓? |
| 淇敼 | 娣诲姞杞崲 CheckBox銆佽处鍙峰垪琛?ItemsControl銆佺紪杈戞ā寮?ComboBox銆両sCompleted 钃濊壊楂樹寒 |
| 淇敼 | **A7**: `LateStageRogueAndReclamation` CheckBox 鍔?`IsEnabled="{c:Binding '!IsCycling'}"`,Cycle 杩愯涓伆鏄? |
| [HOT x4] | 删 AccountSwitch section + 编辑模式 ComboBox；重构每项模板；末尾 [+ 添加账号] 按钮 |
| 修改 | 删单账号 section (原 26-54);轮换 CheckBox 加 `AccountCycleTip` tooltip |
| 修改 | 每项模板简化: 删 `AddAccountAfter` 按钮 + `ShowAddMode`/`ShowDeleteMode` visibility;`RemoveAccount` 按钮 tooltip 改 `AccountCycleRemoveTip` 并永久可见 |
| 新增 | 末尾 `[+ 添加账号]` 按钮, `CommandParameter={x:Null}` 走 `AddAccountAfter` null 分支 |

## `tools/`（7 个文件）

### [TGT] `tools/DependencySetup_渚濊禆搴撳畨瑁?bat` (x2)

| 操作 | 说明 |
|------|------|
| git rm | 缁堢鐢ㄦ埛渚濊禆瀹夎鑴氭湰锛屼笉鍐嶉渶瑕? |
| 杩樺師 | 浠?`install/` 鍓湰鎷峰洖 `tools/`锛屾仮澶?`Copy-Item` 婧愶紱骞跺叆 git |

### [TGT] `tools/OptimizeTemplates/optimize_templates.json` 

| 操作 | 说明 |
|------|------|
| 接上游 | v6.16.x 模板哈希 |

### [TGT] `tools/gen-downstream-changes.py` (x2)

| 操作 | 说明 |
|------|------|
| 鏂板缓 | 瑙ｆ瀽 LOG.md 4 鍒楄〃鏍硷紙`# / 鏂囦欢(瀵硅薄) / 鎿嶄綔 / 璇存槑`锛夛紝鎻愬彇鍒?2 鍙嶅紩鍙疯矾寰勶細鍘昏鍙峰悗缂€锛坄path:123-456` 鈫?`path`锛夈€乥race-aware 閫楀彿鍒囧垎锛堜繚鐣?`{zh-cn,en-us}` 鍐呴€楀彿锛夈€乻hell brace 灞曞紑锛坄{a,b,c}.xaml` 鈫?澶氫釜鏂囦欢锛夈€佽繃婊?`install*/`/`build/`/`debug/`/`config/`/`cache/`/`data/`/`reports/` 绛夐潪婧愮爜浜х墿锛涙寜椤跺眰鐩綍鍒嗙粍锛岃鏀?鈮?3 娆℃爣 `[HOT]`锛屽惁鍒?`[TGT]`锛涜緭鍑?markdown 琛ㄦ牸銆傛敮鎸?`--log` / `--out` / `--dry-run` 鍙傛暟 |
| 修改 | 加 `_read_log_text` 兼容 LOG.md 历史 UTF-16 LE BOM 编码（脚本此前 hardcode `encoding="utf-8"` 在 LOG.md 为 UTF-16 LE 时抛 UnicodeDecodeError） |

### [TGT] `tools/local-install-staging.bat` (x2)

| 操作 | 说明 |
|------|------|
| 鏂板缓 | 鍩轰簬 `local-install.bat`锛? 澶?`install` 璺緞鏀逛负 `install-staging`锛坈make `--prefix`銆乨otnet `-o`銆乶beauty 琛ヤ竵銆佹竻鐞?`*.h`/`msvc-debug`銆乺obocopy `resource`锛? |
| 淇敼 | 鍚屼笂 |

### [TGT] `tools/local-install.bat` (x2)

| 操作 | 说明 |
|------|------|
| 淇敼 | `global.json` 娉ㄥ叆浠?`{"version":"10.0.203","rollForward":"disable"}` 鏀逛负 `{"version":"10.0.100","rollForward":"latestFeature"}` |
| 保留 fork SDK 锁死 | `10.0.100 + latestFeature`（本机 10.0.300 适配） |

### [TGT] `tools/release-zip.bat` (x2)

| 操作 | 说明 |
|------|------|
| git rm | 鍙戝竷鎵撳寘鑴氭湰锛屼笉鍐嶉渶瑕? |
| 鏂板缓 | bat 澶栧３锛岃皟 ps1 鍚?`pause`锛涘け璐ユ椂 `errorlevel` 閫忎紶 |

### [HOT] `tools/release-zip.ps1` (x3)

| 操作 | 说明 |
|------|------|
| git rm | 鍙戝竷鎵撳寘鑴氭湰锛屼笉鍐嶉渶瑕? |
| 淇敼 | 姝ラ 2 鏀瑰崟鐩爣 cmake build锛涙楠?6 鏀?global.json 涓?`10.0.100 + latestFeature`锛涙楠?8 staging `/XD` 鍔?`.git`锛涜剼鏈敞閲婅鏄庢敼鍔ㄥ師鍥? |
| 鏂板缓 | 鏍稿績 PowerShell 鑴氭湰锛垀180 琛岋級锛宍-Version` / `-SkipBuild` / `-KeepInstallerDir` 涓変釜寮€鍏? |

