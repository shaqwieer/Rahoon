import Link from "next/link";
import { WithdrawInterest } from "@/components/market/WithdrawInterest";
import { Badge, Card, StatusBadge, Timeline } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import { day } from "@/lib/market/format";
import type { MarketEvent } from "@/lib/market/types";

interface MyInterest {
  reference: string;
  status: string;
  statusLabel: string;
  createdAt: string;
  message: string | null;
  closeReason: string | null;
  opportunity: { reference: string; title: string; status: string; statusLabel: string; available: boolean; termsChanged: boolean };
  events: MarketEvent[];
}

/** Interests the buyer sent: each tied to an opportunity and the figures version they saw. None reserves a property. */
export default async function MyInterestsPage() {
  const rows = await apiGet<MyInterest[]>("/market/my/interests");
  return (
    <div className="flex flex-col gap-4">
      <h1 className="m-0 text-24 font-bold">اهتماماتي</h1>
      <p className="m-0 text-14 text-muted">الاهتمام بداية تواصل مع فريق رهون، ولا يحجز العقار ولا يعد عرضًا ملزمًا.</p>
      {rows.length === 0 ? (
        <Card><p className="m-0 text-15 text-muted">لم ترسل اهتمامًا بعد. <Link href="/opportunities">تصفح الفرص</Link>.</p></Card>
      ) : (
        rows.map((i) => (
          <Card key={i.reference} title={<Link href={`/opportunities/${i.opportunity.reference}`}>{i.opportunity.title}</Link>} actions={<StatusBadge status={i.status} label={i.statusLabel} />}>
            <div className="flex flex-col gap-3">
              <span className="text-13 text-muted"><bdi dir="ltr" className="font-mono">{i.reference}</bdi> · أُرسل {day(i.createdAt)}</span>
              <div className="flex flex-wrap gap-2">
                {!i.opportunity.available ? <Badge tone="warn">الفرصة {i.opportunity.statusLabel}</Badge> : null}
                {i.opportunity.termsChanged ? <Badge tone="info" icon="update">تغيّرت أرقام الفرصة بعد اهتمامك — راجعها</Badge> : null}
              </div>
              {i.message ? <p className="m-0 text-14">رسالتك: {i.message}</p> : null}
              {i.closeReason ? <p className="m-0 text-14">سبب الإغلاق: {i.closeReason}</p> : null}
              <Timeline events={i.events} />
              {i.status === "received" || i.status === "inFollowUp" ? <WithdrawInterest reference={i.reference} /> : null}
            </div>
          </Card>
        ))
      )}
    </div>
  );
}
