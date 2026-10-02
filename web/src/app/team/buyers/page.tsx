import type { Metadata } from "next";
import { QueueTabs, TeamHeader, TeamTable } from "@/components/market/team/TeamBits";
import { Amount, StatusBadge } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import { day, FREQ_PER } from "@/lib/market/format";
import { Pagination } from "@/components/ui/Pagination";

export const metadata: Metadata = { title: "طلبات المشترين" };

interface Row { reference: string; status: string; statusLabel: string; name: string | null; availableNow: number | null; installmentComfort: number | null; installmentFrequency: string | null; cities: string[]; types: string[]; purchaseMode: string | null; statusChangedAt: string; assignedTo: string | null; financeApprovalStatus: string }

interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; pages: number }

const QUEUES = [{ key: "new", label: "جديدة" }, { key: "review", label: "قيد المراجعة" }, { key: "completion", label: "بانتظار الاستكمال" }, { key: "approved", label: "معتمدة للمطابقة" }, { key: "closed", label: "مغلقة" }, { key: "all", label: "الكل" }];

export default async function TeamBuyers({ searchParams }: PageProps<"/team/buyers">) {
  const sp = await searchParams;
  const queue = typeof sp.queue === "string" ? sp.queue : "all";
  const page = Number(typeof sp.page === "string" ? sp.page : "1") || 1;
  const data = await apiGet<Paged<Row>>(`/team/market/buyer-requests?queue=${encodeURIComponent(queue)}&page=${page}`);
  const rows = data.items;
  return (
    <div>
      <TeamHeader title="طلبات المشترين" sub="القدرة المصرّح بها منفصلة عن الإثبات الذي يراجعه الفريق وعن موافقة جهة التمويل." />
      <QueueTabs current={queue} tabs={QUEUES.map((q) => ({ ...q, href: `/team/buyers?queue=${q.key}` }))} />
      <TeamTable empty="لا توجد طلبات هنا." head={["الطلب", "الاسم", "المتاح الآن", "القسط", "المدن والأنواع", "الحالة", "آخر تغيير"]}
        rows={rows.map((r) => ({
          key: r.reference, href: `/team/buyers/${r.reference}`,
          cells: [
            <bdi key="r" dir="ltr" className="font-mono">{r.reference}</bdi>, r.name ?? "—", <Amount key="a" value={r.availableNow} size="sm" />,
            r.installmentComfort ? <span key="i"><Amount value={r.installmentComfort} size="sm" /> {FREQ_PER[r.installmentFrequency ?? ""]}</span> : "—",
            `${r.cities.join("، ")} · ${r.types.join("، ")}`, <StatusBadge key="s" status={r.status} label={r.statusLabel} />, day(r.statusChangedAt),
          ],
        }))} />
      <Pagination className="mt-4" page={data.page} pages={data.pages} total={data.total} pageSize={data.pageSize}
        hrefFor={(p) => `/team/buyers?queue=${encodeURIComponent(queue)}${p > 1 ? `&page=${p}` : ""}`} />
    </div>
  );
}
