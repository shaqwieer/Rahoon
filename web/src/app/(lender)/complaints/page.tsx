import type { Metadata } from "next";
import Link from "next/link";
import { LoadFailure } from "@/components/case/LoadFailure";
import { PageHeader } from "@/components/shell/PageHeader";
import { DateText, EmptyState, Ref, SlaBadge, Tabs, Tag } from "@/components/ui";
import { COMPLAINT_STATUS_ICON, COMPLAINT_STATUS_TONE, type ComplaintListItem } from "@/lib/api/complaints";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";

export const metadata: Metadata = { title: "الشكاوى" };

const VIEWS = ["open", "mine", "closed"] as const;

/** L23 — Complaints list. Reviewers (complaint.handle) get the subject; other roles get the API's tag-only rows. */
export default async function ComplaintsPage({ searchParams }: PageProps<"/complaints">) {
  const sp = await searchParams;
  const me = await getMe();
  const isReviewer = me.authenticated && me.permissions.includes("complaint.handle");
  const requested = typeof sp.status === "string" ? sp.status : "open";
  const view = (VIEWS as readonly string[]).includes(requested) && (requested !== "mine" || isReviewer) ? requested : "open";
  const res = await apiLoad<ComplaintListItem[]>(`/complaints?status=${view}`);
  const tabs = [
    { key: "open", label: "المفتوحة", href: "/complaints" },
    ...(isReviewer ? [{ key: "mine", label: "المسندة إليّ", href: "/complaints?status=mine" }] : []),
    { key: "closed", label: "المغلقة", href: "/complaints?status=closed" },
  ];

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="الشكاوى والاعتراضات"
        description={isReviewer
          ? "مراجعة مستقلة للشكوى برد مكتوب ومهلة واضحة. مرتبة حسب أقرب مهلة."
          : "تراجعها جهة مستقلة عن فريق الحالة. يظهر لك وسم الشكوى وحالتها ومهلتها دون نصها."}
      />
      <Tabs label="عرض الشكاوى" active={view} tabs={res.ok ? tabs.map((t) => (t.key === view ? { ...t, count: res.data.length } : t)) : tabs} />
      {!res.ok ? (
        <LoadFailure state={res} forbiddenTitle="لا تملك صلاحية عرض الشكاوى" forbiddenBody="تظهر الشكاوى لمراجعي الامتثال ولمن يملك صلاحية «عرض الشكاوى»." />
      ) : res.data.length === 0 ? (
        <EmptyState icon="support_agent" title={view === "closed" ? "لا شكاوى مغلقة" : view === "mine" ? "لا شكاوى مسندة إليك" : "لا شكاوى مفتوحة"} body="تصلك إشعارات عند استلام شكوى أو اعتراض جديد." />
      ) : (
        <ComplaintsTable items={res.data} />
      )}
    </div>
  );
}

function StatusTag({ item }: { item: Pick<ComplaintListItem, "status" | "statusLabel"> }) {
  return <Tag tone={COMPLAINT_STATUS_TONE[item.status] ?? "neutral"} icon={COMPLAINT_STATUS_ICON[item.status]}>{item.statusLabel}</Tag>;
}

function ComplaintsTable({ items }: { items: ComplaintListItem[] }) {
  const withSubject = items.some((i) => i.subject !== null);
  const open = (s: ComplaintListItem["status"]) => s !== "Resolved" && s !== "Closed";
  return (
    <>
      {/* 768+: table */}
      <div className="hidden overflow-x-auto rounded-lg border border-line bg-white md:block">
        <table className="w-full border-collapse text-14">
          <caption className="sr-only">الشكاوى والاعتراضات</caption>
          <thead>
            <tr className="bg-warm text-muted">
              <th scope="col" className="px-4 py-3 text-start font-semibold">المرجع</th>
              <th scope="col" className="px-4 py-3 text-start font-semibold">الحالة المرتبطة</th>
              <th scope="col" className="px-4 py-3 text-start font-semibold">النوع</th>
              {withSubject ? <th scope="col" className="px-4 py-3 text-start font-semibold">الموضوع</th> : null}
              <th scope="col" className="px-4 py-3 text-start font-semibold">الوضع</th>
              <th scope="col" className="px-4 py-3 text-start font-semibold">المهلة</th>
              <th scope="col" className="px-4 py-3 text-start font-semibold">المراجِع</th>
            </tr>
          </thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.reference} className="border-t border-divider">
                <th scope="row" className="px-4 py-3 text-start font-normal">
                  <Link href={`/complaints/${i.reference}`}><Ref>{i.reference}</Ref></Link>
                </th>
                <td className="px-4 py-3"><Link href={`/cases/${i.caseRef}`}><Ref strong={false}>{i.caseRef}</Ref></Link></td>
                <td className="px-4 py-3">{i.type}</td>
                {withSubject ? <td className="max-w-[360px] px-4 py-3">{i.subject ?? "—"}</td> : null}
                <td className="px-4 py-3"><StatusTag item={i} /></td>
                <td className="px-4 py-3">
                  {open(i.status) ? <SlaBadge tone={i.slaTone} size="sm">{i.slaText}</SlaBadge> : <DateText value={i.dueOn} />}
                </td>
                <td className="px-4 py-3">{i.reviewer ?? "بانتظار الإسناد"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {/* <768: cards */}
      <ul className="m-0 flex list-none flex-col gap-2 p-0 md:hidden" aria-label="الشكاوى والاعتراضات">
        {items.map((i) => (
          <li key={i.reference}>
            <Link href={`/complaints/${i.reference}`} className="flex flex-col gap-1.5 rounded-md border border-line bg-white p-4 text-ink no-underline hover:bg-warm">
              <span className="flex items-center justify-between gap-2">
                <Ref>{i.reference}</Ref>
                <StatusTag item={i} />
              </span>
              <span className="text-13 text-muted">{i.type} · الحالة <bdi dir="ltr" className="font-mono">{i.caseRef}</bdi></span>
              {i.subject ? <strong className="text-15">{i.subject}</strong> : null}
              <span className="flex flex-wrap items-center gap-2 text-13 text-muted">
                {open(i.status) ? <SlaBadge tone={i.slaTone} size="sm">{i.slaText}</SlaBadge> : null}
                {i.reviewer ?? "بانتظار الإسناد"}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
