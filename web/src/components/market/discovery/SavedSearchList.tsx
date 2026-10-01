"use client";

import Link from "next/link";
import { useState } from "react";
import { Badge, Card } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Checkbox, TextField } from "@/components/ui/Field";
import { apiSend, isApiError } from "@/lib/api/client";
import { dayTime } from "@/lib/market/format";
import type { AlertDelivery, SavedSearchView } from "@/lib/market/types";

const STATUS: Record<string, { label: string; tone: "ok" | "info" | "warn" | "neutral" | "err" }> = {
  sent: { label: "وصل داخل حسابك", tone: "ok" },
  simulated: { label: "داخل حسابك · الرسالة النصية تجريبية لم تُرسل", tone: "info" },
  skipped: { label: "لم يُرسل", tone: "neutral" },
  failed: { label: "تعذّر", tone: "err" },
  sending: { label: "قيد الإرسال", tone: "info" },
};

/** The person's saved searches: open, rename, alerts on/off (with agreement), pause/resume, delete. */
export function SavedSearchList({ initial, delivery, max }: { initial: SavedSearchView[]; delivery: AlertDelivery; max: number }) {
  const [items, setItems] = useState(initial);
  const [msg, setMsg] = useState<{ tone: "ok" | "err"; text: string } | null>(null);

  const replace = (s: SavedSearchView) => setItems((xs) => xs.map((x) => (x.id === s.id ? s : x)));
  const remove = (id: string) => setItems((xs) => xs.filter((x) => x.id !== id));

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="m-0 text-24 font-bold">عمليات البحث المحفوظة</h1>
        <Link href="/opportunities" className={buttonClasses({ variant: "secondary", size: "md" })}>بحث جديد</Link>
      </div>
      <Alert tone={delivery.workerEnabled ? "info" : "warn"}>
        {delivery.workerEnabled
          ? `نراجع عمليات البحث التي فعّلت تنبيهاتها كل ${delivery.intervalMinutes} دقيقة تقريبًا، وننبهك بالفرص المنشورة الجديدة أو التي انخفض المطلوب الآن فيها.`
          : "التنبيهات متوقفة على هذه البيئة حاليًا؛ تبقى عمليات البحث محفوظة ويمكنك فتحها في أي وقت."}
        {delivery.smsText ? ` ${delivery.smsText}` : ""}
      </Alert>
      {msg ? <Alert tone={msg.tone} role="status">{msg.text}</Alert> : null}
      {items.length === 0 ? (
        <Card>
          <p className="m-0 text-15 text-muted">لا توجد عمليات بحث محفوظة. ابحث في <Link href="/opportunities">الفرص المتاحة</Link> ثم «احفظ هذا البحث».</p>
        </Card>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-4 p-0">
          {items.map((s) => <li key={s.id}><SearchRow s={s} onChange={replace} onRemove={remove} onMessage={setMsg} /></li>)}
        </ul>
      )}
      <p className="m-0 text-13 text-muted">حتى {max} عملية بحث. التنبيه لا يحجز فرصة ولا يعني موافقة تمويل.</p>
    </div>
  );
}

