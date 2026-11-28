#!/usr/bin/env python3
"""Xuất kết quả Integration Test Batch 1 (Auth & HR nền) vào Report-5.2_Integration Test.xlsx.

  python tests/scripts/export_it_report.py --round 1 [--batch N]   # tạo sheet Batch1_Auth_HR từ sheet mẫu, điền Round 1
  python tests/scripts/export_it_report.py --round 2   # chỉ điền cột Round 2 (+ Defect IDs nếu có file map)
  python tests/scripts/export_it_report.py --suite st --batch N --round 1|2   # System Test → Report-5.3_System Test.xlsx
    (mã ca ST_M<N>_NNN, spec docs/ST_TESTCASE/ST_M<N>_*.md, kết quả docs/ST_RESULTS/ST_M<N>_Round<R>.json,
     ảnh lỗi docs/ST_RESULTS/screenshots/<CaseID>.png được chèn vào cột Note; sheet mẫu "Workflow Name1" của
     Report-5.3_System Test_FRs.xlsx được nhân bản thành sheet Module<N>_<Tên>)

  python tests/scripts/export_it_report.py --type nfr --round 1|2 [--batch N]   # NFR → Report-5.4_Non-Functional Test.xlsx
    (mã ca NFR_G<N>_NNN, spec docs/ST_NFR_TESTCASE/ST_NFR_TestCases_Group<N>_*.md, kết quả docs/ST_RESULTS/NFR_G<N>_Round<R>.json,
     chỉ số docs/ST_RESULTS/nfr_metrics/<CaseID>.json ghép vào cột Note; bỏ --batch = mọi nhóm đã có kết quả của Round đó.
     File 5.4 chưa có thì tạo từ Report-5.3_System Test_FRs.xlsx, sheet Group<N>_<Tên> nhân bản từ "Workflow Name1")

Nguyên tắc: mở trực tiếp workbook hiện có (không tạo file mới), nhân bản sheet mẫu bằng copy_worksheet để giữ style,
không đụng tới sheet mẫu gốc. Round 2 / Round 3 / Defect IDs của Round 1 để trống.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import sys
from copy import copy
from pathlib import Path

from openpyxl import load_workbook
from openpyxl.formatting.rule import CellIsRule
from openpyxl.styles import Font, PatternFill
from openpyxl.worksheet.datavalidation import DataValidation

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "tests" / "docs"
WORKBOOK = DOCS / "Report-5.2_Integration Test.xlsx"
RESULTS_DIR = DOCS / "IT_RESULTS"
ST_WORKBOOK = DOCS / "Report-5.3_System Test_FRs.xlsx"
ST_RESULTS_DIR = DOCS / "ST_RESULTS"
NFR_WORKBOOK = DOCS / "Report-5.4_Non-Functional Test.xlsx"
NFR_SPEC_DIR = DOCS / "ST_NFR_TESTCASE"
NFR_METRICS_DIR = ST_RESULTS_DIR / "nfr_metrics"

BATCHES = {
    1: dict(
        key="B1", spec_name="IT_B1_TestCases_Auth_HR.md", sheet="Batch1_Auth_HR", expected=212,
        title="Batch 1 — Auth & HR nền",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): đăng nhập/hồ sơ, tra cứu, RBAC, nhân viên, tài khoản, "
            "tổ & chức vụ, UX_Employee_AttendanceCode, bậc thợ. Dữ liệu nền S0 (16 nhân viên, 5 tổ, bậc thợ, RJ snapshots)."
        ),
        precondition="Dữ liệu nền S0; Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced={"IT_B1_151"},  # race condition 2/6 lần chạy lại bị 500 → khóa Failed theo yêu cầu
        stats_row=11, tc_first_row=9,
    ),
    2: dict(
        key="B2", spec_name="IT_B2_TestCases_Master_Catalog.md", sheet="Batch2_Master_Catalog", expected=200,
        title="Batch 2 — Master Catalog",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): Customer, Supplier, Material, Product (ảnh, định mức, "
            "nhập Excel), Vehicle; RBAC 4 prefix, ràng buộc UNIQUE/CHECK, AuditLogs, 13 ca đồng thời (chạy lặp 6 lần). Dữ liệu nền S0 của Batch 2."
        ),
        precondition="Dữ liệu nền S0 (Batch 2); Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced=set(),  # 13 ca đồng thời: 6/6 lần chạy đều Passed
        stats_row=12, tc_first_row=None,  # None = nối tiếp sau dòng cuối đang có
    ),
    3: dict(
        key="B3", spec_name="IT_B3_TestCases_DinhMuc_CongDoan.md", sheet="Batch3_DinhMuc_CongDoan", expected=171,
        title="Batch 3 — Định mức mã hàng & Công đoạn",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): phân quyền chéo Admin/Accountant, định mức mã hàng (FK ghép theo "
            "ProductId), nhập Excel định mức / công đoạn, trigger ProductMarker / PieceRate, UX_Operation_FinalStep, UQ_PieceRate, luồng duyệt đơn giá, "
            "các ca đồng thời (chạy lặp 6 lần). Dữ liệu nền S0 của Batch 3."
        ),
        precondition="Dữ liệu nền S0 (Batch 3); Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced=set(),  # chỉ 1 lần chạy chốt: không có ca đồng thời nào chập chờn giữa các lần chạy (017, 078, 161 Failed ổn định)
        stats_row=13, tc_first_row=None,
    ),
    4: dict(
        key="B4", spec_name="IT_B4_TestCases_DonHang_PhanTo_NVL.md", sheet="Batch4_DonHang_PhanTo_NVL", expected=172,
        title="Batch 4 — Đơn hàng khách, Phân tổ & NVL đơn",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): phân quyền chéo Admin/Manager/WarehouseKeeper, tạo/sửa/chuyển trạng thái đơn "
            "(UQ_OrderLine, RowVer), phân tổ may (UQ_OrderLineTeam), bìa của đơn (WidthM, trigger ProductMarker), NVL của đơn (cột computed, UQ_OM), lịch giao "
            "(UQ_DSD_Order), import Excel, vw_OrderLineProgress, các ca đồng thời (chạy lặp 6 lần). Dữ liệu nền S0 của Batch 4."
        ),
        precondition="Dữ liệu nền S0 (Batch 4); Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced=set(),  # ca đồng thời 094/095/096 Failed ổn định (409 TEAM_ORDER_BUSY); các ca còn lại Passed, không chập chờn
        stats_row=14, tc_first_row=None,
    ),
    5: dict(
        key="B5", spec_name="IT_B5_TestCases_MuaHang.md", sheet="Batch5_Mua_hang", expected=122,
        title="Batch 5 — Mua hàng",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): phân quyền chéo Admin/Manager/Accountant/WarehouseKeeper, nhà cung cấp, "
            "tạo đơn mua (UQ_POL, UQ_POL_Stock, NVL mới), đặt hàng / hủy / đổi giá (RowVer), thanh toán nhiều lần (CK_PP_Amount, cửa sổ 24 giờ), "
            "lịch giao (UQ_DSD_PurchaseOrder), fault-injection rollback, các ca đồng thời (chạy lặp 6 lần). Dữ liệu nền S0 của Batch 5."
        ),
        precondition="Dữ liệu nền S0 (Batch 5); Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced=set(),  # 2 lần chạy toàn batch cho cùng danh sách 42 ca Failed: không có ca đồng thời chập chờn
        stats_row=15, tc_first_row=None,
    ),
    6: dict(
        key="B6", spec_name="IT_B6_TestCases_NhapKho_ThieuGiao.md", sheet="Batch6_Nhap_kho_Thieu_giao", expected=125,
        title="Batch 6 — Nhập kho NVL & thiếu giao",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): phân quyền chéo Admin/Manager/WarehouseKeeper, lập / sửa phiếu nhập NVL "
            "(UQ_MRL, UQ_MRL_Stock, CK_MRL_Target), xác nhận / trả lại phiếu (thẻ kho append-only, usp_Stock_Post), phiếu bổ sung, xử lý thiếu giao "
            "(SHORT_DELIVERY, UX_Request_Receipt), IncomingMaterials, export Excel, Attachments (upload, xóa mềm, IDOR), fault-injection rollback và "
            "các ca đồng thời (chạy lặp 6 lần). Dữ liệu nền S0 của Batch 6."
        ),
        precondition="Dữ liệu nền S0 (Batch 6); Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced={"IT_B6_047"},  # 047 chập chờn giữa các lần chạy (Failed 2/3 lần trước, Passed lần chốt) → khóa Failed
        stats_row=16, tc_first_row=None,
    ),
    7: dict(
        key="B7", spec_name="IT_B7_TestCases_LenhCat_BanCat_BanGiao.md", sheet="Batch7_LenhCat_BanCat_BanGiao", expected=133,
        title="Batch 7 — Lệnh cắt & bàn cắt & bàn giao BTP",
        requirement=(
            "Integration Test API → Service → Test DB (SQL Server localhost): phân quyền Manager/TeamLeader/WarehouseKeeper và [CuttingTeamOnly], cách ly dữ liệu theo tổ, "
            "lập / sửa / đóng thiếu / hủy lệnh cắt, lệnh cắt bù, kiểm vải, bàn cắt (UQ_CL, UQ_CLL, UQ_CLF), bàn giao BTP (tạo, hủy, xác nhận, từ chối, thẻ kho), "
            "view vw_WorkOrderLineProgress, fault-injection rollback và các ca đồng thời (chạy lặp 6 lần). Dữ liệu nền S0 của Batch 7."
        ),
        precondition="Dữ liệu nền S0 (Batch 7); Test DB thật; mỗi ca tự Rollback / dọn dữ liệu",
        race_forced=set(),  # ca đồng thời 042/057/062/109/125/133 Failed ổn định, không có ca chập chờn
        stats_row=16, tc_first_row=None,
    ),
}
CFG: dict = {}
SUITE = "it"


def st_config(n: int) -> dict:
    """Cấu hình module System Test: tên lấy từ tests/docs/ST_MODULES.md, spec ở docs/ST_TESTCASE/ST_M<N>_*.md."""
    name = f"Module {n}"
    modules = DOCS / "ST_MODULES.md"
    if modules.exists():
        for line in modules.read_text(encoding="utf-8").splitlines():
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if len(cells) >= 5 and cells[0] == str(n):
                name = cells[1]
                break
    found = sorted((DOCS / "ST_TESTCASE").glob(f"ST_M{n}_*.md")) if (DOCS / "ST_TESTCASE").exists() else []
    return dict(
        pfx="ST", key=f"M{n}", spec_name=found[0].name if found else f"ST_M{n}_TestCases.md",
        sheet=(f"Module{n}_" + (found[0].stem.split("TestCases_", 1)[-1] if found else "x"))[:31],
        expected=None, title=f"Module {n} — {name}", short=name,
        requirement=f"System Test E2E (Playwright, Chromium) trên giao diện Frontend: {name}. Phạm vi Frontend theo tests/docs/ST_MODULES.md.",
        precondition="Dữ liệu tiền tố ST_AUTO_; phiên đăng nhập theo Role; mỗi ca tự dọn dữ liệu",
        race_forced=set(), stats_row=None, tc_first_row=None,
    )


def nfr_config(n: int) -> dict:
    """Cấu hình nhóm NFR: tên lấy từ file spec docs/ST_NFR_TESTCASE/ST_NFR_TestCases_Group<N>_*.md."""
    found = sorted(NFR_SPEC_DIR.glob(f"ST_NFR_TestCases_Group{n}_*.md"))
    if not found:
        sys.exit(f"Không thấy spec ST_NFR_TestCases_Group{n}_*.md trong {NFR_SPEC_DIR}")
    name = found[0].stem.split("TestCases_", 1)[1]  # Group1_Performance
    short = name.split("_", 1)[-1]
    return dict(
        pfx="NFR", key=f"G{n}", spec_name=found[0].name, sheet=name[:31],
        expected=None, title=f"NFR Group {n} — {short}", short=short,
        requirement=f"Non-Functional System Test (nhóm {short}) theo tests/docs/ST_NFR_TARGETS.md: NBomber / HttpClient / Playwright trên môi trường test.",
        precondition="Môi trường test riêng, DB reset + seed theo nhóm; dữ liệu tiền tố NFR_AUTO_, dọn sau khi chạy",
        race_forced=set(), stats_row=None, tc_first_row=None,
    )


def st_stats_row() -> int:
    """Dòng Test Statistics của module ST: dòng đã trỏ tới sheet này, hoặc dòng trống đầu tiên từ 11."""
    return 11  # được tính lại trong update_summary_sheets
SHEET_NAME = ""
TEMPLATE_SHEET = "Feature Name1"
RACE_FORCED_FAILED: set = set()
FAILED_MD = RESULTS_DIR
SPEC_CANDIDATES: list = []
FEATURE_TITLE = FEATURE_REQUIREMENT = ""


def select_batch(n: int) -> None:
    global CFG, SHEET_NAME, RACE_FORCED_FAILED, FAILED_MD, SPEC_CANDIDATES, FEATURE_TITLE, FEATURE_REQUIREMENT
    global RESULTS_DIR
    if SUITE in ("st", "nfr"):
        RESULTS_DIR = ST_RESULTS_DIR
        CFG = st_config(n) if SUITE == "st" else nfr_config(n)
    else:
        CFG = BATCHES[n]
        CFG.setdefault("pfx", "IT")
    SHEET_NAME, RACE_FORCED_FAILED = CFG["sheet"], CFG["race_forced"]
    global TEMPLATE_SHEET
    TEMPLATE_SHEET = "Feature Name1" if SUITE == "it" else "Workflow Name1"
    FAILED_MD = RESULTS_DIR / f"{CFG['pfx']}_{CFG['key']}_Round1_Failed.md"
    spec_dir = {"it": "IT_TESTCASE", "st": "ST_TESTCASE", "nfr": "ST_NFR_TESTCASE"}[SUITE]
    SPEC_CANDIDATES = [DOCS / CFG["spec_name"], DOCS / spec_dir / CFG["spec_name"]]
    FEATURE_TITLE, FEATURE_REQUIREMENT = CFG["title"], CFG["requirement"]

FIRST_DATA_ROW = 12
GROUP_ROW_TEMPLATE = 11   # "Function A": hàng tiêu đề nhóm trong sheet mẫu
CASE_ROW_TEMPLATE = 13    # hàng ca thường (hàng 12 chỉ khác chiều cao)
COLS = {"id": 1, "desc": 2, "proc": 3, "expected": 4, "pre": 5, "r1": 6, "r1date": 7, "r1tester": 8,
        "r2": 9, "r2date": 10, "r2tester": 11, "r3": 12, "r3date": 13, "r3tester": 14, "defect": 15, "note": 16}
LINE_HEIGHT = 12.75
MAX_ROW_HEIGHT = 409.0

CATEGORY = {
    "A": "Lệch mã HTTP", "B": "Thiếu validation / quy tắc nghiệp vụ", "C": "Lỗi 500 / race",
    "D": "Rò rỉ dữ liệu / bảo mật", "E": "Lệch cấu trúc / audit", "A/B": "Lệch mã HTTP + thiếu quy tắc",
}


# ----------------------------------------------------------------------------- đọc nguồn

def clean(text: str) -> str:
    """Bỏ ký hiệu Markdown (backtick, **), đổi <br> thành xuống dòng."""
    text = re.sub(r"<br\s*/?>", "\n", text)
    return text.replace("`", "").replace("**", "").strip()


def find_spec() -> Path:
    for p in SPEC_CANDIDATES:
        if p.exists():
            return p
    sys.exit(f"Không tìm thấy {CFG['spec_name']} trong: " + ", ".join(map(str, SPEC_CANDIDATES)))


def parse_spec(path: Path) -> list[dict]:
    """Trả về các ca IT_B1_xxx theo thứ tự xuất hiện, kèm nhóm chức năng (## hoặc ### nếu mục có tiểu mục)."""
    cases, h2, h3 = [], "", ""
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("### "):
            h3 = line[4:].strip()
        elif line.startswith("## "):
            h2, h3 = line[3:].strip(), ""
        m = re.match(rf"^\|\s*({CFG['pfx']}_{CFG['key']}_\d{{3}})\b[^|]*\|", line)  # ô ID có thể kèm nhãn **[Đ1]**
        if not m:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) != 6 - 1 + 0 and len(cells) < 5:
            sys.exit(f"{m.group(1)}: dòng bảng không đủ cột: {line[:80]}")
        # ID | Pre | Input | Expected | Type — nếu dư cột (ký tự | nằm trong nội dung) thì phần dư thuộc cột Expected.
        cid, pre, inp, typ = m.group(1), cells[1], cells[2], cells[-1]
        expected = "|".join(cells[3:-1])
        cases.append({
            "id": cid, "pre": clean(pre), "input": clean(inp), "expected": clean(expected), "type": typ,
            "group": re.sub(r"`", "", h3 or h2 or (CFG.get("short") or "")),
        })
    return cases


