"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState, useTransition } from "react";
import { PageHeader } from "@/components/shell/PageHeader";
import {
  Alert,
  Button,
  CaseTable,
  Dialog,
  EmptyState,
  FilterChip,
  Icon,
  Menu,
  Pagination,
  Select,
  Tabs,
  Textarea,
  buttonClasses,
  useToast,
  type CaseRowData,
} from "@/components/ui";
import { CASE_STATUS_KEYS } from "@/components/ui/tones";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { toSlaTone, type CaseListData } from "@/lib/api/lender";
import { formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

interface Filters {
  q: string;
  status: string;
  sla: string;
  region: string;
  sort: string;
}

const VIEW_LABELS: Array<[keyof CaseListData["counts"], string]> = [
  ["mine", "حالاتي"],
  ["action", "تحتاج إجرائي"],
  ["overdue", "متأخرة"],
  ["expiring", "تنتهي مستنداتها قريباً"],
  ["all", "كل الحالات"],
];

const SLA_OPTIONS = [
  { value: "overdue", label: "متأخرة" },
  { value: "soon", label: "خلال يومين" },
  { value: "paused", label: "موقوفة" },
];

export function CasesView({ data, view, filters, canCreate, canImport, canAssign, canExport }: {
  data: CaseListData;
  view: string;
  filters: Filters;
  canCreate: boolean;
  canImport: boolean;
  canAssign: boolean;
  canExport: boolean;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const { t } = useI18n();
  const [pending, startTransition] = useTransition();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [q, setQ] = useState(filters.q);
  const [reassignOpen, setReassignOpen] = useState(false);

  // Reset the selection whenever a new page of data arrives (render-time state adjustment, no effect).
  const [seenData, setSeenData] = useState(data);
  if (seenData !== data) {
    setSeenData(data);
    setSelected(new Set());
  }

  const push = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    if (!("page" in patch)) next.delete("page");
    startTransition(() => router.push(`/cases?${next}`));
  };

  const rows: CaseRowData[] = useMemo(
    () =>
      data.items.map((c) => ({
        id: c.reference,
        ref: c.reference,
        owner: [c.owner, c.city].filter(Boolean).join(" · "),
        status: c.status,
        nextAction: c.nextAction,
        sla: { tone: toSlaTone(c.slaTone), text: c.slaText },
        outstanding: c.outstanding,
        arrears: c.arrearsInstallments,
        assignee: c.manager,
        href: `/cases/${c.reference}`,
      })),
    [data.items],
  );

  const statusLabel = filters.status
    ? filters.status.split(",").map((s) => t.caseStates[s as keyof typeof t.caseStates] ?? s).join("، ")
    : null;

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="الحالات"
        actions={
          <div className="flex flex-wrap gap-2">
            {canImport ? (
              <Link className={buttonClasses({ variant: "secondary" })} href="/cases/import">
                <Icon name="upload" size={18} />
                استيراد ملف
              </Link>
            ) : null}
            {canCreate ? (
              <Link className={buttonClasses({ variant: "primary" })} href="/cases/new">
                <Icon name="add" size={18} />
                حالة جديدة
              </Link>
            ) : null}
          </div>
        }
      />

      <Tabs
        label="العروض المحفوظة"
        active={view}
        tabs={VIEW_LABELS.map(([key, label]) => ({ key, label, count: data.counts[key], href: `/cases?view=${key}` }))}
      />

      <div className="flex flex-wrap items-center gap-2" role="search" aria-label="بحث وتصفية الحالات">
        <form
          className="flex h-10 w-full items-center gap-2 rounded-sm border border-line-strong bg-white px-3 sm:w-72"
          onSubmit={(e) => {
            e.preventDefault();
            push({ q: q.trim() || null });
          }}
        >
          <Icon name="search" size={20} className="text-muted" />
          <label htmlFor="case-q" className="sr-only">بحث داخل القائمة</label>
          <input
            id="case-q"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="بحث داخل القائمة (المرجع، المالك، العقد)"
            className="min-w-0 flex-1 bg-transparent text-14 outline-none"
          />
        </form>
        {statusLabel ? <FilterChip variant="active" label={`الحالة: ${statusLabel}`} onRemove={() => push({ status: null })} /> : null}
        <Menu
          label="تصفية حسب الحالة"
          trigger={<>الحالة<Icon name="expand_more" size={18} /></>}
          triggerClassName="inline-flex min-h-9 items-center gap-1 rounded-pill border border-line-strong bg-white px-3 text-14"
          items={CASE_STATUS_KEYS.filter((k) => k !== "draft").map((k) => ({
            key: k,
            label: t.caseStates[k as keyof typeof t.caseStates] ?? k,
            checked: filters.status.split(",").includes(k),
            onSelect: () => {
              const set = new Set(filters.status ? filters.status.split(",") : []);
              if (set.has(k)) set.delete(k);
              else set.add(k);
              push({ status: [...set].join(",") || null });
            },
          }))}
        />
        {filters.sla ? (
          <FilterChip variant="active" label={`المهلة: ${SLA_OPTIONS.find((o) => o.value === filters.sla)?.label}`} onRemove={() => push({ sla: null })} />
        ) : (
          <Menu
            label="تصفية حسب المهلة"
            trigger={<>المهلة<Icon name="expand_more" size={18} /></>}
            triggerClassName="inline-flex min-h-9 items-center gap-1 rounded-pill border border-line-strong bg-white px-3 text-14"
            items={SLA_OPTIONS.map((o) => ({ key: o.value, label: o.label, onSelect: () => push({ sla: o.value }) }))}
          />
        )}
        <Menu
          label="الترتيب"
          trigger={<><Icon name="sort" size={18} />{filters.sort === "amount" ? "الأعلى مبلغاً" : filters.sort === "recent" ? "الأحدث تغيراً" : "أقرب مهلة"}</>}
          triggerClassName="inline-flex min-h-9 items-center gap-1 rounded-pill border border-line-strong bg-white px-3 text-14"
          items={[
            { key: "due", label: "أقرب مهلة", onSelect: () => push({ sort: null }), checked: !filters.sort },
            { key: "amount", label: "الأعلى مبلغاً", onSelect: () => push({ sort: "amount" }), checked: filters.sort === "amount" },
            { key: "recent", label: "الأحدث تغيراً", onSelect: () => push({ sort: "recent" }), checked: filters.sort === "recent" },
          ]}
        />
      </div>

      {selected.size > 0 ? (
        <div role="region" aria-label="إجراءات جماعية" className="flex flex-wrap items-center gap-3 rounded-md border border-ink bg-ink px-4 py-2 text-white">
          <span className="text-14 font-semibold">
            {selected.size === 1 ? "حُددت حالة واحدة" : selected.size === 2 ? "حُددت حالتان" : `حُددت ${formatNumber(selected.size)} حالات`}
          </span>
          {canAssign ? (
            <Button variant="inverse" size="sm" icon="person_add" review onClick={() => setReassignOpen(true)}>
              إعادة إسناد
            </Button>
          ) : null}
          {canExport ? (
            <a className={buttonClasses({ variant: "inverse", size: "sm" })} href={`/api/cases/export?view=all&refs=${[...selected].join(",")}`} download>
              <Icon name="download" size={18} />
              تصدير مخفي البيانات
            </a>
          ) : null}
          <span className="text-13 text-inv-2">الإجراءات الحساسة غير متاحة جماعياً</span>
          <button type="button" className="ms-auto text-14 text-white underline" onClick={() => setSelected(new Set())}>
            إلغاء التحديد
          </button>
        </div>
      ) : null}

      <div aria-busy={pending} className={pending ? "opacity-60 transition-opacity" : undefined}>
        {rows.length === 0 ? (
          <EmptyState
            icon="inbox"
            title="لا توجد حالات تطابق المرشحات"
            body="جرّب إزالة مرشح أو غيّر العرض المحفوظ."
            action={filters.status || filters.sla || filters.q ? <Button variant="secondary" onClick={() => push({ status: null, sla: null, q: null })}>مسح المرشحات</Button> : undefined}
          />
        ) : (
          <CaseTable rows={rows} label="قائمة الحالات" selectable={canAssign || canExport} selected={selected} onSelectedChange={setSelected} />
        )}
      </div>

      <Pagination
        page={data.page}
        pageSize={data.pageSize}
        total={data.total}
        onPageChange={(p) => push({ page: String(p) })}
        note={filters.sort === "amount" ? "مرتبة حسب المبلغ" : "مرتبة حسب أقرب مهلة"}
      />

      <ReassignDialog open={reassignOpen} onClose={() => setReassignOpen(false)} references={[...selected]} onDone={() => router.refresh()} />
    </div>
  );
}