function SearchRow({ s, onChange, onRemove, onMessage }: {
  s: SavedSearchView;
  onChange: (s: SavedSearchView) => void;
  onRemove: (id: string) => void;
  onMessage: (m: { tone: "ok" | "err"; text: string } | null) => void;
}) {
  const [busy, setBusy] = useState<string | null>(null);
  const [renaming, setRenaming] = useState(false);
  const [name, setName] = useState(s.name);
  const [enabling, setEnabling] = useState(false);
  const [consent, setConsent] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const call = async (key: string, fn: () => Promise<{ search?: SavedSearchView; deleted?: boolean }>) => {
    setBusy(key);
    onMessage(null);
    try {
      const r = await fn();
      if (r.deleted) onRemove(s.id);
      else if (r.search) onChange(r.search);
      return true;
    } catch (err) {
      onMessage({ tone: "err", text: isApiError(err) ? (err.errors ? Object.values(err.errors)[0]?.[0] ?? err.title : err.title) : "تعذّر تنفيذ الإجراء." });
      return false;
    } finally {
      setBusy(null);
    }
  };

  return (
    <Card>
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-start justify-between gap-2">
          {renaming ? (
            <form className="flex flex-wrap items-end gap-2" onSubmit={async (e) => {
              e.preventDefault();
              if (await call("rename", () => apiSend("PUT", `/market/my/searches/${s.id}`, { name, version: s.version }))) setRenaming(false);
            }}>
              <TextField label="اسم البحث" value={name} onChange={(e) => setName(e.target.value)} maxLength={80} />
              <Button type="submit" size="md" loading={busy === "rename"}>حفظ</Button>
              <Button type="button" variant="secondary" size="md" onClick={() => setRenaming(false)}>إلغاء</Button>
            </form>
          ) : (
            <h2 className="m-0 text-18 font-bold">{s.name}</h2>
          )}
          <span className="flex flex-wrap gap-1.5">
            {!s.alertsEnabled ? <Badge>بلا تنبيهات</Badge> : s.paused ? <Badge tone="warn">التنبيهات موقوفة</Badge> : <Badge tone="ok" icon="notifications_active">التنبيهات مفعلة{s.channel === "sms" ? " · رسالة نصية (تجريبية)" : ""}</Badge>}
          </span>
        </div>
        <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
          {s.summary.map((x) => <li key={x}><Badge tone="neutral">{x}</Badge></li>)}
        </ul>
        <span className="text-14">يطابقه الآن <strong>{s.currentMatches}</strong> {s.currentMatches === 1 ? "فرصة منشورة" : "فرص منشورة"}.</span>
        {s.recentAlerts.length ? (
          <details className="text-13">
            <summary className="cursor-pointer font-semibold">آخر التنبيهات</summary>
            <ul className="m-0 mt-2 flex list-none flex-col gap-1 p-0">
              {s.recentAlerts.map((a, i) => (
                <li key={i} className="flex flex-wrap items-center gap-2">
                  <Badge tone={STATUS[a.status]?.tone ?? "neutral"}>{STATUS[a.status]?.label ?? a.status}</Badge>
                  <bdi dir="ltr" className="text-muted">{dayTime(a.sentAt ?? a.createdAt)}</bdi>
                  {a.reason ? <span className="text-muted">{a.reason}</span> : null}
                </li>
              ))}
            </ul>
          </details>
        ) : null}
        {enabling ? (
          <div className="flex flex-col gap-2 rounded-md bg-subtle p-3">
            <Checkbox label="أوافق على أن ترسل لي رهون تنبيهات بهذا البحث، ويمكنني إيقافها أو حذفها في أي وقت." checked={consent} onChange={(e) => setConsent(e.target.checked)} />
            <div className="flex gap-2">
              <Button size="md" disabled={!consent} loading={busy === "enable"} onClick={async () => {
                if (await call("enable", () => apiSend("PUT", `/market/my/searches/${s.id}`, { alertsEnabled: true, consent: true, version: s.version }))) setEnabling(false);
              }}>تفعيل التنبيهات</Button>
              <Button size="md" variant="secondary" onClick={() => setEnabling(false)}>إلغاء</Button>
            </div>
          </div>
        ) : null}
        {confirmDelete ? (
          <div className="flex flex-wrap items-center gap-2 rounded-md border border-err-line bg-err-bg p-3 text-14" role="alert">
            حذف هذا البحث وإيقاف تنبيهاته؟
            <Button size="md" variant="sensitive" loading={busy === "delete"} onClick={() => void call("delete", () => apiSend("DELETE", `/market/my/searches/${s.id}`))}>حذف</Button>
            <Button size="md" variant="secondary" onClick={() => setConfirmDelete(false)}>إلغاء</Button>
          </div>
        ) : null}
        <div className="flex flex-wrap gap-2">
          <Link href={s.url} className={buttonClasses({ variant: "primary", size: "md" })}>فتح النتائج</Link>
          {!renaming ? <Button size="md" variant="secondary" onClick={() => setRenaming(true)}>إعادة التسمية</Button> : null}
          {!s.alertsEnabled ? (
            <Button size="md" variant="secondary" onClick={() => setEnabling(true)}>تفعيل التنبيهات</Button>
          ) : s.paused ? (
            <Button size="md" variant="secondary" loading={busy === "resume"} onClick={() => void call("resume", () => apiSend("POST", `/market/my/searches/${s.id}/resume`))}>استئناف التنبيهات</Button>
          ) : (
            <>
              <Button size="md" variant="secondary" loading={busy === "pause"} onClick={() => void call("pause", () => apiSend("POST", `/market/my/searches/${s.id}/pause`))}>إيقاف مؤقت</Button>
              <Button size="md" variant="text" loading={busy === "off"} onClick={() => void call("off", () => apiSend("PUT", `/market/my/searches/${s.id}`, { alertsEnabled: false, version: s.version }))}>إلغاء التنبيهات</Button>
            </>
          )}
          {!confirmDelete ? <Button size="md" variant="sensitive" onClick={() => setConfirmDelete(true)}>حذف</Button> : null}
        </div>
      </div>
    </Card>
  );
}
