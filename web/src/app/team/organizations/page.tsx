import type { Metadata } from "next";
import Link from "next/link";
import { QueueTabs, TeamHeader, TeamTable } from "@/components/market/team/TeamBits";
import { Badge } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet, can, requireMe } from "@/lib/api/server";
import { ORG_TYPE_LABELS } from "@/lib/market/orgTypes";
import { day } from "@/lib/market/format";
import { FormDropdown } from "@/components/ui/Dropdown";
import { Pagination } from "@/components/ui/Pagination";

export const metadata: Metadata = { title: "دليل الجهات" };

interface Row {
  id: string;
  nameAr: string;
  nameEn: string | null;
  types: string[];
  active: boolean;
  origin: "import" | "manual";
  originLabel: string;
  verifiedOn: string | null;
  sourceName: string | null;
  adminProtected: boolean;
}
interface ListResult {
  items: Row[];
  total: number;
  page: number;
  pages: number;
  pageSize: number;
  counts: { all: number; active: number; developer: number; bank: number; financeCompany: number };
}

const str = (v: string | string[] | undefined) => (typeof v === "string" ? v : "");

/** Directory administration: read with directory.read; add and edit with directory.manage. */
export default async function TeamOrganizations({ searchParams }: PageProps<"/team/organizations">) {
  const me = await requireMe();
  const manage = can(me, "directory.manage");
  const sp = await searchParams;
  const q = str(sp.q), type = str(sp.type), status = str(sp.status), origin = str(sp.origin);
  const page = Number(str(sp.page)) || 1;
  const qs = (over: Record<string, string | number>) => {
    const p = new URLSearchParams();
    for (const [k, v] of Object.entries({ q, type, status, origin, page: 1, ...over })) if (v !== "" && !(k === "page" && v === 1)) p.set(k, String(v));
    const s = p.toString();
    return s ? `?${s}` : "";
  };
  const data = await apiGet<ListResult>(`/team/directory${qs({ page })}`);
  const c = data.counts;

  return (
    <div>
      <TeamHeader
        title="دليل الجهات"
        sub="مطورون عقاريون وبنوك وشركات تمويل حقيقية من مصادر رسمية، يختار منها الملاك والمشترون. لا يُحذف شيء: الجهة تُوقف فقط."
        actions={manage ? <Link href="/team/organizations/new" className={buttonClasses({ variant: "primary" })}>إضافة جهة</Link> : null}
      />
      <QueueTabs
        current={type || "all"}
        tabs={[
          { key: "all", label: "الكل", href: `/team/organizations${qs({ type: "" })}`, count: c.all },
          { key: "developer", label: "مطورون", href: `/team/organizations${qs({ type: "developer" })}`, count: c.developer },
          { key: "bank", label: "بنوك", href: `/team/organizations${qs({ type: "bank" })}`, count: c.bank },
          { key: "finance_company", label: "شركات تمويل", href: `/team/organizations${qs({ type: "finance_company" })}`, count: c.financeCompany },
        ]}
      />
      <form method="get" action="/team/organizations" className="mb-4 flex flex-wrap items-end gap-3" role="search">
        {type ? <input type="hidden" name="type" value={type} /> : null}
        <label className="flex min-w-[220px] flex-1 flex-col gap-1 text-14">
          بحث بالاسم أو الموقع أو الرقم
          <input name="q" defaultValue={q} className="min-h-11 rounded-sm border border-line-strong bg-white px-3 text-15" />
        </label>
        <div className="flex w-44 flex-col gap-1 text-14">
          <span id="org-status-l">الحالة</span>
          <FormDropdown name="status" ariaLabel="الحالة" defaultValue={status}
            options={[{ value: "", label: "الكل" }, { value: "active", label: "مفعّلة" }, { value: "inactive", label: "موقوفة" }]} />
        </div>
        <div className="flex w-52 flex-col gap-1 text-14">
          <span id="org-origin-l">المصدر</span>
          <FormDropdown name="origin" ariaLabel="المصدر" defaultValue={origin}
            options={[{ value: "", label: "الكل" }, { value: "import", label: "استيراد من مصدر" }, { value: "manual", label: "إضافة يدوية" }]} />
        </div>
        <button type="submit" className={buttonClasses({ variant: "secondary" })}>تطبيق</button>
      </form>
      <p className="mb-3 text-14 text-muted">
        {data.total} جهة · {c.active} مفعّلة من {c.all}
      </p>
      <TeamTable
        empty="لا توجد جهات تطابق البحث."
        head={["الجهة", "النوع", "الحالة", "المصدر", "آخر تحقق"]}
        rows={data.items.map((r) => ({
          key: r.id,
          href: `/team/organizations/${r.id}`,
          cells: [
            <span key="n" className="flex flex-col">
              <span>{r.nameAr}</span>
              {r.nameEn ? <bdi dir="ltr" className="text-12 font-normal text-muted">{r.nameEn}</bdi> : null}
            </span>,
            r.types.map((t) => ORG_TYPE_LABELS[t] ?? t).join("، "),
            <Badge key="s" tone={r.active ? "ok" : "neutral"}>{r.active ? "مفعّلة" : "موقوفة"}</Badge>,
            <span key="o" className="flex flex-wrap gap-1">
              {r.originLabel}
              {r.adminProtected ? <Badge tone="info">معدّلة يدويًا</Badge> : null}
            </span>,
            r.verifiedOn ? day(r.verifiedOn) : "—",
          ],
        }))}
      />
      <Pagination className="mt-4" page={data.page} pages={data.pages} total={data.total} pageSize={data.pageSize} hrefFor={(p) => `/team/organizations${qs({ page: p })}`} />
    </div>
  );
}
