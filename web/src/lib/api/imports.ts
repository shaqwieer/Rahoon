/** L04 bulk import DTOs (mirrors server/src/Rahoon.Api/Modules/Imports/ImportEndpoints.cs). */

export type ImportBatchStatus = "validated" | "importing" | "completed" | "failed";
export type ImportRowStatus = "ready" | "duplicate" | "error" | "imported" | "skipped";
export type ImportDecision = "skip" | "link" | "create_with_reason";

export interface ImportBatchSummary {
  id: string;
  fileName: string;
  templateVersion: string;
  status: ImportBatchStatus;
  uploadedBy: string | null;
  uploadedAt: string;
  /** 2 = validation (decisions open), 3 = result. */
  step: number;
  counts: {
    total: number;
    ready: number;
    duplicates: number;
    errors: number;
    imported: number;
    skipped: number;
    undecidedDuplicates: number;
    importable: number;
  };
  meta: string;
  note: string;
}

export interface ImportIssue {
  field: string;
  fieldLabel: string;
  code: string;
  message: string;
  fix: string;
  severity: "error" | "duplicate" | string;
}

export interface ImportRowView {
  rowNumber: number;
  contractNumberMasked: string | null;
  status: ImportRowStatus;
  issues: ImportIssue[];
  duplicateOf: string | null;
  decision: ImportDecision | null;
  decisionReason: string | null;
  createdCaseId: string | null;
  actions: ImportDecision[];
}

export interface ImportBatchDetail {
  batch: ImportBatchSummary;
  rows: ImportRowView[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ImportBatchListItem {
  id: string;
  fileName: string;
  status: ImportBatchStatus;
  totalRows: number;
  readyCount: number;
  duplicateCount: number;
  errorCount: number;
  importedCount: number;
  createdAt: string;
}

export interface ImportCommitResult {
  alreadyCommitted: boolean;
  batch: ImportBatchSummary;
  created: string[];
  linked?: number;
}
