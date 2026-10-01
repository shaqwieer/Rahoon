"""Converts a REGA developers workbook (sheet «المطورون», as collected from REGA's official Wafi pages) into the
organization-directory dataset format read by `import-directory --file`.

Usage: python scripts/rega_xlsx_to_csv.py REGA_Developers_2026-10-01.xlsx server/src/Rahoon.Api/Seed/Data/rega-developers-2026-10-01.csv

Only rows of licence type «مطور عقاري» with status Active are kept. Each row keeps its own REGA page URL as the source.
The item key is licence number + name, because REGA lists some licence numbers under several names."""
import csv, datetime, re, sys

import openpyxl

SOURCE_NAME = "الهيئة العامة للعقار — المنشآت المؤهلة (وافي)"


def main(src: str, dst: str) -> None:
    ws = openpyxl.load_workbook(src, read_only=True).worksheets[0]
    collected = None
    header_seen = False
    out = []
    for row in ws.iter_rows(values_only=True):
        if not row or all(c is None for c in row):
            continue
        if collected is None and isinstance(row[0], str) and "تاريخ الجمع" in row[0]:
            m = re.search(r"\d{4}-\d{2}-\d{2}", row[0])
            collected = m.group(0) if m else None
        if row[0] == "م":
            header_seen = True
            continue
        if not header_seen or row[0] is None:
            continue
        _, name, licence, _region, status, kind, _start, _end, _page, url, *_ = row
        if kind != "مطور عقاري" or str(status).strip().lower() != "active":
            continue
        licence = str(licence).strip().lstrip("0")
        name = " ".join(str(name).split())
        out.append({
            "name_ar": name, "name_en": "", "types": "developer", "website": "", "license_number": licence,
            "registration_number": "", "source_name": SOURCE_NAME, "source_url": url,
            "verified_on": collected or datetime.date.today().isoformat(), "item_key": f"{licence}:{name}",
        })
    with open(dst, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(out[0].keys()))
        w.writeheader()
        w.writerows(out)
    print(f"{len(out)} developers written to {dst} (collected {collected})")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
