"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Button, DateText, Dialog, EmptyState, Icon, KeyValueList, Money, Tag, Textarea, buttonClasses, useToast } from "@/components/ui";
import type { Tone } from "@/components/ui/tones";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { versionFileHref } from "@/lib/api/documents";
import type { TimelineItem, ValuationData, ValuationReportDto } from "@/lib/api/valuation";
import { cn } from "@/lib/cn";
import { formatNumber } from "@/lib/format";

const TIMELINE_TONE: Record<TimelineItem["tone"], string> = {
  ink: "text-ink",
  info: "text-info",
  ok: "text-ok",
  warn: "text-warn",
  err: "text-err",
  muted: "text-muted",
};

const VALIDITY_ICON = { ok: "event_available", warn: "event_upcoming", err: "event_busy" } as const;

/** L11 — valuation summary (value, range, LTV, methodology, validity), review of reports under review, revaluation, assignment timeline. */
export function ValuationView({ reference, data, canAssign, canDownload }: { reference: string; data: ValuationData; canAssign: boolean; canDownload: boolean }) {
  const [reval, setReval] = useState<null | "eligible" | "reason">(null);
  const cur = data.current;
  const r = data.revaluation;

  return (
    <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)]">
      <div className="flex min-w-0 flex-col gap-5">
        {cur ? (
          <ReportCard report={cur} reference={reference} canDownload={canDownload} ltv={data.ltv}>
            {canAssign ? (
              <RevaluationActions revaluation={r} onOpen={setReval} />
            ) : null}
          </ReportCard>
        ) : (
          <EmptyState
            icon="real_estate_agent"
            title="لا يوجد تقرير تقييم معتمد"
            body="يُعتمد التقرير بعد مراجعته. طلب إعادة التقييم ينشئ مهمة للمحلل لتكليف مقيّم."
            action={canAssign ? <RevaluationActions revaluation={r} onOpen={setReval} /> : undefined}
          />
        )}

        {data.underReview.length > 0 ? (
          <section aria-labelledby="ur-h" className="flex flex-col gap-3">
            <h3 id="ur-h" className="m-0 text-17 font-semibold">تقارير قيد المراجعة</h3>
            {data.underReview.map((u) => (
              <ReportCard key={u.id} report={u} reference={reference} canDownload={canDownload}>
                {u.canReview ? <ReviewReport reference={reference} report={u} /> : null}
              </ReportCard>
            ))}
          </section>
        ) : null}

        {data.history.length > 0 ? (
          <section aria-labelledby="vh-h" className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4">
            <h3 id="vh-h" className="m-0 text-16 font-semibold">تقارير سابقة</h3>
            <ul className="m-0 flex list-none flex-col gap-2 p-0 text-14">
              {data.history.map((h) => (
                <li key={h.id} className="flex flex-wrap items-center gap-2">
                  <bdi dir="ltr" className="font-mono font-semibold">v{h.versionNo}</bdi>
                  <Money value={h.marketValue} />
                  <span className="text-muted">· <DateText value={h.reportDate} /></span>
                  <Tag tone={h.status === "Returned" ? "warn" : "neutral"} className="ms-auto">{h.statusLabel}</Tag>
                  {h.reviewNote ? <span className="basis-full text-13 text-muted">«{h.reviewNote}»</span> : null}
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>

      <AssignmentCard assignment={data.assignment} />

      {canAssign ? <RevaluationDialog reference={reference} mode={reval} onClose={() => setReval(null)} /> : null}
    </div>
  );
}

function ReportCard({ report, reference, canDownload, ltv, children }: {
  report: ValuationReportDto;
  reference: string;
  canDownload: boolean;
  ltv?: ValuationData["ltv"];
  children?: React.ReactNode;
}) {
  const accepted = report.status === "Accepted";
  return (
    <section aria-label={report.title} className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="m-0 flex-1 text-17 font-semibold">{report.title}</h3>
        {accepted ? (
          <Tag tone={report.validityTone as Tone} icon={VALIDITY_ICON[report.validityTone]}>{report.validityLabel}</Tag>
        ) : (
          <Tag tone="warn" icon="pending">{report.statusLabel}</Tag>
        )}
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Figure label="القيمة السوقية" value={<bdi dir="ltr" className="tabular-nums">{formatNumber(report.marketValue)}</bdi>} sub="ر.س" />
        <Figure label="النطاق" value={report.range ? <bdi dir="ltr" className="tabular-nums">{report.range.label}</bdi> : "—"} sub="حسب المقارنات" />
        {ltv ? (
          <Figure label={ltv.label} value={ltv.value === null ? "—" : <bdi dir="ltr" className="tabular-nums">{formatNumber(ltv.value, 1)}%</bdi>} sub={ltv.caption} />
        ) : (
          <Figure label="الصلاحية" value={<bdi dir="ltr" className="tabular-nums">{formatNumber(report.validityDays)}</bdi>} sub="يوماً" />
        )}
      </div>
      <KeyValueList
        dense
        rows={[
          { key: "المقيّم", value: report.valuer },
          ...(report.inspectionDate ? [{ key: "تاريخ المعاينة", value: <DateText value={report.inspectionDate} /> }] : []),
          { key: "تاريخ التقرير", value: <DateText value={report.reportDate} /> },
          ...(report.methodology
            ? [{ key: "المنهجية", value: report.comparablesCount ? `${report.methodology} · ${formatNumber(report.comparablesCount)} مقارنات` : report.methodology }]
            : []),
          { key: "الصلاحية", value: <>حتى <DateText value={report.validUntil} /> ({formatNumber(report.validityDays)} يوماً)</> },
          ...(report.assignment ? [{ key: "التكليف", value: <bdi dir="ltr" className="font-mono">{report.assignment}</bdi> }] : []),
          ...(report.reviewedBy && accepted ? [{ key: "اعتمده", value: <>{report.reviewedBy}{report.reviewedAt ? <> · <DateText value={report.reviewedAt} /></> : null}</> }] : []),
        ]}
      />
      <div className="flex flex-wrap items-start gap-3">
        {canDownload && report.documentVersionId ? (
          <a href={versionFileHref(reference, report.documentVersionId)} target="_blank" rel="noopener" className={buttonClasses({ variant: "secondary" })}>
            <Icon name="description" size={18} />
            فتح التقرير
          </a>
        ) : null}
        {children}
      </div>
    </section>
  );
}

function Figure({ label, value, sub }: { label: string; value: React.ReactNode; sub: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-md bg-warm p-3">
      <span className="text-13 text-muted">{label}</span>
      <strong className="text-22 leading-8">{value}</strong>
      <span className="text-12 text-muted">{sub}</span>
    </div>
  );
}

/** «طلب إعادة تقييم»: disabled with the API's reason until eligible; a documented reason is the alternate path (conflict #11). */
function RevaluationActions({ revaluation: r, onOpen }: { revaluation: ValuationData["revaluation"]; onOpen: (m: "eligible" | "reason") => void }) {
  if (r.pending) {
    return (
      <div className="flex flex-col gap-1">
        <Button variant="secondary" icon="autorenew" softDisabled aria-describedby="reval-why">طلب إعادة تقييم</Button>
        <span id="reval-why" className="text-12 text-muted">{r.disabledReason ?? "يوجد طلب إعادة تقييم مفتوح."}</span>
      </div>
    );
  }
  if (!r.canRequest) return null;
  if (r.eligible) {
    return <Button variant="secondary" icon="autorenew" review onClick={() => onOpen("eligible")}>طلب إعادة تقييم</Button>;
  }
  return (
    <div className="flex max-w-[360px] flex-col gap-1">
      <Button variant="secondary" icon="autorenew" softDisabled aria-describedby="reval-why">طلب إعادة تقييم</Button>
      <span id="reval-why" className="text-12 text-muted">
        {r.disabledReason}
        {r.eligibleFrom ? <> يُتاح من <DateText value={r.eligibleFrom} />.</> : null}
      </span>
      <button type="button" onClick={() => onOpen("reason")} className="self-start bg-transparent p-0 text-13 font-semibold text-ink underline underline-offset-[3px]">
        طلب بسبب موثق…
      </button>
    </div>
  );
}

function RevaluationDialog({ reference, mode, onClose }: { reference: string; mode: null | "eligible" | "reason"; onClose: () => void }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; field?: string } | null>(null);
  const needsReason = mode === "reason";
  const trimmed = reason.trim();
  const valid = needsReason ? trimmed.length >= 10 : trimmed.length === 0 || trimmed.length >= 10;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<{ assignee: string }>("POST", `/cases/${reference}/valuation/revaluation-requests`, { reason: trimmed || null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: `أُنشئت مهمة إعادة التقييم وأُسندت إلى ${res.assignee}.` });
      setReason("");
      onClose();
      router.refresh();
    } catch (e) {
      key.reset();
      // 409 revaluation_not_eligible / revaluation_pending carry the exact Arabic reason from the API.
      setError(isApiError(e) ? { title: e.title || "تعذّر إرسال الطلب.", field: e.fieldError("reason") } : { title: "تعذّر إرسال الطلب." });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog
      open={mode !== null}
      onClose={onClose}
      title="طلب إعادة تقييم"
      description={needsReason
        ? "التقرير الحالي ما زال صالحاً؛ يُقبل الطلب قبل موعد الأهلية بسبب موثق فقط، ويُسجَّل السبب في سجل الحالة."
        : "تُنشأ مهمة لمحلل الحالة لتكليف مقيّم، وتُسجَّل في سجل الحالة."}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>إلغاء</Button>
          <Button onClick={() => void submit()} loading={busy} disabled={!valid}>إرسال الطلب</Button>
        </>
      }
    >
      <div className="flex flex-col gap-4 p-5">
        {error ? <Alert tone="err" title={error.title} /> : null}
        <Textarea
          label="السبب الموثق"
          requiredMark={needsReason}
          optionalMark={!needsReason}
          value={reason}
          onChange={(e) => { setReason(e.target.value); key.reset(); }}
          maxLength={1000}
          error={error?.field}
          help="من 10 إلى 1000 حرف. مثال: تغيّر جوهري في العقار بعد المعاينة."
        />
      </div>
    </Dialog>
  );
}

