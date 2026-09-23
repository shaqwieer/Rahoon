import "server-only";
import { cache } from "react";
import { apiGet } from "./server";
import type { WorkspaceData } from "./lender";

/** Case workspace payload, fetched once per request and shared by the case layout and its pages. */
export const getWorkspace = cache((reference: string) => apiGet<WorkspaceData>(`/cases/${encodeURIComponent(reference)}`));
