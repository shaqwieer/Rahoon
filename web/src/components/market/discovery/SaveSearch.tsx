"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Chips } from "@/components/market/DynamicField";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { Dialog } from "@/components/ui/Dialog";
import { Checkbox, TextField } from "@/components/ui/Field";
import { apiSend, isApiError, useIdempotencyKey } from "@/lib/api/client";
import type { SavedSearchView } from "@/lib/market/types";

/**
 * «احفظ هذا البحث»: stores the current filters (not the page) in the person's account, optionally with alerts. Alerts need an
 * explicit agreement; in-app alerts are real, SMS goes through the sandbox until a provider is contracted (D1) and says so.
 */
export function SaveSearch({ query, suggestedName, signedIn }: { query: string; suggestedName: string; signedIn: boolean }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [alerts, setAlerts] = useState(true);
  const [channel, setChannel] = useState<"in_app" | "sms">("in_app");
  const [consent, setConsent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<{ created: boolean; search: SavedSearchView } | null>(null);
  const [busy, setBusy] = useState(false);
  const key = useIdempotencyKey();

  if (!signedIn)
    return (
      <Link href={`/signin?next=${encodeURIComponent(`/opportunities${query ? `?${query}` : ""}`)}`} className={buttonClasses({ variant: "secondary", size: "md" })}>
        احفظ البحث وتابع الجديد
      </Link>
    );

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (alerts && !consent) {
      setError("لتفعيل التنبيهات، وافق على استلامها.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const r = await apiSend<{ created: boolean; search: SavedSearchView }>("POST", "/market/my/searches",
        { name: name.trim() || suggestedName, query, alertsEnabled: alerts, channel, consent: alerts && consent }, { idempotencyKey: key.get() });
      setDone(r);
      key.reset();
    } catch (err) {
      setError(isApiError(err) ? (err.errors ? Object.values(err.errors)[0]?.[0] ?? err.title : err.title) : "تعذّر الحفظ. تحقق من الاتصال.");
    } finally {
      setBusy(false);
    }
  };

  const close = () => {
    setOpen(false);
    setDone(null);
    setError(null);
  };

  return (
    <>
      <Button variant="secondary" icon="bookmark_add" onClick={() => setOpen(true)}>احفظ هذا البحث</Button>
      <Dialog open={open} onClose={close} title="حفظ البحث" size="md">
        {done ? (
          <div className="flex flex-col gap-3 p-5" role="status">
            <Alert tone="ok">{done.created ? "حفظنا البحث في حسابك." : "هذا البحث محفوظ من قبل في حسابك بالفلاتر نفسها."}</Alert>
            <p className="m-0 text-14">«{done.search.name}» — يطابقه الآن {done.search.currentMatches} {done.search.currentMatches === 1 ? "فرصة" : "فرص"}.</p>
            {done.search.alertsEnabled ? <p className="m-0 text-13 text-muted">سننبهك بالفرص الجديدة فقط؛ ما يطابق الآن محسوب كمُشاهد.</p> : null}
            <div className="flex gap-2">
              <Link href="/account/searches" className={buttonClasses({ variant: "primary", size: "md" })}>عمليات البحث المحفوظة</Link>
              <Button variant="secondary" onClick={close}>إغلاق</Button>
            </div>
          </div>
        ) : (
          <form onSubmit={submit} className="flex flex-col gap-4 p-5" noValidate>
            {error ? <Alert tone="err" compact>{error}</Alert> : null}
            <TextField label="اسم البحث" value={name} onChange={(e) => setName(e.target.value)} placeholder={suggestedName} maxLength={80} />
            <Checkbox label="نبّهني بالفرص الجديدة التي تطابق هذا البحث" checked={alerts} onChange={(e) => setAlerts(e.target.checked)} />
            {alerts ? (
              <>
                <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
                  <legend className="mb-1 text-14 font-semibold">طريقة التنبيه</legend>
                  <Chips name="channel" value={channel} onChange={(v) => setChannel(v as "in_app" | "sms")}
                    options={[{ value: "in_app", label: "داخل حسابي" }, { value: "sms", label: "داخل حسابي ورسالة نصية" }]} />
                  {channel === "sms" ? <span className="text-13 text-warn">الرسائل النصية تجريبية حاليًا ولا تُرسل فعليًا؛ يصلك التنبيه داخل حسابك.</span> : null}
                </fieldset>
                <Checkbox label="أوافق على أن ترسل لي رهون تنبيهات بهذا البحث، ويمكنني إيقافها أو حذفها في أي وقت." checked={consent} onChange={(e) => setConsent(e.target.checked)} />
              </>
            ) : null}
            <p className="m-0 text-12 text-muted">نحفظ الفلاتر فقط، لا الصفحة. التنبيه يخص الفرص المنشورة الجديدة أو التي انخفض المطلوب الآن فيها، ولا يحجز شيئًا.</p>
            <div className="flex gap-2">
              <Button type="submit" loading={busy}>حفظ</Button>
              <Button type="button" variant="secondary" onClick={close}>إلغاء</Button>
            </div>
          </form>
        )}
      </Dialog>
    </>
  );
}
