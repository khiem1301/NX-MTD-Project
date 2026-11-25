#!/usr/bin/env python3
"""Bóc kết quả Integration Test từ file .trx ra JSON + Markdown gọn.

  python tests/scripts/parse_trx.py --trx tests/docs/IT_RESULTS/IT_B3_Round1.trx [--round 1] [--out-dir DIR] [--force]

Mã ca lấy bằng regex IT_B\\d+_\\d+ trên tên test (hàm [Fact] tên IT_B2_001_..., hoặc [Theory] có tham số
caseId: "IT_B2_001" trong tên hiển thị). Một ID xuất hiện nhiều lần (Theory / chạy lặp): chỉ cần 1 lần Failed là Failed.
Xuất: IT_B[X]_Round[N].json (Id, Outcome, Message ≤ 200 ký tự) và IT_B[X]_Round[N]_Failed.md (chỉ ca Failed).
Không ghi đè file .md đã có (thường được viết tay thêm phân tích) trừ khi có --force.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ID_RE = re.compile(r"IT_B\d+_\d+")
MAX_MSG = 200
DEFAULT_OUT = Path(__file__).resolve().parents[1] / "docs" / "IT_RESULTS"


def short(text: str) -> str:
    text = re.sub(r"\s+", " ", text or "").strip()
    return text if len(text) <= MAX_MSG else text[: MAX_MSG - 1] + "…"


def parse(trx: Path) -> dict[str, dict]:
    root = ET.parse(trx).getroot()
    ns = {"t": root.tag.split("}")[0].strip("{")}
    # testId -> tên hàm (TestMethod.name) để bắt ca mà tên hiển thị không chứa ID
    methods = {
        d.get("id"): (d.find("t:TestMethod", ns).get("name", "") if d.find("t:TestMethod", ns) is not None else "")
        for d in root.iterfind(".//t:UnitTest", ns)
    }
    rows: dict[str, dict] = {}
    for r in root.iterfind(".//t:UnitTestResult", ns):
        m = ID_RE.search(r.get("testName", "")) or ID_RE.search(methods.get(r.get("testId"), ""))
        if not m:
            continue
        cid = m.group(0)
        passed = r.get("outcome") == "Passed"
        msg = ""
        if not passed:
            node = r.find(".//t:ErrorInfo/t:Message", ns)
            msg = short(node.text if node is not None else r.get("outcome", ""))
        prev = rows.get(cid)
        if prev is None or (prev["Outcome"] == "Passed" and not passed):
            rows[cid] = {"Id": cid, "Outcome": "Passed" if passed else "Failed", "Message": msg}
    return rows


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--trx", type=Path, required=True)
    ap.add_argument("--round", type=int, default=1)
    ap.add_argument("--out-dir", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--force", action="store_true", help="ghi đè file _Failed.md đã có")
    args = ap.parse_args()

    rows = parse(args.trx)
    if not rows:
        sys.exit("Không tìm thấy mã IT_B<X>_<NNN> nào trong file trx.")
    batches = {re.match(r"IT_B(\d+)_", i).group(1) for i in rows}
    if len(batches) != 1:
        sys.exit(f"File trx lẫn nhiều batch: {sorted(batches)}")
    b = batches.pop()
    data = [rows[k] for k in sorted(rows)]
    failed = [d for d in data if d["Outcome"] == "Failed"]

    args.out_dir.mkdir(parents=True, exist_ok=True)
    stem = f"IT_B{b}_Round{args.round}"
    (args.out_dir / f"{stem}.json").write_text(json.dumps(data, ensure_ascii=False, indent=4), encoding="utf-8")

    md = args.out_dir / f"{stem}_Failed.md"
    if md.exists() and not args.force:
        print(f"Giữ nguyên {md.name} (đã có; dùng --force để ghi đè).")
    else:
        lines = [f"# IT Batch {b} — Round {args.round}: các ca Failed", "",
                 f"Kết quả: **{len(data)} ca, {len(data) - len(failed)} Passed, {len(failed)} Failed**. Log: `{args.trx.name}`.", "",
                 "| Ca | Thông báo lỗi |", "|---|---|"]
        lines += [f"| {d['Id'][-3:]} | {d['Message'].replace('|', '\\|')} |" for d in failed]
        md.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"Batch {b}: {len(data)} ca; {len(data) - len(failed)} Passed; {len(failed)} Failed → {args.out_dir / (stem + '.json')}")


if __name__ == "__main__":
    main()
