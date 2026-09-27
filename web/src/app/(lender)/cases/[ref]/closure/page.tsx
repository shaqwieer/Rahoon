import type { Metadata } from "next";
import type { ClosureOverview } from "@/lib/api/closure";
import { apiGet, can, requireMe } from "@/lib/api/server";
import { ClosureView } from "./ClosureView";

export const metadata: Metadata = { title: "التسوية والإغلاق" };

/**
 * L26 — manual reconciliation and closure on the settlement path: preparer ≠ reviewer ≠ approver, a step-up for the
 * approvals, the closure documents, then a closure request decided by a person who took no part (Phase 1A-2 step 7).
 * Permissions below are UI hints only; the API decides and answers with the reason.
 */
export default async function ClosurePage({ params }: PageProps<"/cases/[ref]/closure">) {
  const { ref } = await params;
  const [me, d] = await Promise.all([requireMe(), apiGet<ClosureOverview>(`/cases/${encodeURIComponent(ref)}/closure`)]);
  return (
    <ClosureView
      reference={ref}
      d={d}
      me={me.user.name}
      perms={{ prepare: can(me, "reconciliation.prepare"), approve: can(me, "reconciliation.approve"), upload: can(me, "document.upload") }}
    />
  );
}
