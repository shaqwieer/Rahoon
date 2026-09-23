"use client";

import type { ReactNode } from "react";
import { Icon } from "@/components/ui/Icon";
import { Menu, type MenuItemDef } from "@/components/ui/Menu";
import { StageProgress } from "@/components/ui/StageProgress";
import { SlaBadge, StatusChip } from "@/components/ui/Status";
import { Tabs, type TabDef } from "@/components/ui/Tabs";
import type { CaseStatusKey } from "@/components/ui/tones";
import { cn } from "@/lib/cn";
import type { SlaTone } from "@/lib/format";
import { useI18n } from "@/lib/i18n/client";

export interface CaseHeaderProps {
  caseRef: string;
  /** «تمويل سكني · مرابحة». */
  productLabel?: string;
  lenderName?: string;
  /** Masked owner + property, e.g. «عبدالله م. — فيلا سكنية، حي النرجس، الرياض». Rendered as the page h1. */
  title: string;
  statusKey: CaseStatusKey;
  slaTone?: SlaTone;
  slaText?: string;
  /** Case owner (المسؤول). */
  ownerName?: string;
  /** Current stage index 0–6. */
  stage: number;
  /** Custom stage names (path-specific tails such as the referral path). */
  stageNames?: string[];
  tabs: TabDef[];
  activeTab: string;
  /** Extra tabs appended after the standard ones (e.g. «البيع الطوعي»). */
  extraTabs?: TabDef[];
  /** «إجراءات أخرى» menu items; or pass `actionsSlot` for a custom control. */
  actions?: MenuItemDef[];
  actionsSlot?: ReactNode;
  /** Cancels the lender `main` padding so the header spans edge to edge (default true). */
  bleed?: boolean;
}

/** Standard case tab keys → routes, for callers building `tabs`. */
export const CASE_TAB_KEYS = ["overview", "parties", "finance", "property", "documents", "valuation", "solutions", "payments", "comms", "audit"] as const;

/**
 * CaseHeader.dc: reference · product · lender, title, status chip, SLA, owner, 7-stage bar, tablist.
 * Below 768 it compacts to «المرحلة n من 7» and a horizontally scrollable tab strip.
 */
export function CaseHeader({
  caseRef,
  productLabel,
  lenderName,
  title,
  statusKey,
  slaTone,
  slaText,
  ownerName,
  stage,
  stageNames,
  tabs,
  activeTab,
  extraTabs,
  actions,
  actionsSlot,
  bleed = true,
}: CaseHeaderProps) {
  const { t } = useI18n();
  const allTabs = [...tabs, ...(extraTabs ?? [])];
  const actionControl =
    actionsSlot ??
    (actions?.length ? (
      <Menu
        label={t.caseHeader.moreActions}
        align="end"
        triggerClassName="inline-flex min-h-10 items-center gap-1.5 rounded-sm border border-line-strong bg-white px-3.5 text-14 font-semibold hover:bg-subtle"
        trigger={
          <>
            {t.caseHeader.moreActions}
            <Icon name="expand_more" size={18} />
          </>
        }
        items={actions}
      />
    ) : null);

  return (
    <header
      className={cn(
        "flex flex-col gap-3 border-b border-line bg-white px-4 pt-3 md:px-10 md:pt-[18px]",
        bleed && "-mx-4 -mt-4 mb-5 md:-mx-10 md:-mt-7 md:mb-7",
      )}
    >
      <div className="flex items-start gap-4">
        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          <div className="flex flex-wrap items-center gap-2.5 text-13 text-muted">
            <bdi dir="ltr" className="font-mono text-14 font-semibold text-ink">
              {caseRef}
            </bdi>
            {productLabel ? (
              <>
                <span aria-hidden="true">·</span>
                <span>{productLabel}</span>
              </>
            ) : null}
            {lenderName ? (
              <>
                <span aria-hidden="true">·</span>
                <span>{lenderName}</span>
              </>
            ) : null}
          </div>
          <h1 className="m-0 text-18 leading-7 font-bold md:text-24 md:leading-9">{title}</h1>
          <div className="flex flex-wrap items-center gap-2.5">
            <StatusChip status={statusKey} />
            {slaText ? <SlaBadge tone={slaTone ?? "info"}>{slaText}</SlaBadge> : null}
            {ownerName ? (
              <span className="inline-flex items-center gap-1.5 text-13">
                <Icon name="person" size={16} className="text-muted" />
                {t.caseHeader.owner}: {ownerName}
              </span>
            ) : null}
          </div>
        </div>
        {actionControl ? <div className="flex-none">{actionControl}</div> : null}
      </div>
      <div className="hidden md:block">
        <StageProgress current={stage} names={stageNames} variant="header" />
      </div>
      <div className="md:hidden">
        <StageProgress current={stage} names={stageNames} variant="compact" />
      </div>
      <Tabs label={t.caseTabs.label} tabs={allTabs} active={activeTab} bare className="-mx-2" />
    </header>
  );
}
