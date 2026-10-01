"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import { Icon } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import { loadDirectory, matchesOrg, ORG_TYPE_LABELS, type DirectoryKind, type DirectoryOrg } from "@/lib/market/directory";

export interface OrgChoice {
  /** Directory id, or null when the person typed a name that is not in the directory. */
  id: string | null;
  name: string;
}

const MAX_SHOWN = 40;

/**
 * Searchable choice of a real organization from the directory (developers, or banks and finance companies), with
 * «غير موجودة في الدليل» to type a name instead. Listing in the directory says nothing about partnership with Rahoon.
 */
export function OrgPicker({
  kind,
  label,
  value,
  notInList,
  onChange,
  error,
  optional,
  allowOther = true,
}: {
  kind: DirectoryKind;
  label: string;
  value: OrgChoice | null;
  notInList?: boolean;
  /** `notInList` true: the person types the name (value.name), value.id is null. */
  onChange: (value: OrgChoice | null, notInList: boolean) => void;
  error?: string;
  optional?: boolean;
  /** false: only directory organizations can be chosen (no typed name). */
  allowOther?: boolean;
}) {
  const id = useId();
  const listId = `${id}-list`;
  const [orgs, setOrgs] = useState<DirectoryOrg[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const otherLabel = kind === "developer" ? "المطور غير موجود في الدليل" : "الجهة غير موجودة في الدليل";

  useEffect(() => {
    let live = true;
    loadDirectory(kind).then((r) => live && setOrgs(r)).catch(() => live && setFailed(true));
    return () => {
      live = false;
    };
  }, [kind]);

  const matches = useMemo(() => (orgs ?? []).filter((o) => matchesOrg(o, query)), [orgs, query]);
  const shown = matches.slice(0, MAX_SHOWN);
  // The last row is always «not in the directory».
  const rows = shown.length + (allowOther ? 1 : 0);

  const choose = (index: number) => {
    if (index >= shown.length) {
      onChange({ id: null, name: value && !value.id ? value.name : query.trim() }, true);
    } else {
      const o = shown[index];
      onChange({ id: o.id, name: o.nameAr }, false);
    }
    setOpen(false);
    setQuery("");
  };

  const selected = value?.id ? value : null;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-15 font-semibold">
        {label} {optional ? <span className="text-13 font-normal text-muted">(اختياري)</span> : null}
      </label>
      {selected && !notInList ? (
        <div className={cn("flex min-h-12 items-center gap-2 rounded-sm border bg-white px-3", error ? "border-err" : "border-line-strong")}>
          <Icon name="domain" size={20} className="text-muted" />
          <span className="flex-1 text-16">{selected.name}</span>
          <button
            type="button"
            className="min-h-10 rounded-sm px-2 text-14 text-rust underline"
            onClick={() => {
              onChange(null, false);
              window.setTimeout(() => inputRef.current?.focus(), 0);
            }}
          >
            تغيير
          </button>
        </div>
      ) : (
        <div className="relative">
          <input
            ref={inputRef}
            id={id}
            role="combobox"
            aria-expanded={open}
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={open ? `${id}-opt-${active}` : undefined}
            aria-invalid={Boolean(error) || undefined}
            autoComplete="off"
            placeholder={orgs ? "ابحث بالاسم بالعربي أو بالإنجليزي" : failed ? "تعذّر تحميل الدليل — اختر «غير موجودة» واكتب الاسم" : "جارٍ تحميل الدليل…"}
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setOpen(true);
              setActive(0);
            }}
            onFocus={() => setOpen(true)}
            onBlur={() => window.setTimeout(() => setOpen(false), 150)}
            onKeyDown={(e) => {
              if (rows === 0) return;
              if (e.key === "ArrowDown") {
                e.preventDefault();
                setOpen(true);
                setActive((a) => Math.min(a + 1, rows - 1));
              } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setActive((a) => Math.max(a - 1, 0));
              } else if (e.key === "Enter" && open) {
                e.preventDefault();
                choose(active);
              } else if (e.key === "Escape") {
                setOpen(false);
              }
            }}
            className={cn(
              "min-h-12 w-full rounded-sm border bg-white px-3 text-16 outline-none focus:border-rust",
              error ? "border-err" : "border-line-strong",
            )}
          />
          {open ? (
            <ul id={listId} role="listbox" aria-label={label} className="absolute inset-x-0 top-full z-30 mt-1 max-h-72 overflow-auto rounded-sm border border-line bg-white p-1 shadow-lg">
              {shown.map((o, i) => (
                <li
                  key={o.id}
                  id={`${id}-opt-${i}`}
                  role="option"
                  aria-selected={i === active}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => choose(i)}
                  className={cn("flex cursor-pointer flex-col rounded-xs px-3 py-2", i === active ? "bg-rust-50" : "hover:bg-subtle")}
                >
                  <span className="text-15">{o.nameAr}</span>
                  <span className="text-12 text-muted">
                    {[o.nameEn, ...o.types.map((t) => ORG_TYPE_LABELS[t] ?? t)].filter(Boolean).join(" · ")}
                  </span>
                </li>
              ))}
              {orgs && matches.length > shown.length ? (
                <li className="px-3 py-2 text-12 text-muted" aria-hidden="true">
                  {`و${matches.length - shown.length} أخرى — اكتب جزءًا أطول من الاسم`}
                </li>
              ) : null}
              {orgs && matches.length === 0 && query ? (
                <li className="px-3 py-2 text-13 text-muted" aria-hidden="true">
                  لا نتائج في الدليل لهذا البحث.
                </li>
              ) : null}
              {allowOther ? (
              <li
                id={`${id}-opt-${shown.length}`}
                role="option"
                aria-selected={active === shown.length}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => choose(shown.length)}
                className={cn("cursor-pointer rounded-xs border-t border-divider px-3 py-2 text-15 text-rust", active === shown.length ? "bg-rust-50" : "hover:bg-subtle")}
              >
                {otherLabel}
              </li>
              ) : null}
            </ul>
          ) : null}
        </div>
      )}
      {notInList ? (
        <div className="flex flex-col gap-1.5">
          <label htmlFor={`${id}-other`} className="text-14">
            اكتب الاسم
          </label>
          <div className="flex gap-2">
            <input
              id={`${id}-other`}
              value={value?.name ?? ""}
              onChange={(e) => onChange({ id: null, name: e.target.value }, true)}
              className={cn("min-h-12 flex-1 rounded-sm border bg-white px-3 text-16 outline-none focus:border-rust", error ? "border-err" : "border-line-strong")}
            />
            <button type="button" className="min-h-12 rounded-sm px-3 text-14 text-rust underline" onClick={() => onChange(null, false)}>
              البحث في الدليل
            </button>
          </div>
        </div>
      ) : null}
      <span className="text-12 text-muted">الدليل يضم جهات حقيقية من مصادر رسمية؛ وجود الجهة فيه لا يعني شراكة مع رهون أو موافقتها على نقل العقد.</span>
      {error ? <span className="text-13 text-err">{error}</span> : null}
    </div>
  );
}