function ReassignDialog({ open, onClose, references, onDone }: { open: boolean; onClose: () => void; references: string[]; onDone: () => void }) {
  const [members, setMembers] = useState<Array<{ id: string; name: string; team: string | null }>>([]);
  const [manager, setManager] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const key = useIdempotencyKey();
  const toast = useToast();

  useEffect(() => {
    if (!open) return;
    apiSend<Array<{ id: string; name: string; team: string | null }>>("GET", "/org/members?role=case_manager")
      .then(setMembers)
      .catch(() => setError("تعذّر تحميل قائمة المسؤولين."));
  }, [open]);

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<{ updated: number }>("POST", "/cases/reassign", { references, managerMembershipId: manager, reason }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: `أُعيد إسناد ${formatNumber(res.updated)} حالة.` });
      onClose();
      onDone();
    } catch (e) {
      setError(isApiError(e) ? e.title : "تعذّر إعادة الإسناد.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="إعادة إسناد الحالات"
      description={`${formatNumber(references.length)} حالة محددة. يُسجَّل السبب في سجل كل حالة.`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={submit} loading={busy} disabled={!manager || reason.trim().length < 3}>تأكيد إعادة الإسناد</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alert tone="err" title={error} /> : null}
        <Select
          label="المسؤول الجديد"
          requiredMark
          value={manager}
          onChange={(e) => setManager(e.target.value)}
          placeholder="اختر"
          options={members.map((m) => ({ value: m.id, label: m.team ? `${m.name} · ${m.team}` : m.name }))}
        />
        <Textarea label="السبب" requiredMark value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} />
      </div>
    </Dialog>
  );
}
