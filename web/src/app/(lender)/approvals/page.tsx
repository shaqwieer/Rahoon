import type { Metadata } from "next";
import { apiGet } from "@/lib/api/server";
import { ApprovalInbox, type ApprovalDetail, type InboxItem } from "./ApprovalInbox";

export const metadata: Metadata = { title: "الموافقات" };

/** L16 — Approval inbox (sorted by deadline) with the decision pane for the selected request. */
export default async function ApprovalsPage({ searchParams }: PageProps<"/approvals">) {
  const sp = await searchParams;
  const status = sp.status === "decided" ? "decided" : "pending";
  const inbox = await apiGet<{ items: InboxItem[] }>(`/approvals?status=${status}`);
  const selectedId = typeof sp.request === "string" ? sp.request : inbox.items.find((i) => i.canDecide)?.id ?? inbox.items[0]?.id;
  const detail = selectedId ? await apiGet<ApprovalDetail>(`/approvals/${selectedId}`).catch(() => null) : null;
  return <ApprovalInbox items={inbox.items} status={status} detail={detail} />;
}