def nfr_target_names() -> dict[str, str]:
    """G1-01 → tên mục tiêu, đọc từ bảng trong ST_NFR_TARGETS.md (dùng làm tiêu đề nhóm ca)."""
    path, names = DOCS / "ST_NFR_TARGETS.md", {}
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            m = re.match(r"^\|\s*(G\d+-\d+)\s*\|\s*([^|]+?)\s*\|", line)
            if m:
                names[m.group(1)] = clean(m.group(2))
    return names


def parse_nfr_spec(path: Path) -> list[dict]:
    """Bảng NFR: TC ID | Mục tiêu | Loại | Endpoint / Màn hình | Kịch bản | Kết quả mong đợi."""
    lines = path.read_text(encoding="utf-8").splitlines()
    env = next((clean(l.split(":**", 1)[1]) for l in lines if l.startswith("> **Môi trường")), CFG["precondition"])
    names, cases = nfr_target_names(), []
    for line in lines:
        m = re.match(rf"^\|\s*({CFG['pfx']}_{CFG['key']}_\d{{3}})\b", line)
        if not m:
            continue
        cells = [c.strip().replace("\\|", "|") for c in re.split(r"(?<!\\)\|", line.strip().strip("|"))]
        if len(cells) != 6:
            sys.exit(f"{m.group(1)}: cần 6 cột, đọc được {len(cells)}: {line[:80]}")
        _, target, typ, endpoint, scenario, expected = cells
        cases.append({
            "id": m.group(1), "pre": env, "input": clean(scenario), "expected": clean(expected), "type": typ,
            "group": f"{target} — {names[target]}" if target in names else clean(target),
            "endpoint": clean(endpoint),
        })
    return cases


