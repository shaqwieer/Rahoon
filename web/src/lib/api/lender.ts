import type { CaseStatusKey } from "@/components/ui/tones";
import type { SlaTone } from "@/lib/format";

/** API DTOs for the lender workspace (mirrors server/src/Rahoon.Api/Modules/*Endpoints.cs). */

export type ApiSlaTone = SlaTone | "none";

/** The API also returns "none" for terminal cases; the UI renders that as a neutral paused-style badge. */
export function toSlaTone(t: ApiSlaTone | string | null | undefined): SlaTone {
  return t === "ok" || t === "warn" || t === "err" || t === "info" || t === "paused" ? t : "paused";
}

export interface PortfolioData {
  greetingName: string;
  sync: { source: string; at: string | null };
  kpis: {
    active: number;
    newThisMonth: number;
    closed90: number;
    myTasks: number;
    myTasksDueSoon: number;
    overdue: number;
    overduePercent: number;
    overdueTop: { label: string; n: number } | null;
    outstanding: number;
  };
  attention: Array<{ id: string; task: string; caseRef: string; owner: string | null; state: string; slaTone: ApiSlaTone; slaText: string }>;
  distribution: Array<{ status: CaseStatusKey; label: string; n: number }>;
  slaByStage: Array<{ status: CaseStatusKey; label: string; cases: number; medianDays: number; target: string; overdue: number }>;
  regions: string[];
}

export interface CaseListItem {
  reference: string;
  owner: string;
  city: string | null;
  status: CaseStatusKey;
  statusLabel: string;
  nextAction: string;
  slaTone: ApiSlaTone;
  slaText: string;
  dueOn: string | null;
  outstanding: number | null;
  arrearsInstallments: number | null;
  manager: string;
}

export interface CaseListData {
  items: CaseListItem[];
  total: number;
  page: number;
  pageSize: number;
  counts: Record<"mine" | "action" | "overdue" | "expiring" | "all", number>;
}

export interface AvailableAction {
  key: string;
  labelAr: string;
  to: string;
  enabled: boolean;
  reasons: string[];
  requiresReason: boolean;
  requiresStepUp: boolean;
}

export interface NextActionDto {
  eyebrow: string;
  forYou: boolean;
  title: string;
  body: string | null;
  dueTone: string | null;
  dueText: string | null;
  checks: Array<{ ok: boolean; text: string }>;
  primary: { label: string; href: string | null; enabled: boolean; disabledReason: string | null; action: string | null } | null;
  secondary: { label: string; href: string | null; enabled: boolean; disabledReason: string | null; action: string | null } | null;
  assigneeName: string | null;
  assigneeRole: string | null;
}

export interface WorkspaceData {
  header: {
    reference: string;
    product: string;
    lender: string;
    opened: string | null;
    title: string;
    titleShort: string;
    status: CaseStatusKey;
    statusLabel: string;
    stage: number;
    stageNames: string[];
    slaTone: ApiSlaTone;
    slaText: string;
    slaShort: string;
    manager: string | null;
    ownerVerified: boolean;
    ownerAccessLabel: string;
    pauseReason: string | null;
    version: number;
  };
  tabs: { documentsBadge: string | null; solutionsBadge: string | null; commsBadge: string | null; showSale: boolean; showReferral: boolean; showClosure: boolean };
  nextAction: NextActionDto;
  figures: Array<{ key: string; label: string; value: number | null; unit: string; icon: string; source: string | null }>;
  solutions: Array<{
    version: number;
    kind: string;
    status: string;
    termMonths: number;
    installment: number;
    waiver: number;
    preparedBy: string | null;
    preparedAt: string;
    returnReason: string | null;
    returnedBy: string | null;
    stage: string;
    stateText: string;
    tone: string;
    approvalDue: string | null;
  }>;
  documents: {
    total: number;
    verified: number;
    expiring: Array<{ id: string; name: string; days: number; text: string }>;
    needsAttention: Array<{ id: string; name: string; status: string }>;
  };
  tasks: Array<{ id: string; title: string; assignee: string | null; due: string | null; dueText: string | null }>;
  parties: {
    primary: { id: string; name: string; initials: string; role: string; nationalIdMasked: string | null; language: string; canReveal: boolean } | null;
    providers: Array<{ provider: string; reference: string; title: string; text: string }>;
  };
  activity: Array<{ type: string; title: string; meta: string; blocked: boolean }>;
  sensitive: {
    actions: AvailableAction[];
    canRequestCancel: boolean;
    sale: { label: string; enabled: boolean; reasons: string[]; href: string } | null;
    referralNote: string | null;
    canInitiateReferral: boolean;
  };
  actions: AvailableAction[];
}

