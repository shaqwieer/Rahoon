import type { Metadata } from "next";
import { TeamHeader } from "@/components/market/team/TeamBits";
import { Badge, Card, Row } from "@/components/market/ui";
import { MemberAdmin } from "@/components/team/admin/MemberAdmin";
import { apiGet } from "@/lib/api/server";
import { dayTime } from "@/lib/market/format";
import { MEMBER_STATUS, SCOPE_LABEL, type MemberDetail } from "@/lib/team/admin";

export const metadata: Metadata = { title: "عضو في الفريق" };

/** One member: roles, effective access (which role grants what, and how far), open work, status actions, history. */
export default async function MemberPage({ params }: PageProps<"/team/members/[id]">) {
  const { id } = await params;
  const [m, members] = await Promise.all([
    apiGet<MemberDetail>(`/team/admin/members/${encodeURIComponent(id)}`),
    apiGet<{ items: { userId: string; name: string; status: string; self: boolean }[] }>("/team/admin/members?status=active"),
  ]);
  const st = MEMBER_STATUS[m.status];
  const byArea = new Map<string, MemberDetail["effective"]>();
  for (const e of m.effective) byArea.set(e.area, [...(byArea.get(e.area) ?? []), e]);

  return (
    <div className="grid max-w-[1200px] gap-6 xl:grid-cols-[1fr_360px]">
      <div className="flex min-w-0 flex-col gap-6">
        <TeamHeader
          back={{ href: "/team/members", label: "الفريق" }}
          title={<span className="flex flex-wrap items-center gap-2">{m.name}{m.self ? " (أنت)" : ""} <Badge tone={st.tone}>{st.label}</Badge>{m.isOwner ? <Badge tone="rust">مالك المنصة</Badge> : null}</span>}
          sub={m.title ?? undefined}
        />
        <MemberAdmin
          member={m}
          others={members.items.filter((x) => x.userId !== m.userId).map((x) => ({ userId: x.userId, name: x.name }))}
        />
        <Card title="الصلاحيات الفعلية">
          <p className="m-0 mb-3 text-13 text-muted">
            ناتج كل أدواره معًا. النطاق محسوب لكل صلاحية وحدها: صلاحية «لكل الفريق» لا توسّع نطاق صلاحية أخرى.
          </p>
          {m.effective.length === 0 ? <p className="m-0 text-14 text-muted">لا يملك أي صلاحية.</p> : (
            <div className="flex flex-col gap-4">
              {[...byArea.entries()].map(([area, list]) => (
                <div key={area}>
                  <ul className="m-0 flex list-none flex-col gap-2 p-0">
                    {list.map((e) => (
                      <li key={e.key} className="flex flex-col gap-1 rounded-md bg-subtle px-3 py-2 sm:flex-row sm:items-center sm:justify-between">
                        <span className="flex flex-col">
                          <span className="text-14 font-semibold">{e.nameAr}</span>
                          <span className="text-12 text-muted">من: {e.from.map((f) => `${f.roleName}${e.scopable ? ` (${SCOPE_LABEL[f.scope]})` : ""}`).join("، ")}</span>
                        </span>
                        {e.scopable ? <Badge tone={e.scope === "all" ? "info" : "neutral"}>{SCOPE_LABEL[e.scope]}</Badge> : null}
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          )}
        </Card>
        <Card title="سجل العضوية">
          {m.history.length === 0 ? <p className="m-0 text-14 text-muted">لا توجد أحداث بعد.</p> : (
            <ol className="m-0 flex list-none flex-col gap-2 p-0">
              {m.history.map((h) => (
                <li key={h.seq} className="flex flex-col border-b border-divider pb-2 text-14 last:border-b-0">
                  <span className="font-semibold">{h.title}</span>
                  <span className="text-12 text-muted">{h.actorLabel ?? "النظام"} · {dayTime(h.occurredAt)}{h.reason ? ` · السبب: ${h.reason}` : ""}</span>
                </li>
              ))}
            </ol>
          )}
        </Card>
      </div>
      <aside className="flex flex-col gap-4">
        <Card title="الحساب">
          <dl className="m-0 flex flex-col">
            <Row label="البريد"><bdi dir="ltr">{m.email}</bdi></Row>
            <Row label="الجوال">{m.phoneMasked ? <bdi dir="ltr">{m.phoneMasked}</bdi> : "—"}</Row>
            <Row label="عضو منذ">{dayTime(m.createdAt)}</Row>
            <Row label="آخر دخول">{m.lastLoginAt ? dayTime(m.lastLoginAt) : "لم يدخل بعد"}</Row>
            <Row label="جلسات نشطة">{m.activeSessions}</Row>
            {m.statusReason ? <Row label="سبب الحالة" hint={m.statusChangedAt ? dayTime(m.statusChangedAt) : undefined}>{m.statusReason}</Row> : null}
          </dl>
        </Card>
        <Card title="أعمال مفتوحة مسندة إليه">
          <dl className="m-0 flex flex-col">
            <Row label="طلبات بيع">{m.workload.saleRequests}</Row>
            <Row label="طلبات شراء">{m.workload.buyerRequests}</Row>
            <Row label="فرص">{m.workload.opportunities}</Row>
            <Row label="اهتمامات">{m.workload.interests}</Row>
          </dl>
        </Card>
      </aside>
    </div>
  );
}