def metric_note(cid: str) -> str:
    """Chỉ số thực đo của ca (nếu có file nfr_metrics/<CaseID>.json) để ghép vào cột Note."""
    path = NFR_METRICS_DIR / f"{cid}.json"
    if not path.exists():
        return ""
    try:
        m = json.loads(path.read_text(encoding="utf-8-sig"))
        if m["case_id"] != cid:
            sys.exit(f"{path.name}: case_id là {m['case_id']}, không khớp tên file")
        ok, fail = m["ok_count"], m["fail_count"]
        rate = f"{fail * 100 / (ok + fail):.2f}%" if ok + fail else "không có request"
        note = (f"[Số đo] p95={m['p95_ms']:g} ms; RPS={m['rps']:g}; Error Rate={rate} ({fail}/{ok + fail}); "
                f"p50={m['p50_ms']:g} ms; max={m['max_ms']:g} ms; VU={m['vu']}; thời lượng={m['duration_sec']:g} s")
    except (json.JSONDecodeError, KeyError, TypeError, ValueError) as ex:
        sys.exit(f"File chỉ số hỏng {path}: {ex!r}")
    return f"{note}. {m['extra_note']}" if m.get("extra_note") else note


def load_results(round_no: int) -> dict[str, str]:
    path = RESULTS_DIR / f"{CFG['pfx']}_{CFG['key']}_Round{round_no}.json"
    if not path.exists():
        sys.exit(f"Thiếu file kết quả: {path}")
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    return {row["Id"]: row["Outcome"] for row in data}


