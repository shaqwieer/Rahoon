import type { Metadata } from "next";
import { LoadFailure } from "@/components/case/LoadFailure";
import { getWorkspace } from "@/lib/api/case";
import type { PendingCancellation } from "@/lib/api/lender";
import { apiLoad } from "@/lib/api/load";
import { getMe } from "@/lib/api/server";
import { CancelView, type CancellationHistoryItem } from "./CancelView";

export const metadata: Metadata = { title: "إلغاء الحالة" };

/** C01 — «إلغاء الحالة…»: maker review screen (reason + step-up) and the approver's decision (checker ≠ requester). */
export default async function CancelPage({ params }: PageProps<"/cases/[ref]/cancel">) {
  const { ref } = await params;
  const [ws, list, me] = await Promise.all([
    getWorkspace(ref),
    apiLoad<{ items: CancellationHistoryItem[]; pending: PendingCancellation | null; canRequest: boolean }>(`/cases/${encodeURIComponent(ref)}/cancellation-requests`),
    getMe(),
  ]);

  if (!list.ok) {
    return (
      <section aria-labelledby="cancel-h" className="flex flex-col gap-4">
        <h2 id="cancel-h" className="m-0 text-20 font-bold">إلغاء الحالة</h2>
        <LoadFailure state={list} />
      </section>
    );
  }

  const h = ws.header;
  return (
    <CancelView
      reference={h.reference}
      status={h.status}
      statusLabel={h.statusLabel}
      manager={h.manager}
      openTasks={ws.tasks.length}
      items={list.data.items}
      pending={list.data.pending}
      canRequest={list.data.canRequest}
      hasCancelPermission={me.authenticated && me.permissions.includes("case.cancel")}
      stepUpActive={me.authenticated && me.stepUpActive}
    />
  );
}