function ReviewReport({ reference, report }: { reference: string; report: ValuationReportDto }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState<"accept" | "return" | null>(null);
  const [error, setError] = useState<{ title: string; field?: string } | null>(null);
  const expired = report.daysLeft < 0;

  const decide = async (decision: "accept" | "return") => {
    setBusy(decision);
    setError(null);
    try {
      const res = await apiSend<{ superseded: number }>("POST", `/cases/${reference}/valuation/${report.id}/review`, { decision, note: note.trim() || null }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({
        tone: "ok",
        message: decision === "accept"
          ? `اعتُمد التقرير v${report.versionNo}${res.superseded > 0 ? " وحلّ محل التقرير المعتمد السابق" : ""}.`
          : `أُعيد التقرير v${report.versionNo} للمقيّم.`,
      });
      router.refresh();
    } catch (e) {
      key.reset();
      setError(isApiError(e) ? { title: e.title || "تعذّر حفظ القرار.", field: e.fieldError("note") } : { title: "تعذّر حفظ القرار." });
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="flex w-full flex-col gap-3 rounded-md bg-warm p-3">
      <strong className="text-14">مراجعة التقرير</strong>
      {error ? <Alert tone="err" title={error.title} /> : null}
      <Textarea label="ملاحظة المراجعة" value={note} onChange={(e) => { setNote(e.target.value); key.reset(); }} maxLength={2000} error={error?.field}
        help="إلزامية عند الإعادة للمقيّم (10 أحرف على الأقل)." />
      <div className="flex flex-wrap items-start gap-2">
        <div className="flex flex-col gap-1">
          <Button icon="check_circle" loading={busy === "accept"} softDisabled={expired} aria-describedby={expired ? `exp-${report.id}` : undefined} onClick={() => void decide("accept")}>
            اعتماد التقرير
          </Button>
          {expired ? <span id={`exp-${report.id}`} className="text-12 text-muted">التقرير منتهي الصلاحية ولا يُعتمد؛ اطلب إعادة تقييم.</span> : null}
        </div>
        <Button variant="secondary" icon="undo" loading={busy === "return"} disabled={note.trim().length < 10} onClick={() => void decide("return")}>
          إعادة للمقيّم
        </Button>
      </div>
    </div>
  );
}

function AssignmentCard({ assignment: a }: { assignment: ValuationData["assignment"] }) {
  return (
    <section aria-labelledby="asg-h" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center gap-2">
        <h3 id="asg-h" className="m-0 flex-1 text-17 font-semibold">تكليف التقييم</h3>
        {a ? <bdi dir="ltr" className="font-mono text-13 font-semibold">{a.reference}</bdi> : null}
      </div>
      {!a ? (
        <p className="m-0 text-14 text-muted">لا يوجد تكليف تقييم على هذه الحالة.</p>
      ) : (
        <>
          <span className="text-14">{a.provider ?? "—"} · {a.title} · المهلة <DateText value={a.dueOn} /></span>
          <ol className="m-0 flex list-none flex-col gap-3.5 p-0">
            {a.timeline.map((t, i) => (
              <li key={`${t.type}-${i}`} className="grid grid-cols-[24px_minmax(0,1fr)] gap-2.5">
                <Icon name={t.icon} size={20} className={TIMELINE_TONE[t.tone]} />
                <div className="flex flex-col gap-0.5">
                  <span className={cn("text-14 font-semibold", t.tone === "err" && "text-err")}>{t.title}</span>
                  <span className="text-13 text-muted">{t.meta}</span>
                </div>
              </li>
            ))}
          </ol>
          <p className={cn("m-0 flex items-start gap-1.5 text-13", a.providerAccess.expired ? "text-muted" : "text-info")}>
            <Icon name="lock_clock" size={16} />
            {a.providerAccess.label}
          </p>
        </>
      )}
    </section>
  );
}
