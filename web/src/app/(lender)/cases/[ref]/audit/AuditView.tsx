"use client";

import { usePathname, useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import {
  AuditTimeline,
  Button,
  Checkbox,
  DateField,
  Dialog,
  EmptyState,
  FilterChip,
  Icon,
  Menu,
  Pagination,
  Tag,
  buttonClasses,
  type AuditEvent,
  type AuditKind,
} from "@/components/ui";
import { apiSend, isApiError } from "@/lib/api/client";
import type { AuditVerifyData, CaseAuditData, CaseAuditRow } from "@/lib/api/audit";
import { cn } from "@/lib/cn";
import { formatNumber } from "@/lib/format";

export interface AuditFilters {
  /** Comma-separated category keys (API `type`). */
  type: string;
  actor: string;
  blocked: boolean;
  from: string;
  to: string;
}

const KIND_BY_TONE: Record<CaseAuditRow["tone"], AuditKind> = {
  err: "blocked",
  info: "transition",
  warn: "reveal",
  ok: "upload",
  neutral: "neutral",
};

function toEvent(r: CaseAuditRow): AuditEvent {
  const kind: AuditKind = r.blocked ? "blocked" : r.type === "case.draft_created" ? "create" : KIND_BY_TONE[r.tone] ?? "neutral";
  const detail = [
    r.reason ? `«${r.reason}»` : null,
    r.detail,
    r.ipMasked ? `IP ${r.ipMasked}` : null,
  ].filter(Boolean) as string[];
  return {
    id: String(r.seq),
    at: r.occurredAt,
    kind,
    icon: r.icon,
    title: r.blocked ? <>{r.title} <span className="sr-only">(محاولة محجوبة)</span></> : r.title,
    actor: r.actor.display ?? (r.actor.type === "system" ? "النظام" : r.actor.label ?? undefined),
    detail:
      detail.length || r.evidence.length ? (
        <span className="flex flex-col gap-1">
          {detail.length ? <span>{detail.join(" · ")}</span> : null}
          {r.evidence.length ? (
            <span className="flex flex-wrap items-center gap-1.5">
              <span className="sr-only">الأدلة:</span>
              {r.evidence.map((e) => (
                <Tag key={e} tone="neutral" icon="attach_file">
                  <bdi dir="ltr" className="font-mono">{e}</bdi>
                </Tag>
              ))}
            </span>
          ) : null}
          <span className="text-12">
            الحدث <bdi dir="ltr" className="font-mono">#{r.seq}</bdi>
          </span>
        </span>
      ) : (
        <span className="text-12">
          الحدث <bdi dir="ltr" className="font-mono">#{r.seq}</bdi>
        </span>
      ),
  };
}

export function AuditView({ reference, data, verify: initialVerify, filters, filterQuery }: {
  reference: string;
  data: CaseAuditData;
  verify: AuditVerifyData | null;
  filters: AuditFilters;
  filterQuery: string;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const [pending, startTransition] = useTransition();
  const [typeOpen, setTypeOpen] = useState(false);
  const [periodOpen, setPeriodOpen] = useState(false);
  const [verify, setVerify] = useState(initialVerify);
  const [verifying, setVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState<string | null>(null);

  const selectedTypes = filters.type ? filters.type.split(",").filter(Boolean) : [];
  const [draftTypes, setDraftTypes] = useState<string[]>(selectedTypes);
  const [draftFrom, setDraftFrom] = useState(filters.from);
  const [draftTo, setDraftTo] = useState(filters.to);

  const catLabel = (k: string) => data.filters.categories.find((c) => c.key === k)?.label ?? k;
  const actorLabel = (k: string) => data.filters.actors.find((a) => a.key === k)?.label ?? k;

  const push = (patch: Partial<Record<keyof AuditFilters, string | null>>) => {
    const next = new URLSearchParams(filterQuery);
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    next.delete("page");
    const qs = next.toString();
    startTransition(() => router.push(qs ? `${pathname}?${qs}` : pathname));
  };

  const pageHref = (p: number) => {
    const next = new URLSearchParams(filterQuery);
    if (p > 1) next.set("page", String(p));
    const qs = next.toString();
    return qs ? `${pathname}?${qs}` : pathname;
  };

  const reverify = async () => {
    setVerifying(true);
    setVerifyError(null);
    try {
      setVerify(await apiSend<AuditVerifyData>("GET", `/cases/${encodeURIComponent(reference)}/audit/verify`));
    } catch (e) {
      setVerifyError(isApiError(e) && e.title ? e.title : "تعذّر التحقق من سلسلة البصمة الآن.");
    } finally {
      setVerifying(false);
    }
  };

  const openPeriod = () => {
    setDraftFrom(filters.from);
    setDraftTo(filters.to);
    setPeriodOpen(true);
  };

  const anyFilter = selectedTypes.length > 0 || filters.actor || filters.blocked || filters.from || filters.to;
  const exportHref = `/api/cases/${encodeURIComponent(reference)}/audit/export${filterQuery ? `?${filterQuery}` : ""}`;
  const events = data.items.map(toEvent);

  return (
    <section aria-labelledby="audit-h" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <h2 id="audit-h" className="m-0 text-20 font-bold">سجل التدقيق</h2>
          <p className="m-0 text-14 text-muted">{data.appendOnlyNote}</p>
        </div>
        {verify ? (
          <Tag tone={verify.ok ? "ok" : "err"} icon={verify.ok ? "verified" : "gpp_bad"}>
            {verify.ok ? "سلسلة البصمة سليمة" : "انقطاع في سلسلة البصمة"}
          </Tag>
        ) : null}
        {data.canExport ? (
          <a href={exportHref} className={buttonClasses({ variant: "secondary" })} download>
            <Icon name="download" size={18} />
            تصدير موقّع (CSV + بصمة)
          </a>
        ) : null}
      </div>

      <div role="group" aria-label="مرشحات السجل" className={cn("flex flex-wrap items-center gap-2", pending && "opacity-70")}>
        {selectedTypes.length > 0 ? (
          <FilterChip
            variant="active"
            label={`النوع: ${selectedTypes.map(catLabel).join("، ")}`}
            onClick={() => {
              setDraftTypes(selectedTypes);
              setTypeOpen(true);
            }}
            onRemove={() => push({ type: null })}
          />
        ) : (
          <FilterChip
            label="النوع"
            expanded={typeOpen}
            onClick={() => {
              setDraftTypes(selectedTypes);
              setTypeOpen(true);
            }}
          />
        )}
        {filters.actor ? (
          <FilterChip variant="active" label={`الفاعل: ${actorLabel(filters.actor)}`} onRemove={() => push({ actor: null })} />
        ) : (
          <Menu
            label="الفاعل"
            trigger={
              <>
                الفاعل
                <Icon name="expand_more" size={16} />
              </>
            }
            triggerClassName="inline-flex min-h-9 items-center gap-1.5 rounded-pill border border-line-strong bg-white px-3 text-13 font-semibold text-ink hover:bg-subtle"
            items={[
              { key: "owner", label: "المالك", onSelect: () => push({ actor: "owner" }) },
              { key: "system", label: "النظام", onSelect: () => push({ actor: "system" }) },
              ...data.filters.actors
                .filter((a) => a.type === "user" || a.type === "provider")
                .map((a) => ({ key: a.key, label: a.label, onSelect: () => push({ actor: a.key }) })),
            ]}
          />
        )}
        {filters.from || filters.to ? (
          <FilterChip
            variant="active"
            label={
              <>
                الفترة: <bdi dir="ltr">{filters.from || "…"} – {filters.to || "…"}</bdi>
              </>
            }
            onClick={openPeriod}
            onRemove={() => push({ from: null, to: null })}
          />
        ) : (
          <FilterChip label="الفترة" expanded={periodOpen} onClick={openPeriod} />
        )}
        {filters.blocked ? (
          <FilterChip variant="active" label="المحاولات المحجوبة فقط" onRemove={() => push({ blocked: null })} />
        ) : (
          <FilterChip variant="action" icon="block" label="المحاولات المحجوبة فقط" onClick={() => push({ blocked: "true" })} />
        )}
        {anyFilter ? (
          <Button variant="text" size="sm" onClick={() => startTransition(() => router.push(pathname))}>
            مسح المرشحات
          </Button>
        ) : null}
        <span className="ms-auto text-13 text-muted" aria-live="polite">
          <bdi dir="ltr">{formatNumber(data.total)}</bdi> حدثاً
        </span>
      </div>

      {events.length === 0 ? (
        <EmptyState
          icon="history"
          title={anyFilter ? "لا أحداث تطابق المرشحات" : "لا أحداث مسجلة لهذه الحالة بعد"}
          body={anyFilter ? "أزل مرشحاً أو وسّع الفترة لعرض أحداث أكثر." : undefined}
          action={anyFilter ? <Button variant="secondary" onClick={() => startTransition(() => router.push(pathname))}>مسح المرشحات</Button> : undefined}
        />
      ) : (
        <AuditTimeline events={events} />
      )}

      {data.total > data.pageSize ? <Pagination page={data.page} pageSize={data.pageSize} total={data.total} hrefFor={pageHref} note="الأحدث أولاً" /> : null}

      <div
        className={cn(
          "flex flex-wrap items-center gap-3 rounded-md border px-4 py-3 text-14",
          verify === null ? "border-line bg-warm" : verify.ok ? "border-ok-line bg-ok-bg" : "border-err-line bg-err-bg",
        )}
        role={verify && !verify.ok ? "alert" : "status"}
      >
        <Icon name={verify === null ? "help" : verify.ok ? "verified" : "gpp_bad"} size={20} className={verify === null ? "text-muted" : verify.ok ? "text-ok" : "text-err"} />
        <span className="min-w-0 flex-1">
          {verifyError ?? (verify ? verify.text : "لم نتمكن من التحقق من سلسلة البصمة الآن.")}
        </span>
        <Button variant="secondary" size="sm" icon="sync" loading={verifying} onClick={() => void reverify()}>
          إعادة التحقق
        </Button>
      </div>

      <Dialog
        open={typeOpen}
        onClose={() => setTypeOpen(false)}
        size="sm"
        title="نوع الحدث"
        footer={
          <>
            <Button
              onClick={() => {
                setTypeOpen(false);
                push({ type: draftTypes.join(",") || null });
              }}
            >
              تطبيق
            </Button>
            <Button variant="secondary" onClick={() => setDraftTypes([])}>
              إلغاء التحديد
            </Button>
          </>
        }
      >
        <fieldset className="m-0 flex flex-col gap-3 border-0 p-5">
          <legend className="sr-only">اختر نوعاً أو أكثر</legend>
          {data.filters.categories.map((c) => (
            <Checkbox
              key={c.key}
              label={c.label}
              checked={draftTypes.includes(c.key)}
              onChange={(e) => setDraftTypes((prev) => (e.target.checked ? [...prev, c.key] : prev.filter((x) => x !== c.key)))}
            />
          ))}
        </fieldset>
      </Dialog>

      <Dialog
        open={periodOpen}
        onClose={() => setPeriodOpen(false)}
        size="sm"
        title="الفترة"
        description="التواريخ بتوقيت الرياض."
        footer={
          <Button
            disabled={Boolean(draftFrom && draftTo && draftFrom > draftTo)}
            onClick={() => {
              setPeriodOpen(false);
              push({ from: draftFrom || null, to: draftTo || null });
            }}
          >
            تطبيق
          </Button>
        }
      >
        <div className="flex flex-col gap-4 p-5">
          <DateField label="من" value={draftFrom} onValueChange={setDraftFrom} />
          <DateField
            label="إلى"
            value={draftTo}
            onValueChange={setDraftTo}
            error={draftFrom && draftTo && draftFrom > draftTo ? "تاريخ النهاية قبل البداية." : undefined}
          />
        </div>
      </Dialog>
    </section>
  );
}
