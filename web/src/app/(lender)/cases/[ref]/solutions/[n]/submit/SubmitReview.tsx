"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Alert, Checkbox, Icon, ReviewScreen, Textarea, useToast } from "@/components/ui";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import { SOLUTION_KIND_LABEL, type SubmissionData } from "@/lib/api/lender";
import { formatDate, formatMoney, formatPercent } from "@/lib/format";

/** P0 review-screen pattern: what happens · evidence · note + attestation; blocked for the preparer. */
export function SubmitReview({ data }: { data: SubmissionData }) {
  const router = useRouter();
  const toast = useToast();
  const key = useIdempotencyKey();
  const [note, setNote] = useState("");
  const [attested, setAttested] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const s = data.summary;
  const base = `/cases/${data.reference}`;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const r = await apiSend<{ approver: string; dueOn: string }>("POST", `${base}/solutions/${data.version}/submit`,
        { note, attested, expectedStatus: data.expectedStatus }, { idempotencyKey: key.get() });
      key.reset();
      toast.toast({ tone: "ok", message: `أُرسل الحل للموافقة · ${r.approver} · المهلة ${r.dueOn}` });
      router.push(base);
      router.refresh();
    } catch (e) {
      // Keep the same key when the failure may have been a network blip; a validation error is a new submit.
      if (isApiError(e) && e.status !== 0) key.reset();
      setError(isApiError(e) ? [e.title, ...(e.reasons ?? []), ...Object.values(e.errors ?? {}).flat()].join(" · ") : "تعذّر الإرسال.");
    } finally {
      setBusy(false);
    }
  };

  const blocked = data.blockers.length > 0 ? (
    <ul className="m-0 flex list-none flex-col gap-1 p-0">
      {data.blockers.map((b) => <li key={b}>{b}</li>)}
    </ul>
  ) : undefined;

  return (
    <ReviewScreen
      titleAs="h2"
      eyebrow="إجراء عالي الأثر · يُسجَّل في السجل"
      title={`مراجعة قبل إرسال الحل v${data.version} للموافقة الداخلية`}
      error={error}
      whatHappens={[
        { icon: "swap_horiz", content: <>تنتقل الحالة من <strong>{data.from}</strong> إلى <strong>{data.to}</strong>.</> },
        { icon: "edit_off", content: <>يُقفل الإصدار v{data.version} للتعديل. أي تغيير لاحق ينشئ v{data.version + 1} ويعيد السلسلة.</> },
        {
          icon: "person",
          content: data.approver
            ? <>يُسند إلى <strong>{data.approver.name}</strong> ({data.approver.limit}) · المهلة {data.dueDays} أيام: <bdi dir="ltr">{formatDate(data.dueOn)}</bdi>.</>
            : "لا يوجد معتمد متاح ضمن الحدود.",
        },
        { icon: "visibility_off", content: "لن يرى المالك أي عرض قبل الاعتماد. لا يُرسل له إشعار الآن." },
      ]}
      evidence={data.evidence.map((e) => ({ name: e.name, meta: e.meta }))}
      sectionTitles={{ evidence: "الأدلة المرفقة تلقائياً", reason: "ملاحظتك للمعتمد (إلزامية)" }}
      blockedReason={blocked}
      reason={
        <div className="flex flex-col gap-3">
          <Textarea label="ملاحظة للمعتمد" requiredMark value={note} onChange={(e) => setNote(e.target.value)} maxLength={1000}
            placeholder="مثال: راجعت الحل مع كشف الراتب. القسط ضمن حد الاستقطاع، والتنازل محصور في غرامات التأخير فقط." />
          <Checkbox checked={attested} onChange={(e) => setAttested(e.target.checked)} label="أؤكد أنني راجعت شروط الحل والأدلة، وأنني لست مُعِدّ هذا الإصدار." />
        </div>
      }
      aside={
        <>
          <div className="flex flex-col gap-2 rounded-lg border border-line bg-white p-4 text-14">
            <div className="flex items-baseline justify-between">
              <strong>ملخص الحل v{data.version}</strong>
              <bdi dir="ltr" className="font-mono text-13 text-muted">{data.reference}</bdi>
            </div>
            <Row k="النوع" v={SOLUTION_KIND_LABEL[s.kind]} />
            <Row k="المبلغ المعاد جدولته" v={<><bdi dir="ltr">{formatMoney(s.rescheduledAmount)}</bdi> ر.س</>} />
            <Row k="المدة" v={`${s.termMonths} شهراً`} />
            <Row k="القسط الشهري" v={<strong><bdi dir="ltr">{formatMoney(s.installmentAmount)}</bdi> ر.س</strong>} />
            <Row k="التنازل" v={<><bdi dir="ltr">{formatMoney(s.waiverAmount)}</bdi> ({formatPercent(s.waiverPercent * 100, { fractionDigits: 2 })})</>} />
            <Row k="نسبة الاستقطاع" v={s.dsr === null ? "—" : (
              <span className={s.dsr <= s.dsrLimit ? "inline-flex items-center gap-1 text-ok" : "inline-flex items-center gap-1 text-err"}>
                <Icon name={s.dsr <= s.dsrLimit ? "check_circle" : "error"} size={16} />
                {formatPercent(s.dsr * 100, { fractionDigits: 1 })}
              </span>
            )} />
          </div>
          <Alert tone="info" role="none" compact>
            القيم أعلاه نسخة مقفلة ستُرسل كما هي.{s.complianceNotice ? <> قاعدة إشعار الامتثال عند التنازل &gt; 1% — <strong>افتراض يتطلب تأكيد المنتج</strong>.</> : null}
          </Alert>
        </>
      }
      back={{ label: "رجوع للحالة", href: `/cases/${data.reference}` }}
      recordCaption={`سيُسجل: الفاعل، الوقت، الملاحظة، نسخة v${data.version}`}
      primary={{ label: "إرسال للموافقة", onClick: () => void submit(), loading: busy, loadingLabel: "جارٍ الإرسال" }}
      complete={note.trim().length >= 10 && attested}
    />
  );
}

function Row({ k, v }: { k: string; v: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted">{k}</span>
      <span className="text-end">{v}</span>
    </div>
  );
}