def parse_failed_notes(path: Path, cases: list[dict] | None = None) -> dict[str, str]:
    """IT_B1_Round1_Failed.md: | Ca | Nhóm | Spec | Thực tế |  →  Note ngắn gọn.
    Dạng 2 cột do parse_trx.py sinh (| Ca | Thông báo lỗi |): Spec lấy từ Expected Results của ca, Thực tế = thông báo lỗi của test."""
    notes: dict[str, str] = {}
    by_id = {c["id"]: c for c in cases or []}
    for line in path.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\|\s*(\d{3})\s*\|", line)
        if not m:
            continue
        parts = [p.strip() for p in line.strip().strip("|").split(" | ")]
        if len(parts) == 2:
            cid = f"{CFG['pfx']}_{CFG['key']}_{parts[0]}"
            case = by_id[cid]
            spec = " ".join(case["expected"].split())
            label = "Vi phạm SLA/Threshold" if SUITE == "nfr" else "Lệch Spec"
            notes[cid] = f"[{label}] Spec: {spec}. Thực tế (app): {clean(parts[1].replace(chr(92) + '|', '|'))}"
            shot = ST_RESULTS_DIR / "screenshots" / f"{cid}.png"
            if SUITE != "it" and shot.exists():
                notes[cid] += f" Ảnh lỗi: tests/docs/ST_RESULTS/screenshots/{cid}.png"
            continue
        if len(parts) < 4:
            sys.exit(f"Dòng Failed không đủ 4 cột: {line[:80]}")
        num, group, spec, actual = parts[0], parts[1], parts[2], " | ".join(parts[3:])
        label = CATEGORY.get(group, group)
        notes[f"{CFG['pfx']}_{CFG['key']}_{num}"] = f"[{label}] Spec: {clean(spec)}. Thực tế: {clean(actual)}."
    return notes


