"use client";

import { useRouter } from "next/navigation";
import { useState, type ReactNode } from "react";
import { StepUpDialog } from "@/components/case/StepUpDialog";
import {
  Alert,
  Button,
  Checkbox,
  DateText,
  EmptyState,
  Icon,
  KeyValueList,
  RadioCardGroup,
  ReviewScreen,
  SystemState,
  Tag,
  Textarea,
  useToast,
} from "@/components/ui";
import type { CaseStatusKey, Tone } from "@/components/ui/tones";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { PendingCancellation } from "@/lib/api/lender";
import { formatNumber } from "@/lib/format";

export interface CancellationHistoryItem {
  id: string;
  status: "Pending" | "Approved" | "Rejected" | string;
  reason: string | null;
  requestedBy: string | null;
  requestedAt: string | null;
  approver: string | null;
  dueOn: string | null;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionReason: string | null;
}

const HISTORY_STATUS: Record<string, { label: string; tone: Tone }> = {
  Pending: { label: "بانتظار الاعتماد", tone: "warn" },
  Approved: { label: "اعتُمد الإلغاء", tone: "ok" },
  Rejected: { label: "رُفض الطلب", tone: "err" },
};

type SubmitError = { title: string; reasons: string[]; stale?: boolean };

function toError(e: unknown, fallback: string): SubmitError {
  if (!isApiError(e)) return { title: fallback, reasons: [] };
  const field = e.fieldError("reason") ?? e.fieldError("decision");
  return {
    title: e.title || fallback,
    reasons: e.reasons?.length ? e.reasons : field ? [field] : [],
    stale: e.code === "stale_state" || e.code === "cancellation_pending" || e.code === "already_decided",
  };
}

interface Props {
  reference: string;
  status: CaseStatusKey;
  statusLabel: string;
  manager: string | null;
  openTasks: number;
  items: CancellationHistoryItem[];
  pending: PendingCancellation | null;
  canRequest: boolean;
  hasCancelPermission: boolean;
  stepUpActive: boolean;
}

export function CancelView(p: Props) {
  const terminal = p.status === "closed" || p.status === "cancelled";
  const summary = (
    <section aria-labelledby="cx-sum" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <h3 id="cx-sum" className="m-0 text-16 font-semibold">ملخص الحالة</h3>
      <KeyValueList
        dense
        rows={[
          { key: "المرجع", value: <bdi dir="ltr" className="font-mono">{p.reference}</bdi> },
          { key: "الحالة الحالية", value: p.statusLabel },
          { key: "مدير الحالة", value: p.manager ?? "—" },
          { key: "المهام المفتوحة", value: <bdi dir="ltr">{formatNumber(p.openTasks)}</bdi> },
        ]}
      />
    </section>
  );
  const history = p.items.filter((i) => i.id !== p.pending?.id);
  const historyBlock = history.length ? <History items={history} /> : null;

  if (p.pending?.canDecide) {
    return (
      <>
        <DecisionScreen {...p} pending={p.pending} aside={<>{summary}{historyBlock}</>} />
      </>
    );
  }

  if (p.pending) {
    return (
      <div className="flex flex-col gap-5">
        <h2 className="m-0 text-20 font-bold">إلغاء الحالة</h2>
        <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
          <PendingPanel pending={p.pending} />
          <div className="flex flex-col gap-4">{summary}{historyBlock}</div>
        </div>
      </div>
    );
  }

  if (p.canRequest) {
    return <RequestScreen {...p} aside={<>{summary}{historyBlock}</>} />;
  }

  return (
    <div className="flex flex-col gap-5">
      <h2 className="m-0 text-20 font-bold">إلغاء الحالة</h2>
      {terminal ? (
        <EmptyState icon="cancel" title={`الحالة ${p.statusLabel}`} body="لا يُطلب إلغاء حالة مغلقة أو ملغاة." />
      ) : !p.hasCancelPermission ? (
        <SystemState kind="forbidden" title="لا تملك صلاحية طلب إلغاء الحالة" body="يطلب الإلغاء مدير الحالة، ويعتمده معتمد آخر." />
      ) : (
        <EmptyState icon="cancel" title="طلب الإلغاء غير متاح الآن" body="أعد تحميل الصفحة لرؤية آخر وضع للحالة." />
      )}
      {historyBlock}
    </div>
  );
}

