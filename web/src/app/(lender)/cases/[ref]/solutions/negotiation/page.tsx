import type { Metadata } from "next";
import { apiGet, getMe } from "@/lib/api/server";
import type { NegotiationData } from "@/lib/api/lender";
import { NegotiationView } from "./NegotiationView";

export const metadata: Metadata = { title: "التفاوض" };

/** L18 — Negotiation: offers, owner counter-requests and internal notes on one timeline, with the next action. */
export default async function NegotiationPage({ params }: PageProps<"/cases/[ref]/solutions/negotiation">) {
  const { ref } = await params;
  const [data, me] = await Promise.all([apiGet<NegotiationData>(`/cases/${encodeURIComponent(ref)}/negotiation`), getMe()]);
  const perms = me.authenticated ? me.permissions : [];
  return (
    <NegotiationView
      reference={ref}
      data={data}
      canManage={perms.includes("negotiation.manage")}
      canPrepare={perms.includes("solution.prepare")}
    />
  );
}
