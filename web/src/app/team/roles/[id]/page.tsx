import type { Metadata } from "next";
import Link from "next/link";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { Badge, Card } from "@/components/market/ui";
import { RoleEditor } from "@/components/team/admin/RoleEditor";
import { apiGet } from "@/lib/api/server";
import { MEMBER_STATUS, type Catalog, type MemberStatus, type RoleRow, type Scope } from "@/lib/team/admin";

export const metadata: Metadata = { title: "دور" };

interface RoleDetail {
  role: RoleRow;
  holders: { id: string; name: string; status: MemberStatus }[];
  actions: { edit: boolean; archive: boolean; blocked: string | null };
  grantable: Record<string, Scope>;
}

export default async function RolePage({ params }: PageProps<"/team/roles/[id]">) {
  const { id } = await params;
  const [d, catalog] = await Promise.all([
    apiGet<RoleDetail>(`/team/admin/roles/${encodeURIComponent(id)}`),
    apiGet<Catalog>("/team/admin/catalog"),
  ]);
  const r = d.role;
  return (
    <div className="grid max-w-[1200px] gap-6 xl:grid-cols-[1fr_320px]">
      <div className="flex min-w-0 flex-col gap-4">
        <TeamHeader back={{ href: "/team/roles", label: "الأدوار والصلاحيات" }}
          title={<span className="flex flex-wrap items-center gap-2">{r.nameAr}<Badge tone={r.isSystem ? "neutral" : "info"}>{r.isSystem ? "نظامي" : "مخصص"}</Badge>{r.archived ? <Badge tone="warn">مؤرشف</Badge> : null}</span>}
          sub={r.descriptionAr ?? undefined} />
        <RoleEditor catalog={catalog} role={r} grantable={d.grantable} blocked={d.actions.blocked} canArchive={d.actions.archive} />
      </div>
      <aside>
        <Card title={`الأعضاء (${d.holders.length})`}>
          {d.holders.length === 0 ? <p className="m-0 text-14 text-muted">لا يحمل هذا الدور أحد.</p> : (
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {d.holders.map((h) => (
                <li key={h.id} className="flex items-center justify-between gap-2 text-14">
                  <Link href={`/team/members/${h.id}`}>{h.name}</Link>
                  <Badge tone={MEMBER_STATUS[h.status].tone}>{MEMBER_STATUS[h.status].label}</Badge>
                </li>
              ))}
            </ul>
          )}
          {!d.actions.archive && !r.isSystem && !r.archived && d.holders.length > 0 ? (
            <p className="m-0 mt-3 text-13 text-muted">للأرشفة: غيّر أدوار أعضائه أولًا.</p>
          ) : null}
        </Card>
      </aside>
    </div>
  );
}
