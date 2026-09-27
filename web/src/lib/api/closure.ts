/** Shapes of /api/cases/{ref}/closure (server/src/Rahoon.Api/Modules/Closure/ClosureEndpoints.cs, L26 / F01–F04). */

export interface ChainStep {
  role: string;
  user: string | null;
  status: "done" | "in_progress" | "pending";
  at: string | null;
  reason?: string | null;
}

export interface ReconciliationView {
  id: string;
  basis: string;
  basisLabel: string;
  status: "Draft" | "Submitted" | "Reviewed" | "Approved" | "Returned";
  expectedAmount: number;
  expectedSource: string | null;
  receivedAmount: number;
  waivedAmount: number;
  difference: number;
  differenceExplanation: string | null;
  differenceTone: "ok" | "warn" | "err";
  differenceLabel: string;
  lines: Array<{ id: string; kind: string; label: string; amount: number; reference: string | null; matchStatus: string | null; sourceType: string | null; valueDate: string | null; countsIn: string }>;
  formula: string;
  chain: ChainStep[];
  returnReason: string | null;
  note: string | null;
}

export interface ClosureDocumentRow {
  id: string;
  type: string;
  title: string;
  status: "ready" | "pending";
  preparedByDept: string | null;
  preparedOn: string | null;
  documentVersionId: string | null;
  externalReference: string | null;
  blocksClosure: boolean;
  shareWithOwner: boolean;
  visibleToOwner: boolean;
  meta: string;
}

export interface TraceRow {
  key: string;
  label: string;
  amount: number | null;
  sourceType: string;
  sourceTypeLabel: string;
  source: string;
  sourceRef: string | null;
  sourceDate: string | null;
  hasSource: boolean;
}

export interface ClosureAction {
  key: "request_closure" | "decide_closure";
  label: string;
  enabled: boolean;
  reasons: string[];
  requiresStepUp?: boolean;
}

export interface ClosureOverview {
  caseRef: string;
  caseStatus: string;
  closedAt: string | null;
  reconciliation: ReconciliationView | null;
  documents: ClosureDocumentRow[];
  trace: TraceRow[];
  missingSources: number;
  request: {
    id: string;
    status: "Pending" | "Approved" | "Rejected";
    note: string;
    requestedAt: string;
    requestedBy: string | null;
    decidedAt: string | null;
    decidedBy: string | null;
    decisionReason: string | null;
    traceSha256: string;
  } | null;
  consequences: string[];
  blockers: string[];
  actions: ClosureAction[];
}