# ----------------------------------------------------------------------------- style / chiều cao

def copy_style(src, dst) -> None:
    dst.font, dst.fill, dst.border = copy(src.font), copy(src.fill), copy(src.border)
    dst.alignment, dst.number_format, dst.protection = copy(src.alignment), src.number_format, copy(src.protection)


def wrap_style(cell, vertical: str | None = None) -> None:
    al = copy(cell.alignment)
    al.wrap_text = True
    if vertical:
        al.vertical = vertical
    cell.alignment = al


def estimate_lines(text, width: float) -> int:
    if text is None or text == "":
        return 1
    usable = max(width * 1.05, 4)  # ký tự Tahoma 10 ≈ 1 đơn vị độ rộng cột
    return sum(max(1, math.ceil(len(par) / usable)) for par in str(text).split("\n"))


def fit_row_height(ws, row: int) -> None:
    lines = 1
    for col in range(1, 17):
        letter = ws.cell(row, col).column_letter
        width = ws.column_dimensions[letter].width or 9.0
        lines = max(lines, estimate_lines(ws.cell(row, col).value, width))
    ws.row_dimensions[row].height = min(MAX_ROW_HEIGHT, max(15.0, lines * LINE_HEIGHT + 3))


# ----------------------------------------------------------------------------- Round 1

def rebuild_validations(src_ws, dst_ws, last_row: int) -> None:
    """copy_worksheet không sao chép data validation: tạo lại, mở rộng vùng F/I/L tới dòng cuối."""
    for dv in src_ws.data_validations.dataValidation:
        sq = " ".join(str(r) for r in dv.sqref.ranges)
        for col in "FIL":
            sq = re.sub(rf"\b{col}{FIRST_DATA_ROW}:{col}\d+\b", f"{col}{FIRST_DATA_ROW}:{col}{last_row}", sq)
        new = DataValidation(type=dv.type, formula1=dv.formula1, formula2=dv.formula2, allow_blank=dv.allow_blank,
                             showErrorMessage=dv.showErrorMessage, showInputMessage=dv.showInputMessage,
                             errorTitle=dv.errorTitle, error=dv.error, promptTitle=dv.promptTitle, prompt=dv.prompt,
                             operator=dv.operator, errorStyle=dv.errorStyle)
        new.sqref = sq
        dst_ws.add_data_validation(new)


def color_results(ws) -> None:
    """Passed nền xanh, Failed nền đỏ ở cột kết quả Round 1/2/3. copy_worksheet không sao chép conditional formatting."""
    for text, font, fill in (("Passed", "FF006100", "FFC6EFCE"), ("Failed", "FF9C0006", "FFFFC7CE")):
        ws.conditional_formatting.add("F10:F998 I10:I998 L10:L998", CellIsRule(
            operator="equal", formula=[f'"{text}"'], font=Font(color=font),
            fill=PatternFill(fill_type="solid", start_color=fill, end_color=fill)))


def build_sheet(wb, cases: list[dict], results: dict[str, str], notes: dict[str, str]):
    if SHEET_NAME in wb.sheetnames:
        existing = wb[SHEET_NAME]
        has_round2 = any(existing.cell(r, COLS["r2"]).value not in (None, "", "Pending")
                         for r in range(FIRST_DATA_ROW, existing.max_row + 1))
        if has_round2:
            sys.exit(f"Sheet {SHEET_NAME} đã có dữ liệu Round 2; dừng để không ghi đè. Xóa tay sheet nếu thật sự muốn tạo lại.")
        del wb[SHEET_NAME]

    template = wb[TEMPLATE_SHEET]
    ws = wb.copy_worksheet(template)
    ws.title = SHEET_NAME
    sheet_prefix = {"it": "Batch", "st": "Module", "nfr": "Group"}[SUITE]
    others = [n for n in wb.sheetnames if n.startswith(sheet_prefix) and n != SHEET_NAME]
    anchor = wb.sheetnames.index(others[-1]) if others else wb.sheetnames.index("Test Statistics")
    wb.move_sheet(ws, offset=(anchor + 1) - wb.sheetnames.index(SHEET_NAME))

    # Style mẫu: lấy trước khi xóa dữ liệu mẫu.
    group_style = {c: ws.cell(GROUP_ROW_TEMPLATE, c) for c in range(1, 17)}
    case_style = {c: ws.cell(CASE_ROW_TEMPLATE, c) for c in range(1, 17)}
    group_snap = {c: (copy(x.font), copy(x.fill), copy(x.border), copy(x.alignment), x.number_format) for c, x in group_style.items()}
    case_snap = {c: (copy(x.font), copy(x.fill), copy(x.border), copy(x.alignment), x.number_format) for c, x in case_style.items()}

    ws.delete_rows(GROUP_ROW_TEMPLATE, ws.max_row)
    for r in list(ws.row_dimensions.keys()):
        if r >= GROUP_ROW_TEMPLATE:
            del ws.row_dimensions[r]

    ws["B2"], ws["B3"] = FEATURE_TITLE, FEATURE_REQUIREMENT
    last_row = GROUP_ROW_TEMPLATE + len(cases) + len({c["group"] for c in cases}) - 1
    ws["B4"] = f'=COUNTIF(A{GROUP_ROW_TEMPLATE}:A998,"{CFG['pfx']}_{CFG['key']}_*")'
    # Round 2/3 trong sheet mẫu đều đếm cột F (Round 1); sheet này đếm đúng cột I / L.
    for row, col in ((6, "F"), (7, "I"), (8, "L")):
        for k in "BCDE":
            ws[f"{k}{row}"] = f"=COUNTIF(${col}10:${col}998,{k}5)"
    ws.freeze_panes = template.freeze_panes

    def paint(row, snap):
        for c, (font, fill, border, align, fmt) in snap.items():
            cell = ws.cell(row, c)
            cell.font, cell.fill, cell.border, cell.alignment, cell.number_format = copy(font), copy(fill), copy(border), copy(align), fmt

    row, current_group = GROUP_ROW_TEMPLATE, None
    for case in cases:
        if case["group"] != current_group:
            current_group = case["group"]
            paint(row, group_snap)
            ws.cell(row, 1).value = current_group
            ws.row_dimensions[row].height = template.row_dimensions[GROUP_ROW_TEMPLATE].height
            row += 1
        paint(row, case_snap)
        outcome = "Failed" if case["id"] in RACE_FORCED_FAILED else results[case["id"]]
        values = {
            COLS["id"]: case["id"],
            COLS["desc"]: "\n".join(filter(None, [case["group"], case.get("endpoint"), f'Loại: {case["type"]}'])),
            COLS["proc"]: case["input"],
            COLS["expected"]: case["expected"],
            COLS["pre"]: case["pre"],
            COLS["r1"]: outcome,
        }
        note = notes[case["id"]] if outcome == "Failed" else ""
        if SUITE == "nfr":
            note = "\n".join(filter(None, [note, metric_note(case["id"])]))
        if note:
            values[COLS["note"]] = note
        for c, v in values.items():
            ws.cell(row, c).value = v
        for c in range(1, 17):
            wrap_style(ws.cell(row, c), vertical="top")
        fit_row_height(ws, row)
        row += 1

    assert row - 1 == last_row, (row - 1, last_row)
    rebuild_validations(template, ws, last_row)
    color_results(ws)
    return ws


