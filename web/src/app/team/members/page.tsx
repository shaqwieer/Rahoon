import type { Metadata } from "next";
import Link from "next/link";
import { InvitationList } from "@/components/team/admin/InvitationList";
import { QueueTabs, TeamHeader, TeamTable } from "@/components/market/team/TeamBits";
import { Badge, Card } from "@/components/market/ui";
import { buttonClasses } from "@/components/ui/buttonStyles";
import { apiGet } from "@/lib/api/server";
import { dayTime } from "@/lib/market/format";
import { MEMBER_STATUS, type InvitationRow, type MemberRow } from "@/lib/team/admin";

export const metadata: Metadata = { title: "الفريق" };

interface MembersResult { items: MemberRow[]; counts: Record<string, number>; pendingInvitations: number; canManage: boolean }

const str = (v: string | string[] | undefined) => (typeof v === "string" ? v : "");

/** Team members (team.read): status tabs, search, roles, open work; pending invitations below. */
export default async function TeamMembers({ searchParams }: PageProps<"/team/members">) {
  const sp = await searchParams;
  const status = str(sp.status);
  const q = str(sp.q);
  const qs = (over: Record<string, string>) => {
    const p = new URLSearchParams();
    for (const [k, v] of Object.entries({ status, q, ...over })) if (v) p.set(k, v);
    const s = p.toString();
    return s ? `?${s}` : "";
  };
  const [data, invitations] = await Promise.all([
    apiGet<MembersResult>(`/team/admin/members${qs({})}`),
    apiGet<{ items: InvitationRow[]; canManage: boolean }>("/team/admin/invitations"),
  ]);
  const c = data.counts;
  const total = Object.values(c).reduce((a, b) => a + b, 0);

  return (
    <div>
      <TeamHeader
        title="الفريق"
        sub="أعضاء فريق رهون وأدوارهم. لا يُحذف عضو: يُوقف أو يُزال من الفريق ويبقى سجله وكل ما قام به."
        actions={data.canManage ? <Link href="/team/members/invite" className={buttonClasses({ variant: "primary" })}>دعوة عضو</Link> : null}
      />
      <QueueTabs
        current={status || "all"}
        tabs={[
          { key: "all", label: "الكل", href: `/team/members${qs({ status: "" })}`, count: total },
          { key: "active", label: "نشط", href: `/team/members${qs({ status: "active" })}`, count: c.active ?? 0 },
          { key: "suspended", label: "موقوف", href: `/team/members${qs({ status: "suspended" })}`, count: c.suspended ?? 0 },
          { key: "revoked", label: "أُزيل", href: `/team/members${qs({ status: "revoked" })}`, count: c.revoked ?? 0 },
        ]}
      />
      <form method="get" action="/team/members" className="mb-4 flex flex-wrap items-end gap-3" role="search">
        {status ? <input type="hidden" name="status" value={status} /> : null}
        <label className="flex min-w-[220px] flex-1 flex-col gap-1 text-14">
          بحث بالاسم أو البريد أو المسمى
          <input name="q" defaultValue={q} className="min-h-11 rounded-sm border border-line-strong bg-white px-3 text-15" />
        </label>
        <button type="submit" className={buttonClasses({ variant: "secondary" })}>بحث</button>
      </form>
      <TeamTable
        empty="لا يوجد أعضاء يطابقون البحث."
        head={["العضو", "الأدوار", "الحالة", "أعمال مفتوحة", "آخر دخول"]}
        rows={data.items.map((m) => ({
          key: m.id,
          href: `/team/members/${m.id}`,
          cells: [
            <span key="n" className="flex flex-col">
              <span>{m.name}{m.self ? " (أنت)" : ""}</span>
              <bdi dir="ltr" className="text-12 font-normal text-muted">{m.email}</bdi>
            </span>,
            <span key="r" className="flex flex-wrap gap-1">{m.roles.map((r) => <Badge key={r.id} tone={r.isSystem ? "neutral" : "info"}>{r.nameAr}</Badge>)}</span>,
            <Badge key="s" tone={MEMBER_STATUS[m.status].tone}>{MEMBER_STATUS[m.status].label}</Badge>,
            String(m.openWork),
            m.lastLoginAt ? dayTime(m.lastLoginAt) : "لم يدخل بعد",
          ],
        }))}
      />
      <Card title={`الدعوات (${data.pendingInvitations} بانتظار القبول)`} className="mt-6">
        <InvitationList items={invitations.items} canManage={invitations.canManage} />
      </Card>
    </div>
  );
}
