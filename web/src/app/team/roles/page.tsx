import type { Metadata } from "next";
import Link from "next/link";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { Badge, Card } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import type { Catalog, RoleRow } from "@/lib/team/admin";

export const metadata: Metadata = { title: "الأدوار والصلاحيات" };

const CELL: Record<string, { text: string; className: string; label: string }> = {
  all: { text: "الكل", className: "bg-rust-50 text-rust-700 font-bold", label: "لكل أعمال الفريق" },
  assigned: { text: "المسند", className: "bg-subtle font-semibold", label: "للمسند إليه فقط" },
};

/** Roles (roles.read): protected system roles and custom roles, and the permission matrix of who holds what and how far. */
export default async function TeamRoles() {
  const [roles, catalog] = await Promise.all([
    apiGet<{ items: RoleRow[]; canManage: boolean }>("/team/admin/roles"),
    apiGet<Catalog>("/team/admin/catalog"),
  ]);
  const active = roles.items.filter((r) => !r.archived);
  const archived = roles.items.filter((r) => r.archived);
  const grant = (r: RoleRow, key: string) => r.grants.find((g) => g.key === key)?.scope;

  return (
    <div>
      <TeamHeader
        title="الأدوار والصلاحيات"
        sub="الأدوار النظامية ثابتة. لمزيج آخر أنشئ دورًا مخصصًا: يغيّر من يملك الصلاحية، لا معنى الإجراء نفسه."
        actions={roles.canManage ? <Link href="/team/roles/new" className={buttonClasses({ variant: "primary" })}>دور مخصص جديد</Link> : null}
      />
      <ul className="m-0 mb-6 grid list-none gap-3 p-0 md:grid-cols-2 xl:grid-cols-3">
        {active.map((r) => (
          <li key={r.id}>
            <Link href={`/team/roles/${r.id}`} className="flex h-full flex-col gap-2 rounded-lg border border-line bg-white p-4 text-ink no-underline shadow-1 hover:border-rust-200">
              <span className="flex flex-wrap items-center gap-2">
                <strong className="text-16">{r.nameAr}</strong>
                <Badge tone={r.isSystem ? "neutral" : "info"}>{r.isSystem ? "نظامي" : "مخصص"}</Badge>
              </span>
              {r.descriptionAr ? <span className="text-13 text-charcoal">{r.descriptionAr}</span> : null}
              <span className="mt-auto text-12 text-muted">{r.members} {r.members === 1 ? "عضو" : "أعضاء"} · {r.grants.length} صلاحية</span>
            </Link>
          </li>
        ))}
      </ul>

      <Card title="مصفوفة الصلاحيات">
        <p className="m-0 mb-3 text-13 text-muted">
          «الكل»: لكل أعمال الفريق. «المسند»: للأعمال المسندة إلى العضو فقط. الصلاحيات المعلّمة «لمرحلة لاحقة» محجوزة ولا تُمنح بعد.
        </p>
        <div className="-mx-4 overflow-x-auto px-4 md:mx-0 md:px-0">
          <table className="w-full min-w-[900px] border-collapse text-13">
            <caption className="sr-only">الصلاحيات حسب الدور ونطاقها</caption>
            <thead>
              <tr className="bg-subtle">
                <th scope="col" className="sticky start-0 z-10 bg-subtle px-3 py-2 text-start">الصلاحية</th>
                {active.map((r) => <th key={r.id} scope="col" className="px-2 py-2 text-center font-semibold">{r.nameAr}</th>)}
              </tr>
            </thead>
            <tbody>
              {catalog.areas.map((area) => (
                <AreaRows key={area.key} area={area} roles={active} grant={grant} />
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {archived.length ? (
        <Card title="أدوار مؤرشفة" className="mt-6">
          <ul className="m-0 flex list-none flex-wrap gap-2 p-0">
            {archived.map((r) => <li key={r.id}><Link href={`/team/roles/${r.id}`}>{r.nameAr}</Link></li>)}
          </ul>
        </Card>
      ) : null}
    </div>
  );
}

function AreaRows({ area, roles, grant }: { area: Catalog["areas"][number]; roles: RoleRow[]; grant: (r: RoleRow, key: string) => string | undefined }) {
  return (
    <>
      <tr>
        <th colSpan={roles.length + 1} scope="colgroup" className="border-t border-line bg-warm px-3 py-1.5 text-start text-12 font-bold text-muted">{area.nameAr}</th>
      </tr>
      {area.permissions.map((p) => (
        <tr key={p.key} className="border-t border-divider">
          <th scope="row" className="sticky start-0 z-10 bg-white px-3 py-2 text-start font-normal">
            {p.nameAr}
            {p.reserved ? <span className="ms-1 text-12 text-muted">(لمرحلة لاحقة {p.reservedFor})</span> : null}
          </th>
          {roles.map((r) => {
            const s = grant(r, p.key);
            const cell = s ? CELL[s] : null;
            return (
              <td key={r.id} className="px-1 py-1 text-center">
                {cell ? <span className={`inline-block rounded-sm px-2 py-0.5 ${cell.className}`} title={cell.label}>{p.scopable ? cell.text : "✓"}</span> : <span className="text-soft" aria-label="لا">—</span>}
              </td>
            );
          })}
        </tr>
      ))}
    </>
  );
}
