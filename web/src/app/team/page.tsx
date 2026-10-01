import type { Metadata } from "next";
import Link from "next/link";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { Badge, Card } from "@/components/market/ui";
import { Icon } from "@/components/ui/Icon";
import { redirect } from "next/navigation";
import { apiGet, can, requireMe } from "@/lib/api/server";
import { day } from "@/lib/market/format";

export const metadata: Metadata = { title: "مساحة فريق رهون" };

interface Overview {
  sale: Record<string, number>;
  buyer: Record<string, number>;
  opportunities: Record<string, number>;
  interests: Record<string, number>;
  /** Null when the member doesn't follow all work (visitor messages belong to no case). */
  contactNew: number | null;
  unassigned: { sale: number; interests: number };
  myTasks: { kind: string; reference: string; title: string; status: string; at: string }[];
  permissions: string[];
  scopes: Record<string, "assigned" | "all">;
}

const KIND: Record<string, { label: string; href: (r: string) => string; icon: string }> = {
  sale: { label: "طلب بيع", href: (r) => `/team/sale/${r}`, icon: "sell" },
  buyer: { label: "طلب مشترٍ", href: (r) => `/team/buyers/${r}`, icon: "person_search" },
  opportunity: { label: "فرصة", href: (r) => `/team/opportunities/${r}`, icon: "home_work" },
  interest: { label: "اهتمام", href: (r) => `/team/interests/${r}`, icon: "handshake" },
};

/** Team overview: queues with counts and «مهامي» (what is assigned to me and waiting for the team). */
export default async function TeamHome() {
  const me = await requireMe();
  // Members without the dashboard land on the first area their grants open (computed by the API).
  if (!can(me, "dashboard.view") || !can(me, "market.view")) redirect(me.home === "/team" ? "/access-denied" : me.home);
  const o = await apiGet<Overview>("/team/market/overview");
  const assignedOnly = o.scopes["market.view"] === "assigned";
  const tiles = [
    { label: "طلبات بيع جديدة", n: o.sale.Submitted ?? 0, href: "/team/sale?queue=new", icon: "inbox" },
    { label: "طلبات بيع قيد المراجعة", n: o.sale.UnderReview ?? 0, href: "/team/sale?queue=review", icon: "fact_check" },
    { label: "بانتظار استكمال العميل", n: o.sale.NeedsCompletion ?? 0, href: "/team/sale?queue=completion", icon: "hourglass_top" },
    { label: "معتمدة لإعداد فرصة", n: o.sale.ApprovedForListing ?? 0, href: "/team/sale?queue=approved", icon: "task_alt" },
    { label: "طلبات مشترين جديدة", n: (o.buyer.Submitted ?? 0) + (o.buyer.UnderReview ?? 0), href: "/team/buyers?queue=new", icon: "person_search" },
    { label: "فرص قيد الإعداد", n: (o.opportunities.Preparing ?? 0) + (o.opportunities.ReadyToPublish ?? 0), href: "/team/opportunities?status=Preparing", icon: "home_work" },
    { label: "فرص منشورة", n: o.opportunities.Published ?? 0, href: "/team/opportunities?status=Published", icon: "public" },
    { label: "اهتمامات جديدة", n: o.interests.Received ?? 0, href: "/team/interests?status=Received", icon: "handshake" },
    ...(o.contactNew === null ? [] : [{ label: "رسائل تواصل جديدة", n: o.contactNew, href: "/team/messages?status=new", icon: "mail" }]),
  ];
  return (
    <div>
      <TeamHeader title="مهامي والملخص"
        sub={assignedOnly
          ? "تعرض الأرقام والقوائم الأعمال المسندة إليك فقط. يسند مدير العمليات الأعمال الجديدة."
          : "ما يحتاج الفريق: المراجعة، طلب الاستكمال، الاعتماد، تجهيز الفرص، ومتابعة الاهتمامات."} />
      <ul className="m-0 mb-6 grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-3 xl:grid-cols-5">
        {tiles.map((t) => (
          <li key={t.label}>
            <Link href={t.href} className="flex h-full flex-col gap-2 rounded-lg border border-line bg-white p-4 text-ink no-underline shadow-1 hover:border-rust-200">
              <Icon name={t.icon} size={22} className="text-rust" />
              <strong className="text-28 leading-none">{t.n}</strong>
              <span className="text-13 text-charcoal">{t.label}</span>
            </Link>
          </li>
        ))}
      </ul>
      {o.unassigned.sale + o.unassigned.interests > 0 ? (
        <p className="mb-4 flex items-center gap-2 text-14">
          <Badge tone="warn">بدون مسؤول</Badge>
          {o.unassigned.sale} طلب بيع و{o.unassigned.interests} اهتمام —{" "}
          <Link href="/team/sale?assigned=none">اعرضها</Link>
        </p>
      ) : null}
      <Card title="مهامي">
        {o.myTasks.length === 0 ? (
          <p className="m-0 text-14 text-muted">لا توجد مهام مسندة إليك الآن.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-1 p-0">
            {o.myTasks.map((t) => {
              const k = KIND[t.kind];
              return (
                <li key={`${t.kind}-${t.reference}`}>
                  <Link href={k.href(t.reference)} className="flex items-center gap-3 rounded-sm p-2.5 text-ink no-underline hover:bg-subtle">
                    <Icon name={k.icon} size={20} className="text-muted" />
                    <span className="flex flex-1 flex-col">
                      <span className="text-14 font-semibold">{k.label}: {t.title}</span>
                      <span className="text-12 text-muted"><bdi dir="ltr">{t.reference}</bdi> · منذ {day(t.at)}</span>
                    </span>
                    <Icon name="chevron_left" size={20} mirror className="text-muted" />
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </Card>
    </div>
  );
}
