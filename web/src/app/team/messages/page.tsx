import type { Metadata } from "next";
import { HandledButton } from "@/components/market/team/HandledButton";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { Badge, Card } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import { dayTime } from "@/lib/market/format";

export const metadata: Metadata = { title: "رسائل التواصل" };

interface Msg { reference: string; name: string; phoneMasked: string; topic: string; message: string; status: string; createdAt: string; handledNote: string | null; handledByLabel: string | null; handledAt: string | null }
const TOPIC: Record<string, string> = { sell: "بيع عقار", buy: "شراء فرصة", request: "طلب قائم", other: "أخرى" };

export default async function TeamMessages({ searchParams }: PageProps<"/team/messages">) {
  const sp = await searchParams;
  const rows = await apiGet<Msg[]>(`/team/market/contact-messages${sp.status === "new" ? "?status=new" : ""}`);
  return (
    <div>
      <TeamHeader title="رسائل التواصل" sub="رسائل نموذج «تواصل معنا». الجوال مخفي جزئيًا؛ تواصل من القنوات المعتمدة." />
      <div className="flex flex-col gap-3">
        {rows.length === 0 ? <p className="m-0 text-15 text-muted">لا توجد رسائل.</p> : rows.map((m) => (
          <Card key={m.reference}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <strong>{m.name} · <bdi dir="ltr">{m.phoneMasked}</bdi></strong>
              <span className="flex gap-2"><Badge>{TOPIC[m.topic] ?? m.topic}</Badge><Badge tone={m.status === "new" ? "warn" : "ok"}>{m.status === "new" ? "جديدة" : "تمت المعالجة"}</Badge></span>
            </div>
            <p className="m-0 mt-2 text-15 leading-7">{m.message}</p>
            <p className="m-0 mt-1 text-12 text-muted"><bdi dir="ltr">{m.reference}</bdi> · {dayTime(m.createdAt)}{m.handledByLabel ? ` · عالجها ${m.handledByLabel}` : ""}</p>
            {m.handledNote ? <p className="m-0 text-13">ملاحظة: {m.handledNote}</p> : null}
            {m.status === "new" ? <HandledButton reference={m.reference} /> : null}
          </Card>
        ))}
      </div>
    </div>
  );
}