def update_summary_sheets(wb, ws, cases: list[dict]) -> None:
    """Test Statistics: dòng 11 trỏ sang sheet mới. Test Cases: liệt kê các nhóm chức năng của Batch 1."""
    if SUITE in ("st", "nfr"):
        update_summary_st(wb, cases)
        return
    stats = wb["Test Statistics"]
    q = f"'{SHEET_NAME}'"
    sr = CFG["stats_row"]
    if sr is None:  # ST: dùng lại dòng đã trỏ tới sheet này, nếu không thì dòng trống đầu tiên
        sr = next((r for r in range(11, 200) if stats[f"C{r}"].value in (f"={q}!B2", None, "Test coverage")), 11)
        for r in range(11, sr):
            pass
        if stats[f"C{sr}"].value in (None,):
            stats[f"B{sr}"] = sr - 10
    if stats[f"C{sr}"].value == "Test coverage":  # hết chỗ cho batch mới: dời 2 dòng tổng kết xuống 1 dòng, giữ style
        for r in (sr + 1, sr):
            for col in range(1, 10):
                copy_style(stats.cell(r, col), stats.cell(r + 1, col))
                stats.cell(r + 1, col).value = stats.cell(r, col).value
        for col in range(1, 10):
            copy_style(stats.cell(sr - 1, col), stats.cell(sr, col))
            stats.cell(sr, col).value = None
        d, e, g, h = (f"{c}11:{c}{sr}" for c in "DEGH")
        stats[f"E{sr + 1}"] = f"=(SUM({d})+SUM({e}))*100/(SUM({h})-SUM({g}))"
        stats[f"E{sr + 2}"] = f"=SUM({d})*100/(SUM({h})-SUM({g}))"
    stats[f"C{sr}"], stats[f"D{sr}"], stats[f"E{sr}"] = f"={q}!B2", f"={q}!B6", f"={q}!C6"
    stats[f"F{sr}"], stats[f"G{sr}"], stats[f"H{sr}"] = f"={q}!D6", f"={q}!E6", f"={q}!B4"

    tc = wb["Test Cases"]
    groups: list[tuple[str, list[str]]] = []
    for c in cases:
        if not groups or groups[-1][0] != c["group"]:
            groups.append((c["group"], []))
        groups[-1][1].append(c["id"])
    style_row = 13
    first = CFG["tc_first_row"]
    if first is None:  # nối tiếp danh sách các batch trước, đánh số tiếp
        first = max((r for r in range(9, tc.max_row + 1) if tc.cell(r, 2).value not in (None, "")), default=8) + 1
        first = max(first, 9)
    base_no = int(tc.cell(first - 1, 2).value) if first > 9 else 0
    for i, (name, ids) in enumerate(groups):
        r = first + i
        for col in range(2, 7):
            if r > 13:
                copy_style(tc.cell(style_row, col), tc.cell(r, col))
        tc.cell(r, 2).value = str(base_no + i + 1)
        tc.cell(r, 3).value = name
        tc.cell(r, 4).value = SHEET_NAME
        tc.cell(r, 5).value = f"{len(ids)} ca: {ids[0]} → {ids[-1]}"
        tc.cell(r, 6).value = CFG["precondition"]
        for col in range(2, 7):
            wrap_style(tc.cell(r, col))


ST_CAPACITY = 15