export interface SolutionDto {
  version: number;
  kind: "Reschedule" | "ReducedPayoff" | "GracePeriod" | "VoluntarySale";
  status: string;
  termMonths: number;
  firstDueDate: string;
  lastDueDate: string;
  waiverAmount: number;
  waiverPercent: number;
  downPayment: number;
  graceMonths: number;
  rescheduledAmount: number;
  installmentAmount: number;
  finalInstallmentAmount: number;
  discountedPayoffAmount: number | null;
  dsr: number | null;
  dsrLimit: number;
  netIncomeUsed: number | null;
  justification: string | null;
  breachMissedConsecutive: number;
  breachCureDays: number;
  offerValidityDays: number;
  preparedBy: string | null;
  preparedAt: string;
  lockedAt: string | null;
  returnReason: string | null;
  rowVersion: number;
  firstDueHijri: string;
}

export interface SolutionContext {
  reference: string;
  owner: string;
  outstanding: number;
  lateFeesDue: number;
  marketValue: number | null;
  netIncome: number | null;
  incomeSource: string | null;
  incomeVerifiedOn: string | null;
  dsrLimit: number;
  caseStatus: CaseStatusKey;
  maxTermMonths: number;
}

export interface SolutionRoute {
  reviewer: string | null;
  reviewerIsPreparer: boolean;
  approver: string | null;
  approverTier: string | null;
  approverLimit: string | null;
  escalated: boolean;
  noApprover: boolean;
}

export interface SolutionDetail {
  context: SolutionContext;
  solution: SolutionDto;
  previous: SolutionDto | null;
  route: SolutionRoute;
  permissions: { canEdit: boolean; canHandover: boolean; canSubmit: boolean; canReturn: boolean; isPreparer: boolean };
  kinds: Array<{ key: SolutionDto["kind"]; label: string; desc: string; enabled: boolean; reason: string | null }>;
}

export interface SolutionCalc {
  rescheduledAmount: number;
  installmentAmount: number;
  finalInstallmentAmount: number;
  lastDueDate: string;
  waiverPercent: number;
  dsr: number | null;
  dsrWithinLimit: boolean;
  installments: number;
  lastDueHijri: string;
  firstDueHijri: string;
  route: SolutionRoute;
}

export interface SubmissionData {
  reference: string;
  version: number;
  from: string;
  to: string;
  expectedStatus: string;
  approver: { name: string; tier: string; limit: string } | null;
  dueOn: string;
  dueDays: number;
  evidence: Array<{ name: string; meta: string }>;
  summary: {
    kind: string;
    rescheduledAmount: number;
    termMonths: number;
    installmentAmount: number;
    waiverAmount: number;
    waiverPercent: number;
    dsr: number | null;
    dsrLimit: number;
    complianceNotice: boolean;
  };
  blockers: string[];
  canSubmit: boolean;
}

export const SOLUTION_KIND_LABEL: Record<string, string> = {
  Reschedule: "إعادة جدولة",
  ReducedPayoff: "سداد مخفض",
  GracePeriod: "فترة سماح",
  VoluntarySale: "بيع طوعي",
};
