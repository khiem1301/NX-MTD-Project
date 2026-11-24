# -*- coding: utf-8 -*-
"""Read-only Excel stats dump for UT summary. No tests executed."""
from openpyxl import load_workbook
from collections import defaultdict
import json

path = r"D:\01.LEARNING MATERIALS\FPT University\Semester 9\SEP490\01. Báo cáo chính thức\Report-5.1_Unit Test.xlsx"
wb = load_workbook(path, data_only=True)
out = {"sheets": wb.sheetnames, "modules": {}, "failed_r1": [], "failed_r2": [], "stats_notes": None}

st = wb["Test Statistics"]
out["stats_notes"] = st["B7"].value
out["stats_rows"] = []
for r in range(11, 40):
    vals = [st.cell(r, c).value for c in range(1, 10)]
    if any(v is not None for v in vals):
        out["stats_rows"].append({"row": r, "vals": vals})

for name in wb.sheetnames:
    if name in ("Cover", "Test Cases", "Test Statistics"):
        continue
    ws = wb[name]
    mod = {
        "tc": ws["B3"].value,
        "r1": {"P": ws["B5"].value, "F": ws["C5"].value, "Pend": ws["D5"].value},
        "r2": {"P": ws["B6"].value, "F": ws["C6"].value, "Pend": ws["D6"].value},
        "r3": {"P": ws["B7"].value, "F": ws["C7"].value, "Pend": ws["D7"].value},
        "fails_r1": [],
        "fails_r2": [],
    }
    for r in range(12, (ws.max_row or 12) + 1):
        uid = ws.cell(r, 1).value
        if not isinstance(uid, str) or not uid.startswith("UTC"):
            continue
        r1 = ws.cell(r, 6).value
        r2 = ws.cell(r, 9).value
        exp = ws.cell(r, 4).value
        typ = ws.cell(r, 5).value
        pre = ws.cell(r, 2).value
        if r1 == "Failed":
            mod["fails_r1"].append({"id": uid, "type": typ, "expected": (exp or "")[:200], "pre": (pre or "")[:120]})
        if r2 == "Failed":
            mod["fails_r2"].append({"id": uid, "type": typ, "expected": (exp or "")[:200]})
    out["modules"][name] = mod

dest = r"D:\01.LEARNING MATERIALS\FPT University\Semester 9\SEP490\04. Source code\NX-MTD\NX-MTD\tests\docs\_excel_stats_dump.json"
with open(dest, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2, default=str)
print("wrote", dest)
# quick totals
r1p = r1f = r2p = r2f = total = 0
for m, d in out["modules"].items():
    total += int(d["tc"] or 0)
    r1p += int(d["r1"]["P"] or 0)
    r1f += int(d["r1"]["F"] or 0)
    r2p += int(d["r2"]["P"] or 0)
    r2f += int(d["r2"]["F"] or 0)
    print(m, "TC", d["tc"], "R1", d["r1"], "R2", d["r2"], "failsR1", len(d["fails_r1"]))
print("TOTAL", total, "R1", r1p, r1f, "R2", r2p, r2f)