def update_summary_st(wb, cases: list[dict]) -> None:
    """Report-5.3 (FRs): Test Statistics 11.. (tối đa 15 module, khối Sub total dời xuống dưới) và Test Cases."""
    stats = wb["Test Statistics"]
    q = f"'{SHEET_NAME}'"
    sub_row = 11 + ST_CAPACITY + 1
    if stats["C14"].value == "Sub total":
        snap = {r: [(c.value, copy(c.font), copy(c.fill), copy(c.border), copy(c.alignment), c.number_format) for c in stats[r][:8]]
                for r in (11, 14, 16, 17)}
        for r in range(11, 18):
            for col in range(2, 9):
                stats.cell(r, col).value = None
        for k in range(ST_CAPACITY):
            for col, (_, f, fl, b, a, nf) in enumerate(snap[11], start=1):
                c = stats.cell(11 + k, col)
                c.font, c.fill, c.border, c.alignment, c.number_format = copy(f), copy(fl), copy(b), copy(a), nf
        for src, dst in ((14, sub_row), (16, sub_row + 2), (17, sub_row + 3)):
            for col, (v, f, fl, b, a, nf) in enumerate(snap[src], start=1):
                c = stats.cell(dst, col)
                c.value = v
                c.font, c.fill, c.border, c.alignment, c.number_format = copy(f), copy(fl), copy(b), copy(a), nf
        for col in "DEFGH":
            stats[f"{col}{sub_row}"] = f"=SUM({col}11:{col}{sub_row - 1})"
        stats[f"E{sub_row + 2}"] = f"=(D{sub_row}+E{sub_row})*100/(H{sub_row}-G{sub_row})"
        stats[f"E{sub_row + 3}"] = f"=D{sub_row}*100/(H{sub_row}-G{sub_row})"
    row = next((r for r in range(11, sub_row) if stats[f"C{r}"].value in (None, f"={q}!B2")), None)
    if row is None:
        sys.exit("Test Statistics hết chỗ cho module mới")
    stats[f"B{row}"] = row - 10
    stats[f"C{row}"], stats[f"D{row}"], stats[f"E{row}"] = f"={q}!B2", f"={q}!B6", f"={q}!C6"
    stats[f"F{row}"], stats[f"G{row}"], stats[f"H{row}"] = f"={q}!D6", f"={q}!E6", f"={q}!B4"

    tc = wb["Test Cases"]
    if str(tc["C9"].value or "").startswith("Workflow "):
        for r in range(9, 14):
            for col in range(2, 7):
                tc.cell(r, col).value = None
    existing = [r for r in range(9, tc.max_row + 1) if tc.cell(r, 4).value == SHEET_NAME]
    first = existing[0] if existing else max((r for r in range(9, tc.max_row + 1) if tc.cell(r, 2).value not in (None, "")), default=8) + 1
    base_no = first - 9
    groups: list[tuple[str, list[str]]] = []
    for c in cases:
        if not groups or groups[-1][0] != c["group"]:
            groups.append((c["group"], []))
        groups[-1][1].append(c["id"])
    for i, (name, ids) in enumerate(groups):
        r = first + i
        for col in range(2, 7):
            if r > 13:
                copy_style(tc.cell(13, col), tc.cell(r, col))
        tc.cell(r, 2).value = str(base_no + i + 1)
        tc.cell(r, 3).value = name
        tc.cell(r, 4).value = SHEET_NAME
        tc.cell(r, 5).value = f"{len(ids)} ca: {ids[0]} → {ids[-1]}"
        tc.cell(r, 6).value = CFG["precondition"]
        for col in range(2, 7):
            wrap_style(tc.cell(r, col))


# ----------------------------------------------------------------------------- Round 2

def append_st_round2_note(ws, row: int, cid: str, result: str) -> None:
    """Giữ nguyên Note Round 1, nối thêm đoạn [Round 2]; chạy lại thì thay đoạn cũ."""
    cell = ws.cell(row, COLS["note"])
    old = str(cell.value or "").split("[Round 2]")[0].rstrip()
    measured = metric_note(cid) if SUITE == "nfr" else ""
    if result == "Failed":
        md = RESULTS_DIR / f"{CFG['pfx']}_{CFG['key']}_Round2_Failed.md"
        short = cid.rsplit("_", 1)[1]
        m = re.search(rf"^\| {short} \| (.*) \|\s*$", md.read_text(encoding="utf-8"), re.M)
        if not m:
            sys.exit(f"Ca {cid} Failed ở Round 2 chưa có Note trong {md.name}")
        new = f"[Round 2] {m.group(1)}. Ảnh lỗi: tests/docs/ST_RESULTS/screenshots/round2/{cid}.png"
    elif old or measured:
        new = "[Round 2] Passed"
    else:
        return
    if measured:
        new += "\n" + measured
    cell.value = "\n".join(["[Round 1] " + old.removeprefix("[Round 1] "), new]) if old else new


def fill_round2(wb) -> None:
    if SHEET_NAME not in wb.sheetnames:
        sys.exit(f"Chưa có sheet {SHEET_NAME}: chạy --round 1 trước.")
    ws = wb[SHEET_NAME]
    results = load_results(2)
    defects_path = RESULTS_DIR / f"{CFG['pfx']}_{CFG['key']}_Defects.json"  # tùy chọn: {"IT_B1_017": "BUG-001", ...}
    defects = json.loads(defects_path.read_text(encoding="utf-8")) if defects_path.exists() else {}
    filled = 0
    for r in range(FIRST_DATA_ROW, ws.max_row + 1):
        cid = ws.cell(r, COLS["id"]).value
        if not (isinstance(cid, str) and cid.startswith(f"{CFG['pfx']}_{CFG['key']}_")):
            continue
        if cid in results:
            ws.cell(r, COLS["r2"]).value = results[cid]
            filled += 1
            if SUITE != "it":
                append_st_round2_note(ws, r, cid, results[cid])
        if cid in defects:
            ws.cell(r, COLS["defect"]).value = defects[cid]
    print(f"Round 2: đã điền {filled} ca, {len(defects)} Defect ID.")


