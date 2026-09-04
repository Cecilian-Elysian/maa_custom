#!/usr/bin/env python3
"""
inject-recruitnow-slot.py

上游同步后,把 fork 私有的 RecruitNow@Slot0..3 四个 task 注入到
resource/tasks/tasks.json, 消除该文件与上游的 fork 私有 diff.

使用方法:
    python tools/inject-recruitnow-slot.py

锚点策略:
    在 `"RecruitNow"` task 定义的 `"next": ["RecruitNowConfirm"]` 之后
    插入 4 个 @Slot0..3 派生 task. 锚点是 task 名 + 子键, 不是行号,
    因此上游修改 tasks.json 任何其他位置都不会影响注入.

契约:
    C++ `recruit_now()` 引用这 4 个 task 名 (`RecruitNow@Slot0..3`).
    任何下游改动需同步此脚本的 SLOT_TASKS 常量 + C++ 侧的 slot 名常量.

对应 AGENTS.md §3.2 (Fork 同步 SOP) 与 WORKFLOW.md §P0 抽离隔离.
"""
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
TARGET = REPO_ROOT / "resource" / "tasks" / "tasks.json"

# Fork 私有: 加急点击按槽位限定 ROI (fix/recruit-expedite-slot-target)
# roi 按 `slot_index_from_rect` 分界线 x=640 / y=450 划分象限
SLOT_TASKS = {
    "RecruitNow@Slot0": {
        "baseTask": "RecruitNow",
        "roi": [0, 300, 640, 150],
    },
    "RecruitNow@Slot1": {
        "baseTask": "RecruitNow",
        "roi": [640, 300, 640, 150],
    },
    "RecruitNow@Slot2": {
        "baseTask": "RecruitNow",
        "roi": [0, 450, 640, 300],
    },
    "RecruitNow@Slot3": {
        "baseTask": "RecruitNow",
        "roi": [640, 450, 640, 300],
    },
}

ANCHOR_TASK = "RecruitNow"
ANCHOR_NEXT_KEY = "next"


def main() -> int:
    if not TARGET.exists():
        print(f"[ERR] tasks.json not found: {TARGET}", file=sys.stderr)
        return 1

    with TARGET.open("r", encoding="utf-8") as f:
        data = json.load(f)

    if ANCHOR_TASK not in data:
        print(f"[ERR] anchor task '{ANCHOR_TASK}' not in tasks.json", file=sys.stderr)
        return 1

    existing_slots = [name for name in SLOT_TASKS if name in data]
    if existing_slots:
        for name in existing_slots:
            if data[name] != SLOT_TASKS[name]:
                print(
                    f"[WARN] {name} 存在但内容与脚本常量不同, 请手动核对:",
                    file=sys.stderr,
                )
                print(f"  现有: {json.dumps(data[name], ensure_ascii=False)}",
                      file=sys.stderr)
                print(f"  期望: {json.dumps(SLOT_TASKS[name], ensure_ascii=False)}",
                      file=sys.stderr)
        print(f"[OK] {len(existing_slots)}/{len(SLOT_TASKS)} slot tasks 已是最新, 无需注入")
        return 0

    anchor_next = data[ANCHOR_TASK].get(ANCHOR_NEXT_KEY)
    if anchor_next != ["RecruitNowConfirm"]:
        print(
            f"[WARN] {ANCHOR_TASK}.{ANCHOR_NEXT_KEY} = {anchor_next}, "
            f"与预期 ['RecruitNowConfirm'] 不一致, 跳过注入以免破坏结构",
            file=sys.stderr,
        )
        return 1

    for name, body in SLOT_TASKS.items():
        data[name] = body

    backup = TARGET.with_suffix(".json.bak")
    backup.write_text(TARGET.read_text(encoding="utf-8"), encoding="utf-8")

    json_text = json.dumps(data, ensure_ascii=False, indent=4)
    TARGET.write_text(json_text + "\n", encoding="utf-8")

    print(f"[OK] 注入 {len(SLOT_TASKS)} 个 slot tasks 到 {TARGET}")
    print(f"[OK] 备份: {backup}")
    return 0


if __name__ == "__main__":
    sys.exit(main())