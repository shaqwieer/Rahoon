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
    /** Pending «إلغاء الحالة» request (CancellationEndpoints.PendingSummaryAsync), null when none. */
    pendingCancellation: PendingCancellation | null;
    sale: { label: string; enabled: boolean; reasons: string[]; href: string } | null;
    referralNote: string | null;
    canInitiateReferral: boolean;
  };
  actions: AvailableAction[];
}

export interface PendingCancellation {
  id: string;
  reason: string | null;
  requestedBy: string | null;
  requestedAt: string | null;
  approver: string | null;
  dueOn: string | null;
  assignedToMe: boolean;
  canDecide: boolean;
  /** Separation-of-duties note shown to the requester, else null. */
  note: string | null;
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

/* ───────── B4 · L18–L21 (Modules/Agreements/AgreementEndpoints.cs) ─────────
 * Enum spellings: values the API passes through `.ToString()` are PascalCase (thread kind, offer/agreement/
 * installment/breach status); case statuses are snake keys (CaseStatusInfo.Key).
 */

export type NegotiationKind = "Offer" | "OwnerCounter" | "OwnerMessage" | "LenderClarification" | "InternalNote" | "LenderDecline" | "OwnerDecline" | "OwnerAccept";

export interface NegotiationEntryDto {
  id: string;
  kind: NegotiationKind | string;
  authorType: "lender" | "owner" | "system" | string;
  authorLabel: string;
  at: string;
  body: string;
  internalOnly: boolean;
  /** Server-composed Arabic line with embedded LTR tokens («صالح حتى 2026-10-03», «طلب: …»). */
  terms: string | null;
}

export interface NegotiationData {
  status: CaseStatusKey;
  thread: NegotiationEntryDto[];
  comparison: {
    version: string;
    rows: Array<{ item: string; offered: string; requested: string; changed: boolean }>;
    counterEntryId: string;
  } | null;
  offer: { id: string; status: string; validUntil: string; version: number } | null;
  extensions: Array<{ id: string; title: string; subjectVersionNo: number; mine: boolean }>;
  canAct: boolean;
  note: string;
}

export type AgreementStatus = "PendingActivation" | "Active" | "BreachReview" | "Completed" | "Terminated";

export interface AgreementDto {
  number: string;
  versionLabel: string;
  status: AgreementStatus | string;
  terms: Array<{ k: string; v: string }>;
  consent: { title: string; meta: string; acks: string; textHash: string | null } | null;
  steps: Array<{ key: "legal_review" | "schedule" | "core_system" | string; label: string; done: boolean }>;
  versionMatchesOffer: boolean;
  /** `state` is a raw integration-state string; normalise with toIntegrationState before rendering. */
  signing: { state: string; note: string };
  permissions: { canLegalReview: boolean; canCreateSchedule: boolean; canActivate: boolean };
}

/** `GET /cases/{ref}/agreement` → `{ agreement: null }` until the owner accepts an offer. */
export interface AgreementData {
  agreement: AgreementDto | null;
}

export type InstallmentStatus = "Upcoming" | "Due" | "RecordedPendingMatch" | "Matched" | "Partial" | "Overdue" | "Waived";

export interface InstallmentDto {
  id: string;
  no: number;
  dueDate: string;
  amount: number;
  paid: number | null;
  reference: string | null;
  status: InstallmentStatus | string;
  statusLabel: string;
  paymentId: string | null;
  recordedBy: string | null;
  canMatch: boolean;
  matchBlockedReason: string | null;
}

export interface PaymentsActive {
  agreement: { number: string; installmentCount: number; status: AgreementStatus | string };
  kpis: { rescheduled: number; paid: number; paidCount: number; remaining: number; pendingMatch: number };
  installments: InstallmentDto[];
  nextDue: { no: number; dueDate: string; amount: number } | null;
  breach: { id: string; cureDeadline: string; missed: number[] } | null;
  canRecord: boolean;
}

/** `GET /cases/{ref}/payments` → only `{ agreement: null }` before activation. */
export type PaymentsData = PaymentsActive | { agreement: null };

export function hasSchedule(d: PaymentsData): d is PaymentsActive {
  return d.agreement !== null;
}

/** Recording is accepted only on an active agreement or one under breach review (else 409 no_active_agreement). */
export function canRecordOn(status: string): boolean {
  return status === "Active" || status === "BreachReview";
}

/** Installments a payment can still be recorded against (the API refuses matched / pending-match ones). */
export function recordableInstallments(installments: InstallmentDto[]): InstallmentDto[] {
  return installments.filter((i) => i.status === "Upcoming" || i.status === "Due" || i.status === "Overdue" || i.status === "Partial");
}

export type BreachStatus = "Open" | "Cured" | "Restructuring" | "OtherOptions" | "Closed";

export interface BreachData {
  reviews: Array<{ id: string; status: BreachStatus | string; triggeredAt: string; cureDeadline: string; missed: number[]; outcomeNote: string | null }>;
  timeline: Array<{ icon: string; tone: "err" | "warn" | "muted" | string; date: string; text: string }>;
  paths: Array<{ key: "cure" | "restructuring" | "other_options" | string; title: string; description: string; owner: string }>;
  contactHours: string | null;
  note: string;
}

const INTEGRATION_STATES = ["enabled", "simulated", "pending", "unavailable", "failed"] as const;
export type IntegrationStateKey = (typeof INTEGRATION_STATES)[number];

/** Integration states arrive as free strings (admin.integration_settings); anything unknown reads as «unavailable». */
export function toIntegrationState(s: string | null | undefined): IntegrationStateKey {
  const v = (s ?? "").toLowerCase();
  return (INTEGRATION_STATES as readonly string[]).includes(v) ? (v as IntegrationStateKey) : "unavailable";
}

/* ───────── B2 · S07–S10 (WorkspaceEndpoints.cs, AuthEndpoints.cs, CaseListEndpoints.cs) ───────── */

export interface NotificationItem {
  id: string;
  category: string;
  title: string;
  body: string | null;
  link: string | null;
  tone: string | null;
  createdAt: string;
  read: boolean;
}

export interface NotificationsData {
  items: NotificationItem[];
  unread: number;
}

export interface TaskItem {
  id: string;
  title: string;
  caseRef: string | null;
  owner: string | null;
  dueOn: string | null;
  kind: string;
  link: string | null;
  status: "Open" | "Done" | string;
  completedAt: string | null;
  assignee: string | null;
  slaTone: ApiSlaTone;
  slaText: string;
}

export interface TasksData {
  items: TaskItem[];
  counts: { mine: number; overdue: number };
}

/** Task kinds that close themselves when the linked action happens (CompleteTask → 409 task_auto). */
export const AUTO_TASK_KINDS: readonly string[] = ["approval", "solution_review", "complaint", "agreement"];

export interface OrgMember {
  id: string;
  name: string;
  title: string | null;
  team: string | null;
  roles: string[];
}

export interface SearchData {
  cases: Array<{ reference: string; owner: string | null; city: string | null; status: CaseStatusKey; statusLabel: string }>;
  tasks: Array<{ id: string; title: string; link: string | null }>;
}

export interface SessionItem {
  id: string;
  userAgent: string | null;
  city: string | null;
  createdAt: string;
  lastSeenAt: string;
  current: boolean;
}
