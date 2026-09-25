/** L24 case audit DTOs (mirrors server/src/Rahoon.Api/Modules/Audit/CaseAuditEndpoints.cs). */

export interface CaseAuditRow {
  seq: number;
  type: string;
  category: string | null;
  icon: string;
  tone: "ok" | "info" | "warn" | "err" | "neutral";
  title: string;
  occurredAt: string;
  /** Riyadh time «yyyy-MM-dd HH:mm». */
  time: string;
  hijri: string;
  actor: { type: string; id: string | null; label: string | null; role: string | null; display: string | null };
  fromState: string | null;
  toState: string | null;
  reason: string | null;
  detail: string | null;
  blocked: boolean;
  evidence: string[];
  ipMasked: string | null;
  hash: string;
}

export interface CaseAuditData {
  items: CaseAuditRow[];
  page: number;
  pageSize: number;
  total: number;
  filters: {
    categories: Array<{ key: string; label: string }>;
    actors: Array<{ key: string; label: string; type: string }>;
    applied: { type: string | null; actor: string | null; blocked: boolean | null; from: string | null; to: string | null };
  };
  canExport: boolean;
  appendOnlyNote: string;
}

export interface AuditVerifyData {
  ok: boolean;
  firstBrokenSeq: number | null;
  chainLastSeq: number | null;
  caseLastSeq: number | null;
  caseLastAt: string | null;
  checkedAt: string;
  text: string;
  tone: "ok" | "err";
}