/* ───────── Maker: request cancellation ───────── */

function RequestScreen({ reference, status, stepUpActive, aside }: Props & { aside: ReactNode }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [reason, setReason] = useState("");
  const [attest, setAttest] = useState(false);
  const [busy, setBusy] = useState(false);
  const [stepUp, setStepUp] = useState(false);
  const [error, setError] = useState<SubmitError | null>(null);
  const complete = reason.trim().length >= 10 && attest;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await apiSend<{ id: string; approver: string; dueOn: string }>(
        "POST",
        `/cases/${encodeURIComponent(reference)}/cancellation-requests`,
        { reason: reason.trim(), expectedStatus: status },
        { idempotencyKey: key.get() },
      );
      key.reset();
      toast.toast({ tone: "ok", message: `أُرسل طلب الإلغاء إلى ${res.approver} للاعتماد.` });
      router.push(`/cases/${reference}`);
      router.refresh();
    } catch (e) {
      key.reset();
      if (isApiError(e) && e.code === "step_up_required") setStepUp(true);
      else setError(toError(e, "تعذّر إرسال طلب الإلغاء."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <ReviewScreen
        titleAs="h2"
        eyebrow="إجراء حساس · يتطلب اعتماد شخص آخر ويُسجَّل في السجل"
        title="إلغاء الحالة…"
        error={error ? <ErrorBody e={error} onReload={() => router.refresh()} /> : undefined}
        whatHappens={[
          { icon: "person_check", content: "يُرسل الطلب إلى معتمد مخوّل غير مقدم الطلب (فصل المهام)، مع إشعار ومهمة اعتماد مهلتها يوما عمل." },
          { icon: "hourglass_top", content: <>لا تتغير الحالة قبل الاعتماد. عند الاعتماد تصبح «ملغاة» وتُغلق المهام المفتوحة للحالة.</> },
          { icon: "report", content: "يُمنع الطلب والاعتماد ما دامت هناك شكوى أو اعتراض مفتوح على الحالة." },
          { icon: "notifications", content: "يصلك إشعار بقرار المعتمد (اعتماد أو رفض) مع سببه." },
          { icon: "info", content: "وصول المالك إلى البوابة لا يُلغى تلقائياً بالإلغاء؛ يُراجع بإجراء منفصل." },
          { icon: "passkey", content: "يُطلب رمز التحقق لتأكيد الطلب." },
        ]}
        reason={
          <div className="flex flex-col gap-4">
            <Textarea
              label="سبب الإلغاء"
              requiredMark
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={2000}
              help="10 أحرف على الأقل. يظهر للمعتمد وفي سجل الحالة."
              placeholder="مثال: سُدّد التمويل بالكامل خارج المنصة وفق خطاب المخالصة المرفق."
            />
            <Checkbox
              label="أقر بأن سبب الإلغاء صحيح وموثق، وأن الإلغاء لا يُنفذ إلا باعتماد معتمد آخر."
              checked={attest}
              onChange={(e) => setAttest(e.target.checked)}
            />
          </div>
        }
        aside={aside}
        back={{ label: "رجوع للحالة", href: `/cases/${reference}` }}
        recordCaption="سيُسجل: الفاعل، الوقت، السبب، والمعتمد المسند إليه"
        primary={{ label: "إرسال طلب الإلغاء", loading: busy, onClick: () => (complete ? (stepUpActive ? void submit() : setStepUp(true)) : undefined) }}
        complete={complete}
      />
      <StepUpDialog open={stepUp} onClose={() => setStepUp(false)} onVerified={() => { setStepUp(false); void submit(); }} />
    </>
  );
}

/* ───────── Checker: approve or reject ───────── */

const DECISIONS = [
  { value: "approve", label: "اعتماد الإلغاء", description: "تصبح الحالة «ملغاة» وتُغلق مهامها المفتوحة." },
  { value: "reject", label: "رفض الطلب", description: "تبقى الحالة كما هي ويُبلَّغ مقدم الطلب بالسبب." },
] as const;

function DecisionScreen({ reference, pending, stepUpActive, aside }: Props & { pending: PendingCancellation; aside: ReactNode }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [decision, setDecision] = useState<"approve" | "reject">("approve");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [stepUp, setStepUp] = useState(false);
  const [error, setError] = useState<SubmitError | null>(null);
  const complete = reason.trim().length >= 10;

  const decide = async () => {
    setBusy(true);
    setError(null);
    try {
      await apiSend(
        "POST",
        `/cases/${encodeURIComponent(reference)}/cancellation-requests/${pending.id}/decision`,
        { decision, reason: reason.trim() },
        { idempotencyKey: key.get() },
      );
      key.reset();
      toast.toast({ tone: "ok", message: decision === "approve" ? "اعتُمد إلغاء الحالة." : "رُفض طلب الإلغاء؛ الحالة كما هي." });
      router.push(`/cases/${reference}`);
      router.refresh();
    } catch (e) {
      key.reset();
      if (isApiError(e) && e.code === "step_up_required") setStepUp(true);
      else setError(toError(e, "تعذّر تسجيل القرار."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <ReviewScreen
        titleAs="h2"
        eyebrow="طلب إلغاء بانتظار اعتمادك · يُسجَّل في السجل"
        title="اعتماد إلغاء الحالة"
        error={error ? <ErrorBody e={error} onReload={() => router.refresh()} /> : undefined}
        whatHappens={
          decision === "approve"
            ? [
                { icon: "cancel", content: "تنتقل الحالة إلى «ملغاة» بسبب مقدم الطلب." },
                { icon: "task_alt", content: "تُغلق المهام المفتوحة للحالة." },
                { icon: "notifications", content: "يُبلَّغ مقدم الطلب بالقرار وسببه." },
                { icon: "report", content: "يُرفض الاعتماد إن وُجدت شكوى أو اعتراض مفتوح على الحالة." },
                { icon: "info", content: "وصول المالك إلى البوابة لا يُلغى تلقائياً؛ يُراجع بإجراء منفصل." },
              ]
            : [
                { icon: "undo", content: "تبقى الحالة كما هي دون تغيير." },
                { icon: "notifications", content: "يُبلَّغ مقدم الطلب بالرفض وسببه." },
              ]
        }
        evidence={[
          {
            name: <>طلب الإلغاء من {pending.requestedBy ?? "—"}</>,
            meta: (
              <>
                {pending.requestedAt ? <DateText value={pending.requestedAt} mode="datetime" /> : null}
                {pending.dueOn ? <> · المهلة <bdi dir="ltr">{pending.dueOn}</bdi></> : null}
              </>
            ),
          },
          { name: <>السبب: «{pending.reason ?? "—"}»</> },
        ]}
        sectionTitles={{ evidence: "الطلب", reason: "القرار والسبب" }}
        reason={
          <div className="flex flex-col gap-4">
            <RadioCardGroup legend="القرار" name="cancel-decision" columns={2} value={decision} onChange={(v) => setDecision(v as "approve" | "reject")}
              options={DECISIONS.map((d) => ({ value: d.value, label: d.label, description: d.description }))} />
            <Textarea
              label="سبب القرار"
              requiredMark
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={2000}
              help="10 أحرف على الأقل. يظهر لمقدم الطلب وفي سجل الحالة."
            />
            <span className="flex items-center gap-1.5 text-13 text-muted"><Icon name="passkey" size={18} />سيُطلب رمز التحقق لتأكيد القرار</span>
          </div>
        }
        aside={aside}
        back={{ label: "رجوع للحالة", href: `/cases/${reference}` }}
        recordCaption="سيُسجل: المعتمد، الوقت، القرار والسبب، وتأكيد رمز التحقق"
        primary={{
          label: decision === "approve" ? "تأكيد اعتماد الإلغاء" : "تأكيد رفض الطلب",
          loading: busy,
          onClick: () => (complete ? (stepUpActive ? void decide() : setStepUp(true)) : undefined),
        }}
        complete={complete}
      />
      <StepUpDialog open={stepUp} onClose={() => setStepUp(false)} onVerified={() => { setStepUp(false); void decide(); }} />
    </>
  );
}

/* ───────── Read-only pending request ───────── */

function PendingPanel({ pending }: { pending: PendingCancellation }) {
  return (
    <section aria-labelledby="cx-pending" className="flex flex-col gap-4 rounded-lg border border-line bg-white p-5">
      <div className="flex flex-wrap items-center gap-2">
        <h3 id="cx-pending" className="m-0 flex-1 text-17 font-semibold">طلب إلغاء بانتظار الاعتماد</h3>
        <Tag tone="warn" icon="hourglass_top">بانتظار القرار</Tag>
      </div>
      <KeyValueList
        rows={[
          { key: "مقدم الطلب", value: pending.requestedBy ?? "—" },
          { key: "وقت الطلب", value: pending.requestedAt ? <DateText value={pending.requestedAt} mode="datetime" /> : "—" },
          { key: "المعتمد المسند", value: pending.approver ?? "—" },
          { key: "المهلة", value: pending.dueOn ? <bdi dir="ltr">{pending.dueOn}</bdi> : "—" },
        ]}
      />
      <div className="rounded-md bg-warm p-3 text-14">
        <strong>السبب</strong>
        <p className="m-0 mt-1 leading-7">«{pending.reason ?? "—"}»</p>
      </div>
      <Alert tone="info" role="none" icon="block">
        {pending.note ?? (pending.assignedToMe ? "الطلب مسند إليك لكن دورك لا يتيح اعتماده." : "القرار للمعتمد المسند إليه الطلب؛ لا تملك قراراً عليه.")}
      </Alert>
    </section>
  );
}

function History({ items }: { items: CancellationHistoryItem[] }) {
  return (
    <section aria-labelledby="cx-hist" className="flex flex-col gap-3 rounded-lg border border-line bg-white p-5">
      <h3 id="cx-hist" className="m-0 text-16 font-semibold">طلبات إلغاء سابقة</h3>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {items.map((i) => {
          const s = HISTORY_STATUS[i.status] ?? { label: i.status, tone: "neutral" as Tone };
          return (
            <li key={i.id} className="flex flex-col gap-1 border-t border-divider pt-3 text-14 first:border-t-0 first:pt-0">
              <span className="flex flex-wrap items-center gap-2">
                <Tag tone={s.tone}>{s.label}</Tag>
                {i.requestedAt ? <span className="text-12 text-muted"><DateText value={i.requestedAt} /></span> : null}
              </span>
              <span>طلبه {i.requestedBy ?? "—"} · «{i.reason ?? "—"}»</span>
              {i.decidedBy ? (
                <span className="text-13 text-muted">
                  قرار {i.decidedBy}{i.decidedAt ? <> · <DateText value={i.decidedAt} /></> : null}{i.decisionReason ? <> · «{i.decisionReason}»</> : null}
                </span>
              ) : null}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function ErrorBody({ e, onReload }: { e: SubmitError; onReload: () => void }) {
  return (
    <span className="flex flex-col gap-1">
      <strong>{e.title}</strong>
      {e.reasons.length ? <ul className="m-0 ps-5">{e.reasons.map((r) => <li key={r}>{r}</li>)}</ul> : null}
      {e.stale ? (
        <Button variant="secondary" size="sm" className="self-start" onClick={onReload}>إعادة التحميل</Button>
      ) : null}
    </span>
  );
}
