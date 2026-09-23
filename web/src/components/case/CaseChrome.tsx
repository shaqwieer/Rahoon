"use client";

import { usePathname } from "next/navigation";
import { useState, type ReactNode } from "react";
import { CaseHeader } from "@/components/shell/CaseHeader";
import type { MenuItemDef, TabDef } from "@/components/ui";
import { toSlaTone, type AvailableAction, type WorkspaceData } from "@/lib/api/lender";
import { TransitionDialog } from "./TransitionDialog";

/** Case header + tab strip shared by every case page; active tab derives from the URL. */
export function CaseChrome({ ws, children }: { ws: WorkspaceData; children: ReactNode }) {
  const pathname = usePathname();
  const h = ws.header;
  const base = `/cases/${h.reference}`;
  const segment = pathname.slice(base.length).split("/")[1] ?? "";
  // The agreement (L19) has no tab of its own: the header keeps «الحلول» selected, as in the design.
  const active = segment === "" ? "overview" : segment === "agreement" ? "solutions" : segment;
  const [dialog, setDialog] = useState<AvailableAction | null>(null);

  const tabs: TabDef[] = [
    { key: "overview", label: "نظرة عامة", href: base },
    { key: "parties", label: "الأطراف", href: `${base}/parties` },
    { key: "finance", label: "التمويل والمديونية", href: `${base}/finance` },
    { key: "property", label: "العقار والرهن", href: `${base}/property` },
    { key: "documents", label: "المستندات", href: `${base}/documents`, count: ws.tabs.documentsBadge ? Number(ws.tabs.documentsBadge) : undefined },
    { key: "valuation", label: "التقييم والتحليل", href: `${base}/valuation` },
    { key: "solutions", label: ws.tabs.solutionsBadge ? `الحلول · ${ws.tabs.solutionsBadge}` : "الحلول", href: `${base}/solutions` },
    { key: "payments", label: "المدفوعات", href: `${base}/payments` },
    { key: "comms", label: "التواصل والمهام", href: `${base}/comms`, count: ws.tabs.commsBadge ? Number(ws.tabs.commsBadge) : undefined },
    { key: "audit", label: "السجل", href: `${base}/audit` },
  ];
  const extra: TabDef[] = [
    ...(ws.tabs.showSale ? [{ key: "sale", label: "البيع الطوعي", href: `${base}/sale` }] : []),
    ...(ws.tabs.showReferral ? [{ key: "referral", label: "الإحالة", href: `${base}/referral` }] : []),
    ...(ws.tabs.showClosure ? [{ key: "closure", label: "الإغلاق", href: `${base}/closure` }] : []),
  ];

  // «إجراءات أخرى»: manual transitions the caller may perform (disabled ones still open the review with reasons).
  const menu: MenuItemDef[] = [...ws.actions, ...ws.sensitive.actions].map((a) => ({
    key: a.key,
    label: `${a.labelAr}${a.requiresReason ? "…" : ""}`,
    icon: a.key === "pause" ? "pause_circle" : a.key === "resume" ? "play_circle" : "swap_horiz",
    onSelect: () => setDialog(a),
  }));

  return (
    <>
      <CaseHeader
        caseRef={h.reference}
        productLabel={h.product}
        lenderName={h.lender}
        title={h.title}
        statusKey={h.status}
        slaTone={toSlaTone(h.slaTone)}
        slaText={h.slaText}
        ownerName={h.manager ?? undefined}
        stage={h.stage}
        stageNames={h.stageNames}
        tabs={tabs}
        extraTabs={extra}
        activeTab={active}
        actions={menu}
      />
      {children}
      <TransitionDialog reference={h.reference} action={dialog} expectedStatus={h.status} open={dialog !== null} onClose={() => setDialog(null)} />
    </>
  );
}
