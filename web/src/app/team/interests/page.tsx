import type { Metadata } from "next";
import { QueueTabs, TeamHeader, TeamTable } from "@/components/market/team/TeamBits";
import { StatusBadge } from "@/components/market/ui";
import { apiGet } from "@/lib/api/server";
import { day } from "@/lib/market/format";
import { Pagination } from "@/components/ui/Pagination";

export const metadata: Metadata = { title: "الاهتمامات" };

interface Row { reference: string; status: string; statusLabel: string; opportunity: string; opportunityTitle: string; opportunityStatusLabel: string; buyerName: string | null; createdAt: string; assignedTo: string | null; hasBuyerRequest: boolean }

interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; pages: number }

const TABS = [{ key: "", label: "الكل" }, { key: "Received", label: "جديدة" }, { key: "InFollowUp", label: "قيد المتابعة" }, { key: "Closed", label: "مغلقة" }, { key: "Withdrawn", label: "مسحوبة" }];

export default async function TeamInterests({ searchParams }: PageProps<"/team/interests">) {
  const sp = await searchParams;
  const status = typeof sp.status === "string" ? sp.status : "";
  const page = Number(typeof sp.page === "string" ? sp.page : "1") || 1;
  const data = await apiGet<Paged<Row>>(`/team/market/interests?page=${page}${status ? `&status=${encodeURIComponent(status)}` : ""}`);
  const rows = data.items;
  return (
    <div>
      <TeamHeader title="الاهتمامات" sub="كل اهتمام مرتبط بفرصة وبإصدار أرقامها وبملف المشتري إن وجد. لا يحجز العقار." />
      <QueueTabs current={status} tabs={TABS.map((t) => ({ ...t, href: t.key ? `/team/interests?status=${t.key}` : "/team/interests" }))} />
      <TeamTable empty="لا توجد اهتمامات هنا." head={["الاهتمام", "المشتري", "الفرصة", "الحالة", "المسؤول", "التاريخ"]}
        rows={rows.map((r) => ({
          key: r.reference, href: `/team/interests/${r.reference}`,
          cells: [<bdi key="r" dir="ltr" className="font-mono">{r.reference}</bdi>, `${r.buyerName ?? "—"}${r.hasBuyerRequest ? "" : " (بدون طلب شراء)"}`, r.opportunityTitle,
            <StatusBadge key="s" status={r.status} label={r.statusLabel} />, r.assignedTo ?? "—", day(r.createdAt)],
        }))} />
      <Pagination className="mt-4" page={data.page} pages={data.pages} total={data.total} pageSize={data.pageSize}
        hrefFor={(p) => `/team/interests?${status ? `status=${encodeURIComponent(status)}&` : ""}page=${p}`} />
    </div>
  );
}