# ----------------------------------------------------------------------------- Report-5.3

def create_st_workbook(path: Path) -> None:
    """Tạo Report-5.3 từ template Report-5.2: bỏ sheet Batch*, xóa dữ liệu IT trong Test Statistics / Test Cases."""
    wb = load_workbook(WORKBOOK)
    for name in [n for n in wb.sheetnames if n.startswith("Batch")]:
        del wb[name]
    stats = wb["Test Statistics"]
    for r in range(11, 20):
        for col in range(2, 9):
            stats.cell(r, col).value = None
    tc = wb["Test Cases"]
    for r in range(9, tc.max_row + 1):
        for col in range(2, 7):
            tc.cell(r, col).value = None
    wb.save(path)
    print("Đã tạo từ template:", path)


# ----------------------------------------------------------------------------- Report-5.4

def new_nfr_workbook():
    """Report-5.4 chưa có: lấy định dạng từ Report-5.3 (giữ sheet mẫu Workflow Name*), bỏ dữ liệu System Test. Chỉ lưu khi xuất xong."""
    if not ST_WORKBOOK.exists():
        sys.exit(f"Không thấy template {ST_WORKBOOK}")
    wb = load_workbook(ST_WORKBOOK)
    for name in [n for n in wb.sheetnames if n.startswith("Module")]:
        del wb[name]
    wb["Cover"]["B2"] = "NON-FUNCTIONAL TEST REPORT DOCUMENT"
    stats = wb["Test Statistics"]
    stats["B1"] = "NON-FUNCTIONAL TEST STATISTICS"
    for r in range(11, 11 + ST_CAPACITY + 1):
        for col in range(2, 9):
            stats.cell(r, col).value = None
    tc = wb["Test Cases"]
    for r in range(9, tc.max_row + 1):
        for col in range(2, 7):
            tc.cell(r, col).value = None
    return wb


def nfr_groups_with_results(round_no: int) -> list[int]:
    found = (re.fullmatch(rf"NFR_G(\d+)_Round{round_no}\.json", p.name) for p in ST_RESULTS_DIR.glob("NFR_G*.json"))
    return sorted(int(m.group(1)) for m in found if m)


def export_one(wb, round_no: int) -> None:
    if round_no != 1:
        fill_round2(wb)
        return
    cases = (parse_nfr_spec if SUITE == "nfr" else parse_spec)(find_spec())
    ids = [c["id"] for c in cases]
    n = CFG["expected"] or len(set(ids))
    if not ids or len(ids) != n or len(set(ids)) != n:
        sys.exit(f"Cần đủ {n} ca duy nhất, đọc được {len(ids)} ({len(set(ids))} duy nhất).")
    results = load_results(1)
    missing = [i for i in ids if i not in results]
    if missing:
        sys.exit("Thiếu kết quả cho: " + ", ".join(missing))
    failed = {i for i in ids if results[i] == "Failed"} | RACE_FORCED_FAILED
    notes = parse_failed_notes(FAILED_MD, cases) if failed else {}
    if failed - set(notes):
        sys.exit("Ca Failed chưa có Note: " + ", ".join(sorted(failed - set(notes))))
    ws = build_sheet(wb, cases, results, notes)
    update_summary_sheets(wb, ws, cases)
    print(f"{SHEET_NAME} Round 1: {len(ids)} ca, Failed={len(failed)}, Passed={len(ids) - len(failed)}.")


# ----------------------------------------------------------------------------- main

def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # console Windows mặc định cp1252
    ap =argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--round", type=int, choices=(1, 2), required=True)
    ap.add_argument("--suite", "--type", dest="suite", choices=("it", "st", "nfr"), default="it",
                    help="it = Integration (Report-5.2), st = System (Report-5.3), nfr = Non-Functional (Report-5.4)")
    ap.add_argument("--batch", type=int, default=None, help="số Batch (it), Module (st) hoặc Group (nfr)")
    ap.add_argument("--workbook", type=Path, default=None)
    args = ap.parse_args()

    global SUITE
    SUITE = args.suite
    if SUITE == "nfr":
        batches = [args.batch] if args.batch is not None else nfr_groups_with_results(args.round)
        if not batches:
            sys.exit(f"Chưa có file NFR_G*_Round{args.round}.json trong {ST_RESULTS_DIR}")
    else:
        batches = [1 if args.batch is None else args.batch]
    if SUITE == "it" and batches[0] not in BATCHES:
        sys.exit(f"--batch phải thuộc {sorted(BATCHES)}")
    if args.workbook is None:
        args.workbook = {"it": WORKBOOK, "st": ST_WORKBOOK, "nfr": NFR_WORKBOOK}[SUITE]
    if SUITE == "st" and not args.workbook.exists():
        sys.exit(f"Không thấy template {args.workbook}")
    if SUITE == "nfr" and not args.workbook.exists():
        if args.round != 1:
            sys.exit(f"Chưa có {args.workbook.name}: chạy --round 1 trước.")
        wb = new_nfr_workbook()
    else:
        wb = load_workbook(args.workbook)  # mở trực tiếp file hiện có (không tạo mới)
    for n in batches:  # chỉ lưu khi mọi nhóm đều xuất được
        select_batch(n)
        export_one(wb, args.round)
    wb.save(args.workbook)
    print("Đã lưu:", args.workbook)


if __name__ == "__main__":
    main()
