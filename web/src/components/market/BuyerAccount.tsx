"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { buyerBody, CapacityFields, PreferenceFields, validateBuyer, type BuyerValues } from "@/components/market/BuyerForm";
import { Amount, Badge, Card, DemoBadge, StatusBadge, Timeline } from "@/components/market/ui";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Field";
import { apiSend, isApiError } from "@/lib/api/client";
import { label } from "@/lib/market/catalog";
import { day } from "@/lib/market/format";
import type { BuyerRequestView, Catalog, MarketEvent } from "@/lib/market/types";

const str = (n: number | null) => (n === null || n === undefined ? "" : String(n));

/** The buyer's request: status, the three capacity levels kept apart, editable preferences, and its log. */
export function BuyerAccount({ request: r, events, catalog }: { request: BuyerRequestView; events: MarketEvent[]; catalog: Catalog }) {
  const router = useRouter();
  const [v, setV] = useState<BuyerValues>({
    availableNow: str(r.availableNow), installmentComfort: str(r.installmentComfort), installmentFrequency: r.installmentFrequency ?? "monthly", maxPrice: str(r.maxPrice),
    purchaseMode: r.purchaseMode ?? "", financierId: r.preferredFinancierId ?? "", financierName: r.preferredFinancierName ?? "", cities: r.cities, areasText: r.areasText ?? "", propertyTypes: r.propertyTypes, areaMin: str(r.areaMin), areaMax: str(r.areaMax),
    bedroomsMin: str(r.bedroomsMin), readiness: r.readiness ?? "any", deliveryBy: r.deliveryBy ?? "",
  });
  const [editing, setEditing] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState<string | null>(null);
  const [msg, setMsg] = useState<{ tone: "ok" | "err"; text: string } | null>(null);
  const [note, setNote] = useState("");

  const save = async () => {
    const e = { ...validateBuyer(v, "capacity"), ...validateBuyer(v, "preferences") };
    setErrors(e);
    if (Object.keys(e).length) return;
    setBusy("save");
    setMsg(null);
    try {
      await apiSend("PUT", `/market/buyer-requests/${r.reference}`, buyerBody(v, { contactName: r.contactName }));
      setEditing(false);
      setMsg({ tone: "ok", text: "حفظنا تعديلاتك." });
      router.refresh();
    } catch (err) {
      if (isApiError(err) && err.errors) setErrors(Object.fromEntries(Object.entries(err.errors).map(([k, x]) => [k, x[0]])));
      setMsg({ tone: "err", text: isApiError(err) ? err.title : "تعذّر الحفظ." });
    } finally {
      setBusy(null);
    }
  };

  const act = async (key: string, path: string, body: unknown) => {
    setBusy(key);
    setMsg(null);
    try {
      await apiSend("POST", `/market/buyer-requests/${r.reference}/${path}`, body);
      router.refresh();
    } catch (err) {
      setMsg({ tone: "err", text: isApiError(err) ? err.title : "تعذّر تنفيذ الإجراء." });
    } finally {
      setBusy(null);
    }
  };

  const c = r.capacity;
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="m-0 flex items-center gap-2 text-24 font-bold">
          طلب الشراء <bdi dir="ltr" className="font-mono text-17 text-muted">{r.reference}</bdi>
          {r.isDemo ? <DemoBadge /> : null}
        </h1>
        <StatusBadge status={r.status} label={r.statusLabel} />
      </div>
      <p className="m-0 rounded-md border border-info-line bg-info-bg p-3 text-15"><strong>الخطوة التالية: </strong>{r.nextStep}</p>
      {msg ? <Alert tone={msg.tone} role="status">{msg.text}</Alert> : null}
      {r.openCompletion ? (
        <Alert tone="warn" title="طلب الفريق استكمالًا">
          <p className="m-0 mb-2">{r.openCompletion.note}</p>
          <Textarea label="ملاحظة للفريق (اختياري)" value={note} onChange={(e) => setNote(e.target.value)} rows={2} />
          <Button className="mt-2" onClick={() => void act("resubmit", "resubmit", { note })} loading={busy === "resubmit"}>إرسال للمراجعة</Button>
        </Alert>
      ) : null}
      {r.decisionReason && r.status === "rejected" ? <Alert tone="err" title="سبب عدم القبول">{r.decisionReason}</Alert> : null}

      <Card title="قدرتك الشرائية — ثلاثة مستويات منفصلة">
        <div className="grid gap-3 md:grid-cols-3">
          <div className="flex flex-col gap-1 rounded-md bg-subtle p-3">
            <span className="text-13 font-semibold text-muted">ما صرّحت به</span>
            <span>المتاح الآن: <Amount value={c.declared.availableNow} strong /></span>
            {c.declared.installmentComfort ? <span className="text-14">القسط: <Amount value={c.declared.installmentComfort} size="sm" /> {label(catalog.frequencies, c.declared.installmentFrequency)}</span> : null}
            {c.declared.maxPrice ? <span className="text-14">الحد الأقصى: <Amount value={c.declared.maxPrice} size="sm" /></span> : null}
          </div>
          <div className="flex flex-col gap-1 rounded-md bg-subtle p-3">
            <span className="text-13 font-semibold text-muted">ما راجعه فريق رهون</span>
            {c.reviewed ? (
              <>
                <Amount value={c.reviewed.amount} strong />
                <span className="text-13">{c.reviewed.note}</span>
                <span className="text-12 text-muted">{c.reviewed.by} · {day(c.reviewed.at)}</span>
              </>
            ) : <span className="text-14 text-muted">لم تُراجع إثباتات بعد.</span>}
          </div>
          <div className="flex flex-col gap-1 rounded-md bg-subtle p-3">
            <span className="text-13 font-semibold text-muted">موافقة جهة تمويل</span>
            <span className="text-14">{c.financeApproval.statusLabel}</span>
            {c.financeApproval.source ? <span className="text-13">{c.financeApproval.source} · {day(c.financeApproval.date)}</span> : null}
          </div>
        </div>
        <p className="m-0 mt-3 text-13 text-muted">اعتماد فريق رهون لملفك لا يساوي موافقة بنك أو جهة تمويل.</p>
      </Card>

      <Card title="التفضيلات" actions={r.editable && !editing ? <Button variant="secondary" onClick={() => setEditing(true)}>تعديل</Button> : null}>
        {editing ? (
          <div className="flex flex-col gap-6">
            <CapacityFields v={v} set={(p) => setV((x) => ({ ...x, ...p }))} errors={errors} catalog={catalog} />
            <PreferenceFields v={v} set={(p) => setV((x) => ({ ...x, ...p }))} errors={errors} catalog={catalog} />
            {r.status === "approvedForMatching" ? <p className="m-0 text-13 text-warn">تغيير المبلغ المتاح أو القسط أو طريقة الشراء يعيد ملفك لمراجعة الفريق. تعديل التفضيلات وحدها لا يعيده.</p> : null}
            <div className="flex gap-2">
              <Button onClick={() => void save()} loading={busy === "save"}>حفظ</Button>
              <Button variant="secondary" onClick={() => setEditing(false)}>إلغاء</Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-wrap gap-2">
            {r.cities.map((x) => <Badge key={x}>{catalog.cities.find((y) => y.key === x)?.label ?? x}</Badge>)}
            {r.propertyTypes.map((x) => <Badge key={x} tone="info">{label(catalog.propertyTypes, x)}</Badge>)}
            {r.bedroomsMin ? <Badge>{r.bedroomsMin}+ غرف</Badge> : null}
            {r.readiness && r.readiness !== "any" ? <Badge>{r.readiness === "ready" ? "جاهز" : "تحت الإنشاء"}</Badge> : null}
            {r.areasText ? <Badge>{r.areasText}</Badge> : null}
          </div>
        )}
      </Card>

      <Card title="سجل الطلب">
        <Timeline events={events} />
      </Card>
      {r.editable ? (
        <Button variant="sensitive" className="self-start" loading={busy === "withdraw"} onClick={() => void act("withdraw", "withdraw", { reason: null })}>سحب طلب الشراء</Button>
      ) : null}
    </div>
  );
}
