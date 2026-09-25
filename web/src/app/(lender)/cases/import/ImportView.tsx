"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { LoadFailure } from "@/components/case/LoadFailure";
import { PageHeader } from "@/components/shell/PageHeader";
import {
  Alert,
  Button,
  DateText,
  Dialog,
  EmptyState,
  Icon,
  KpiTile,
  Pagination,
  RadioCardGroup,
  Tabs,
  Tag,
  Textarea,
  UploadDropzone,
  buttonClasses,
  useToast,
  type TabDef,
} from "@/components/ui";
import type { Tone } from "@/components/ui/tones";
import { apiSend, apiUpload, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type {
  ImportBatchDetail,
  ImportBatchListItem,
  ImportBatchStatus,
  ImportBatchSummary,
  ImportCommitResult,
  ImportDecision,
  ImportRowView,
} from "@/lib/api/imports";
import { formatNumber } from "@/lib/format";

const BATCH_STATUS: Record<ImportBatchStatus, { label: string; tone: Tone }> = {
  validated: { label: "بانتظار القرار والاستيراد", tone: "warn" },
  importing: { label: "جارٍ الاستيراد", tone: "info" },
  completed: { label: "اكتمل الاستيراد", tone: "ok" },
  failed: { label: "تعذّر الاستيراد", tone: "err" },
};

const DECISION_LABEL: Record<ImportDecision, string> = {
  skip: "تخطي",
  link: "ربط بالحالة القائمة",
  create_with_reason: "إنشاء مع سبب",
};

const DECISION_DESC: Record<ImportDecision, string> = {
  skip: "لا يُستورد الصف.",
  link: "يُسجَّل الصف على الحالة المفتوحة للعقد نفسه دون إنشاء حالة جديدة ودون تعديل بياناتها.",
  create_with_reason: "تُنشأ مسودة جديدة رغم التكرار، ويُحفظ السبب في السجل.",
};

type Failure = { ok: false; kind: "forbidden" | "error"; status: number; errorRef: string; title: string | null };

export function ImportView({ batches, detail, detailFailure, status, canCommit }: {
  batches: ImportBatchListItem[];
  detail: ImportBatchDetail | null;
  detailFailure: Failure | null;
  status: string;
  canCommit: boolean;
}) {
  const b = detail?.batch;
  const step = b ? b.step : 1;
  const stepLabel = step === 1 ? "الرفع" : step === 2 ? "التحقق" : "النتيجة";

  return (
    <div className="flex flex-col gap-2">
      <PageHeader
        title="استيراد الحالات"
        description={<>الخطوة <bdi dir="ltr">{step}</bdi> من <bdi dir="ltr">3</bdi> · {stepLabel}</>}
        actions={
          <a href="/api/cases/imports/template" download className={buttonClasses({ variant: "secondary" })}>
            <Icon name="download" size={18} />
            تنزيل القالب (v3)
          </a>
        }
      />
      <div className="grid items-start gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="flex min-w-0 flex-col gap-5">
          {detailFailure ? <LoadFailure state={detailFailure} /> : b && detail ? <BatchPanel detail={detail} status={status} canCommit={canCommit} /> : <UploadStep />}
        </div>
        <RecentBatches batches={batches} currentId={b?.id ?? null} />
      </div>
    </div>
  );
}

/* ───────── Step 1: upload ───────── */

function UploadStep({ replacing }: { replacing?: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);

  const upload = async (f: File) => {
    setFile(f);
    setError(null);
    if (!f.name.toLowerCase().endsWith(".csv")) {
      setError("الصيغة المقبولة CSV (UTF-8). احفظ ملف Excel بصيغة CSV باستخدام القالب.");
      return;
    }
    setBusy(true);
    try {
      const fd = new FormData();
      fd.append("file", f);
      const res = await apiUpload<{ batch: ImportBatchSummary }>("/cases/imports", fd);
      router.push(`/cases/import?batch=${res.batch.id}`);
    } catch (e) {
      setError(isApiError(e) ? (e.fieldError("file") ?? (e.title || "تعذّر رفع الملف.")) : "تعذّر رفع الملف.");
      setBusy(false);
    }
  };

  return (
    <section aria-labelledby="up-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-col gap-1">
        <h2 id="up-h" className="m-0 text-18 font-semibold">{replacing ? "رفع ملف بديل" : "رفع ملف الحالات"}</h2>
        <p className="m-0 text-14 text-muted">
          استخدم <a href="/api/cases/imports/template" download>القالب v3</a>. يُتحقق من كل صف قبل الاستيراد: الجاهز يُستورد، والتكرار المحتمل يحتاج قراراً، والخطأ لا يُستورد وتظهر طريقة إصلاحه.
        </p>
      </div>
      {replacing ? (
        <Alert tone="info" role="none" compact>
          يُنشئ الملف البديل دفعة تحقق جديدة؛ تبقى الدفعة «{replacing}» كما هي ولا يُستورد منها شيء ما لم تستوردها.
        </Alert>
      ) : null}
      <UploadDropzone accept=".csv,text/csv" disabled={busy} onFiles={(fs) => fs[0] && void upload(fs[0])} hint="CSV بترميز UTF-8 · حتى 5 م.ب و5,000 صف · فاصلة أو فاصلة منقوطة" />
      {busy ? (
        <p role="status" className="m-0 flex items-center gap-2 text-14">
          <Icon name="progress_activity" size={18} className="animate-rh-spin" />
          جارٍ رفع «{file?.name}» والتحقق من الصفوف…
        </p>
      ) : null}
      {error ? <Alert tone="err" title="لم يُقبل الملف">{error}</Alert> : null}
      <p className="m-0 flex items-center gap-1.5 text-13 text-muted">
        <Icon name="info" size={16} />
        الحالات المستوردة تبدأ «مسودة» ولا يُرسل أي تواصل للمالك
      </p>
    </section>
  );
}

/* ───────── Steps 2–3: batch summary, rows, decisions, commit ───────── */

function BatchPanel({ detail, status, canCommit }: { detail: ImportBatchDetail; status: string; canCommit: boolean }) {
  const router = useRouter();
  const toast = useToast();
  const commitKey = useIdempotencyKey();
  const b = detail.batch;
  const c = b.counts;
  const open = b.status === "validated";
  const [replace, setReplace] = useState(false);
  const [decisionRow, setDecisionRow] = useState<ImportRowView | null>(null);
  const [confirm, setConfirm] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [commitError, setCommitError] = useState<string | null>(null);
  const [created, setCreated] = useState<string[] | null>(null);

  const base = `/cases/import?batch=${b.id}`;
  const tabs: TabDef[] = [
    { key: "attention", label: "تحتاج انتباهاً", href: base },
    { key: "duplicate", label: "تكرار محتمل", count: c.duplicates, href: `${base}&status=duplicate` },
    { key: "error", label: "أخطاء", count: c.errors, countTone: c.errors > 0 ? "err" : "neutral", href: `${base}&status=error` },
    ...(open ? [{ key: "ready", label: "جاهزة", count: c.ready, href: `${base}&status=ready` }] : []),
    ...(!open ? [{ key: "imported", label: "مستوردة", count: c.imported, href: `${base}&status=imported` }] : []),
    ...(!open ? [{ key: "skipped", label: "متخطاة", count: c.skipped, href: `${base}&status=skipped` }] : []),
    { key: "all", label: "كل الصفوف", count: c.total, href: `${base}&status=all` },
  ];

  const commit = async () => {
    setCommitting(true);
    setCommitError(null);
    try {
      const res = await apiSend<ImportCommitResult>("POST", `/cases/imports/${b.id}/commit`, {}, { idempotencyKey: commitKey.get() });
      commitKey.reset();
      setConfirm(false);
      if (res.alreadyCommitted) {
        toast.toast({ tone: "info", message: "سبق استيراد هذه الدفعة؛ لم تُنشأ حالات جديدة." });
      } else {
        setCreated(res.created);
        toast.toast({ tone: "ok", message: `استُوردت ${formatNumber(res.created.length)} حالة كمسودات.` });
      }
      router.refresh();
    } catch (e) {
      commitKey.reset();
      setCommitError(isApiError(e) ? e.title || "تعذّر الاستيراد." : "تعذّر الاستيراد. لم يُنشأ شيء؛ أعد المحاولة.");
    } finally {
      setCommitting(false);
    }
  };

  if (replace) {
    return (
      <>
        <UploadStep replacing={b.fileName} />
        <Button variant="text" className="self-start" onClick={() => setReplace(false)}>رجوع للدفعة</Button>
      </>
    );
  }

  return (
    <>
      <section aria-label="الملف" className="flex flex-wrap items-center gap-4 rounded-lg border border-line bg-white p-4">
        <span className="grid size-11 place-items-center rounded-md bg-subtle"><Icon name="table_view" size={24} /></span>
        <div className="flex min-w-0 flex-1 flex-col gap-0.5">
          <strong className="text-16 break-all"><bdi>{b.fileName}</bdi></strong>
          <span className="text-13 text-muted">{b.meta} · <DateText value={b.uploadedAt} mode="datetime" /></span>
        </div>
        <Tag tone={BATCH_STATUS[b.status].tone}>{BATCH_STATUS[b.status].label}</Tag>
        {open ? <Button variant="secondary" icon="upload_file" onClick={() => setReplace(true)}>استبدال الملف</Button> : null}
      </section>

      {open ? (
        <div className="grid gap-3 sm:grid-cols-3">
          <KpiTile icon="check_circle" iconTone="ok" value={formatNumber(c.ready)} label="صف جاهز" />
          <KpiTile icon="content_copy" iconTone="warn" value={formatNumber(c.duplicates)} label="تكرار محتمل · يحتاج قراراً"
            sub={c.undecidedDuplicates > 0 ? <>بلا قرار: <bdi dir="ltr">{formatNumber(c.undecidedDuplicates)}</bdi></> : "اتُّخذ قرار لكل تكرار"} />
          <KpiTile icon="error" iconTone="err" value={formatNumber(c.errors)} label="أخطاء · لن تُستورد" />
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-3">
          <KpiTile icon="task_alt" iconTone="ok" value={formatNumber(c.imported)} label="استُوردت كمسودات" />
          <KpiTile icon="redo" iconTone="charcoal" value={formatNumber(c.skipped)} label="تُخطيت أو رُبطت" />
          <KpiTile icon="error" iconTone="err" value={formatNumber(c.errors + c.undecidedDuplicates)} label="لم تُستورد (أخطاء أو تكرار دون قرار)" />
        </div>
      )}

      {created && created.length > 0 ? (
        <Alert tone="ok" title={`أُنشئت ${formatNumber(created.length)} مسودة`}>
          <span className="flex flex-wrap gap-x-3 gap-y-1">
            {created.slice(0, 20).map((r) => (
              <Link key={r} href={`/cases/new/${r}`}><bdi dir="ltr" className="font-mono">{r}</bdi></Link>
            ))}
            {created.length > 20 ? <span>… وغيرها في قائمة المسودات</span> : null}
          </span>
        </Alert>
      ) : !open ? (
        <Alert tone="ok" role="none" title="اكتمل الاستيراد">
          الحالات المستوردة «مسودة» في <Link href="/cases?view=drafts">قائمة المسودات</Link>، ولم يُرسل أي تواصل للمالك.
        </Alert>
      ) : null}

      <p className="m-0 flex items-center gap-1.5 rounded-md bg-warm px-3 py-2 text-13 text-muted md:hidden">
        <Icon name="desktop_windows" size={16} />
        مراجعة الصفوف والقرارات متاحة على شاشة أكبر؛ يعرض الجوال حالة الاستيراد فقط.
      </p>

      <section aria-labelledby="rows-h" className="hidden flex-col gap-3 md:flex">
        <h2 id="rows-h" className="m-0 text-18 font-semibold">الصفوف التي تحتاج انتباهاً</h2>
        <Tabs label="تصفية الصفوف" tabs={tabs} active={status} />
        {detail.rows.length === 0 ? (
          <EmptyState icon="task_alt" title="لا صفوف في هذا التصنيف" body={status === "attention" ? "كل الصفوف جاهزة للاستيراد." : undefined} headingLevel={3} />
        ) : (
          <div className="overflow-x-auto rounded-lg border border-line bg-white">
            <table className="w-full min-w-[760px] border-collapse text-14" aria-label="الصفوف التي تحتاج انتباهاً">
              <thead>
                <tr className="bg-warm text-muted">
                  <th scope="col" className="w-[70px] px-4 py-2.5 text-start font-semibold">الصف</th>
                  <th scope="col" className="px-3 py-2.5 text-start font-semibold">رقم العقد</th>
                  <th scope="col" className="px-3 py-2.5 text-start font-semibold">الحقل</th>
                  <th scope="col" className="px-3 py-2.5 text-start font-semibold">المشكلة وطريقة الإصلاح</th>
                  <th scope="col" className="px-3 py-2.5 text-start font-semibold">الإجراء</th>
                </tr>
              </thead>
              <tbody>
                {detail.rows.map((r) => (
                  <RowLine key={r.rowNumber} r={r} open={open} onDecide={() => setDecisionRow(r)} />
                ))}
              </tbody>
            </table>
          </div>
        )}
        {detail.total > detail.pageSize ? (
          <Pagination page={detail.page} pageSize={detail.pageSize} total={detail.total}
            hrefFor={(p) => `${base}${status !== "attention" ? `&status=${status}` : ""}${p > 1 ? `&page=${p}` : ""}`} />
        ) : null}
      </section>

      <div className="sticky bottom-0 z-10 -mx-4 flex flex-wrap items-center gap-3 border-t border-line bg-white px-4 py-3 md:-mx-10 md:px-10">
        <a href={`/api/cases/imports/${b.id}/errors.csv`} download className={buttonClasses({ variant: "secondary" })}>
          <Icon name="download" size={18} />
          تنزيل ملف الأخطاء
        </a>
        <span id="imp-note" className="flex-1 text-13 text-muted">{b.note}</span>
        {open && canCommit ? (
          <Button size="lg" softDisabled={c.importable === 0} aria-describedby="imp-note" onClick={() => setConfirm(true)}>
            استيراد {formatNumber(c.importable)} صفاً
          </Button>
        ) : open ? (
          <span className="text-13 text-muted">الاستيراد يتطلب صلاحية إنشاء الحالات.</span>
        ) : null}
      </div>

      {decisionRow ? <DecisionDialog batchId={b.id} row={decisionRow} onClose={() => setDecisionRow(null)} /> : null}

      <Dialog
        open={confirm}
        onClose={() => setConfirm(false)}
        title={`استيراد ${formatNumber(c.importable)} صفاً`}
        description="راجع ما سيحدث قبل التأكيد. الإجراء يُسجَّل في السجل ولا يمكن التراجع عنه من هنا."
        footer={
          <>
            <Button onClick={() => void commit()} loading={committing} disabled={c.importable === 0}>تأكيد الاستيراد</Button>
            <Button variant="secondary" onClick={() => setConfirm(false)}>رجوع</Button>
          </>
        }
      >
        <div className="flex flex-col gap-3 p-5">
          {commitError ? <Alert tone="err" title={commitError} /> : null}
          <ul className="m-0 flex list-none flex-col gap-2.5 p-0 text-15 leading-6">
            <li className="flex gap-2.5"><Icon name="note_add" size={20} className="text-muted" /><span>تُنشأ <bdi dir="ltr">{formatNumber(c.importable)}</bdi> حالة بحالة «مسودة» (الصفوف الجاهزة و«إنشاء مع سبب»).</span></li>
            <li className="flex gap-2.5"><Icon name="notifications_off" size={20} className="text-muted" /><span>لا يُرسل أي تواصل أو دعوة للمالك.</span></li>
            <li className="flex gap-2.5"><Icon name="error" size={20} className="text-muted" /><span>صفوف الأخطاء (<bdi dir="ltr">{formatNumber(c.errors)}</bdi>) لا تُستورد؛ صحّحها وارفعها في ملف جديد.</span></li>
            {c.undecidedDuplicates > 0 ? (
              <li className="flex gap-2.5"><Icon name="content_copy" size={20} className="text-warn" /><span>تكرار دون قرار (<bdi dir="ltr">{formatNumber(c.undecidedDuplicates)}</bdi>) لن يُستورد، ولا تُعدّل قراراته بعد الاستيراد.</span></li>
            ) : null}
          </ul>
        </div>
      </Dialog>
    </>
  );
}

function RowLine({ r, open, onDecide }: { r: ImportRowView; open: boolean; onDecide: () => void }) {
  const issues = r.issues.length ? r.issues : [null];
  return (
    <>
      {issues.map((i, idx) => (
        <tr key={idx} className={idx === 0 ? "border-t border-divider align-top" : "align-top"}>
          {idx === 0 ? (
            <>
              <td rowSpan={issues.length} className="px-4 py-3 font-mono tabular-nums"><bdi dir="ltr">{r.rowNumber}</bdi></td>
              <td rowSpan={issues.length} className="px-3 py-3"><bdi dir="ltr" className="font-mono text-13">{r.contractNumberMasked ?? "—"}</bdi></td>
            </>
          ) : null}
          <td className="px-3 py-3">{i?.fieldLabel ?? "—"}</td>
          <td className="px-3 py-3">
            {i ? (
              <span className="flex gap-2">
                <Icon name={i.severity === "duplicate" ? "content_copy" : "error"} size={18} className={i.severity === "duplicate" ? "text-warn" : "text-err"} />
                <span className="flex flex-col gap-0.5">
                  <span>{i.message}</span>
                  {i.fix ? <span className="text-13 text-muted">الإصلاح: {i.fix}</span> : null}
                </span>
              </span>
            ) : (
              <span className="text-muted">{r.status === "ready" ? "جاهز للاستيراد" : r.status === "imported" ? "استُورد" : r.status === "skipped" ? "تُخطي" : "—"}</span>
            )}
          </td>
          {idx === 0 ? (
            <td rowSpan={issues.length} className="px-3 py-3">
              {r.status === "duplicate" ? (
                <span className="flex flex-col items-start gap-1.5">
                  {r.decision ? (
                    <Tag tone={r.decision === "create_with_reason" ? "info" : "neutral"}>{DECISION_LABEL[r.decision]}</Tag>
                  ) : (
                    <Tag tone="warn">بلا قرار</Tag>
                  )}
                  {r.decisionReason ? <span className="text-12 text-muted">«{r.decisionReason}»</span> : null}
                  {open && r.actions.length > 0 ? (
                    <Button variant="text" size="sm" onClick={onDecide}>{r.decision ? "تغيير القرار" : r.actions.map((a) => DECISION_LABEL[a].split(" ")[0]).join(" / ")}</Button>
                  ) : null}
                </span>
              ) : r.status === "error" ? (
                <span className="text-13 text-muted">صحّح في الملف</span>
              ) : null}
            </td>
          ) : null}
        </tr>
      ))}
    </>
  );
}

function DecisionDialog({ batchId, row, onClose }: { batchId: string; row: ImportRowView; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [decision, setDecision] = useState<ImportDecision | null>(row.decision ?? null);
  const [reason, setReason] = useState(row.decisionReason ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; field?: string } | null>(null);
  const needsReason = decision === "create_with_reason";
  const valid = decision !== null && (!needsReason || reason.trim().length >= 10);

  const save = async () => {
    if (!decision) return;
    setBusy(true);
    setError(null);
    try {
      await apiSend("POST", `/cases/imports/${batchId}/rows/${row.rowNumber}/decision`, { decision, reason: reason.trim() || null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: `سُجّل القرار للصف ${row.rowNumber}: ${DECISION_LABEL[decision]}` });
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      setError(isApiError(e) ? { title: e.title || "تعذّر حفظ القرار.", field: e.fieldError("reason") ?? e.fieldError("decision") } : { title: "تعذّر حفظ القرار." });
    } finally {
      setBusy(false);
    }
  };

  const dup = row.issues.find((i) => i.severity === "duplicate");
  return (
    <Dialog
      open
      onClose={onClose}
      title={<>قرار الصف <bdi dir="ltr">{row.rowNumber}</bdi></>}
      description={dup ? dup.message : undefined}
      footer={
        <>
          <Button onClick={() => void save()} loading={busy} softDisabled={!valid}>حفظ القرار</Button>
          <Button variant="secondary" onClick={onClose}>رجوع</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error.title}>{error.field}</Alert> : null}
        <RadioCardGroup
          legend="القرار"
          name={`decision-${row.rowNumber}`}
          columns={1}
          value={decision}
          onChange={(v) => setDecision(v as ImportDecision)}
          options={row.actions.map((a) => ({ value: a, label: DECISION_LABEL[a], description: DECISION_DESC[a] }))}
        />
        <Textarea
          label={needsReason ? "سبب الإنشاء رغم التكرار" : "ملاحظة"}
          requiredMark={needsReason}
          optionalMark={!needsReason}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={1000}
          help={needsReason ? "10 أحرف على الأقل. يُحفظ في سجل الحالة الجديدة." : undefined}
          error={needsReason && reason.length > 0 && reason.trim().length < 10 ? "السبب 10 أحرف على الأقل." : undefined}
        />
      </div>
    </Dialog>
  );
}

/* ───────── Recent batches ───────── */

function RecentBatches({ batches, currentId }: { batches: ImportBatchListItem[]; currentId: string | null }) {
  return (
    <aside aria-labelledby="recent-h" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 id="recent-h" className="m-0 text-16 font-semibold">آخر الدفعات</h2>
        {currentId ? <Link href="/cases/import" className="text-14 font-semibold">رفع ملف جديد</Link> : null}
      </div>
      {batches.length === 0 ? (
        <p className="m-0 text-14 text-muted">لم تُرفع دفعات بعد.</p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-2 p-0">
          {batches.map((x) => (
            <li key={x.id}>
              <Link
                href={`/cases/import?batch=${x.id}`}
                aria-current={x.id === currentId ? "true" : undefined}
                className={`flex flex-col gap-1 rounded-md border p-3 text-ink no-underline hover:bg-warm ${x.id === currentId ? "bar-start border-ink" : "border-line"}`}
              >
                <strong className="text-14 break-all"><bdi>{x.fileName}</bdi></strong>
                <span className="flex flex-wrap items-center gap-2 text-12 text-muted">
                  <Tag tone={BATCH_STATUS[x.status].tone}>{BATCH_STATUS[x.status].label}</Tag>
                  <DateText value={x.createdAt} mode="datetime" />
                </span>
                <span className="text-12 text-muted">
                  <bdi dir="ltr">{formatNumber(x.totalRows)}</bdi> صفاً · جاهز <bdi dir="ltr">{formatNumber(x.readyCount)}</bdi> · تكرار <bdi dir="ltr">{formatNumber(x.duplicateCount)}</bdi> · أخطاء <bdi dir="ltr">{formatNumber(x.errorCount)}</bdi>
                  {x.status === "completed" ? <> · مستورد <bdi dir="ltr">{formatNumber(x.importedCount)}</bdi></> : null}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}
