/** L23 complaints DTOs (mirrors server/src/Rahoon.Api/Modules/Complaints/ComplaintEndpoints.cs). */

/** `ComplaintStatus.ToString()`: Received | InReview | AwaitingOwner | Resolved | Escalated | Closed. */
export type ComplaintStatusKey = "Received" | "InReview" | "AwaitingOwner" | "Resolved" | "Escalated" | "Closed";

/** `GET /api/complaints?status=open|mine|closed` — `subject` is null for roles without complaint.handle (tag only). */
export interface ComplaintListItem {
  reference: string;
  caseRef: string;
  /** «شكوى» | «اعتراض» */
  type: string;
  subject: string | null;
  status: ComplaintStatusKey;
  statusLabel: string;
  dueOn: string;
  reviewer: string | null;
  slaText: string;
  slaTone: "ok" | "warn" | "err";
}

/** `GET /api/complaints/{ref}` — subject/body/response are null and findings empty for non-reviewers. */
export interface ComplaintDetail {
  reference: string;
  caseRef: string;
  via: string;
  status: ComplaintStatusKey;
  statusLabel: string;
  subject: string | null;
  body: string | null;
  submittedBy: string;
  submittedAt: string;
  dueOn: string;
  dueText: string;
  reviewer: string | null;
  reviewerIsMe: boolean;
  /** "severity|text" where severity ∈ ok | issue | info. */
  findings: string[];
  /** Accepted | PartiallyAccepted | Rejected */
  decision: string | null;
  /** Sent response, or the saved draft while open. */
  response: string | null;
  respondedAt: string | null;
  impact: string[];
  path: Array<{ label: string; done: boolean; current: boolean; date: string | null }>;
  otherOpen: Array<{ reference: string; type: string; dueOn: string }>;
  canDecide: boolean;
  version: number;
}

export const COMPLAINT_STATUS_TONE: Record<ComplaintStatusKey, "info" | "warn" | "ok" | "err" | "neutral"> = {
  Received: "info",
  InReview: "info",
  AwaitingOwner: "warn",
  Resolved: "ok",
  Closed: "neutral",
  Escalated: "err",
};

export const COMPLAINT_STATUS_ICON: Record<ComplaintStatusKey, string> = {
  Received: "inbox",
  InReview: "manage_search",
  AwaitingOwner: "hourglass_top",
  Resolved: "task_alt",
  Closed: "task_alt",
  Escalated: "north",
};

/** Decision keys sent to `POST /complaints/{ref}/decision` (design order: partial first). */
export const COMPLAINT_DECISIONS = [
  { value: "partially_accepted", api: "PartiallyAccepted", label: "مقبولة جزئياً" },
  { value: "accepted", api: "Accepted", label: "مقبولة" },
  { value: "rejected", api: "Rejected", label: "غير مقبولة" },
] as const;

export function parseFinding(raw: string): { severity: "ok" | "issue" | "info"; text: string } {
  const i = raw.indexOf("|");
  const sev = i > 0 ? raw.slice(0, i) : "info";
  return { severity: sev === "ok" || sev === "issue" ? sev : "info", text: i > 0 ? raw.slice(i + 1) : raw };
}
