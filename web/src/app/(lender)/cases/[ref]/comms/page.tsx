import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import type { CaseCommsData, OrgMember } from "@/lib/api/comms";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";
import { CommsView } from "./CommsView";

export const metadata: Metadata = { title: "التواصل والمهام" };

/** L22 — Case communication & tasks: owner lane vs internal notes, composer with templates, appointments, case tasks. */
export default async function CaseCommsPage({ params }: PageProps<"/cases/[ref]/comms">) {
  const { ref } = await params;
  const me = await getMe();
  const perms = me.authenticated ? me.permissions : [];
  const canManageTasks = perms.includes("task.manage");
  const [comms, members] = await Promise.all([
    apiLoad<CaseCommsData>(`/cases/${encodeURIComponent(ref)}/comms`),
    canManageTasks ? apiLoad<OrgMember[]>("/org/members") : Promise.resolve(null),
  ]);
  if (!comms.ok) return <LoadFailure state={comms} />;
  return (
    <CommsView
      reference={ref}
      data={comms.data}
      canSend={perms.includes("comms.send")}
      canManageTasks={canManageTasks}
      members={members?.ok ? members.data : []}
      myName={me.authenticated ? me.user.name : null}
    />
  );
}
