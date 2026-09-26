/** Shapes shared by server and client API helpers. Mirrors server/src/Rahoon.Api (Identity/AuthEndpoints.cs). */

export type OrgKind = "lender" | "serviceprovider" | "judicialagent" | "platform" | "operator";
export type SessionScope = "none" | "organization" | "owner" | "individual";
export type SessionStage = "mfapending" | "active";

export interface MeMembership {
  id: string;
  organization: string;
  initials: string;
  kind: OrgKind;
  role: string | null;
  current: boolean;
}

export interface MeAuthenticated {
  authenticated: true;
  stage: SessionStage;
  scope: SessionScope;
  user: { id: string; name: string; email: string; locale: string | null; numerals: string | null; initials: string };
  organization: { id: string; name: string; kind: OrgKind; initials: string } | null;
  roles: string[];
  roleName: string | null;
  permissions: string[];
  owner: { caseRef: string; firstName: string; lenderName: string } | null;
  /** Self-registered individual (ADR 0001); identity is self-declared until verified by the Rahoon team. */
  individual?: { idMasked: string; phoneMasked: string; identityAssurance: string } | null;
  memberships: MeMembership[];
  stepUpActive: boolean;
  unreadNotifications: number;
  /** Server-computed landing route for the current context (e.g. /portfolio, /provider, /owner, /select-context). */
  home: string;
}

export type Me = MeAuthenticated | { authenticated: false };

/** RFC 7807 problem body as produced by ProblemExceptionHandler + auth endpoints. */
export interface ProblemBody {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
  reasons?: string[];
  remainingAttempts?: number;
  lockedUntil?: string;
  minutes?: number;
  [key: string]: unknown;
}

/** Thrown by apiGet / apiSend for any non-2xx response (or `code: "network"` when the request never completed). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly title: string;
  readonly errors?: Record<string, string[]>;
  readonly reasons?: string[];
  readonly remainingAttempts?: number;
  readonly lockedUntil?: string;
  readonly minutes?: number;
  readonly problem: ProblemBody;

  constructor(status: number, problem: ProblemBody) {
    super(problem.title ?? `HTTP ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
    this.code = problem.code ?? (status === 0 ? "network" : `http_${status}`);
    this.title = problem.title ?? "";
    this.errors = problem.errors;
    this.reasons = problem.reasons;
    this.remainingAttempts = typeof problem.remainingAttempts === "number" ? problem.remainingAttempts : undefined;
    this.lockedUntil = typeof problem.lockedUntil === "string" ? problem.lockedUntil : undefined;
    this.minutes = typeof problem.minutes === "number" ? problem.minutes : undefined;
  }

  /** First message for a field from `errors` (keys are camelCase as sent by the API). */
  fieldError(field: string): string | undefined {
    return this.errors?.[field]?.[0];
  }
}

export function isApiError(e: unknown): e is ApiError {
  return e instanceof ApiError;
}

async function readBody(res: Response): Promise<unknown> {
  if (res.status === 204) return undefined;
  const type = res.headers.get("content-type") ?? "";
  if (type.includes("json")) {
    try {
      return await res.json();
    } catch {
      return undefined;
    }
  }
  const text = await res.text();
  return text.length ? text : undefined;
}

/** Parses a fetch Response: returns the JSON body on success, throws ApiError otherwise. */
export async function parseResponse<T>(res: Response): Promise<T> {
  const body = await readBody(res);
  if (res.ok) return body as T;
  // Non-JSON bodies come from the proxy/rewrite layer (API down, gateway error): never show them raw.
  const problem: ProblemBody =
    body && typeof body === "object" ? (body as ProblemBody) : { status: res.status, code: res.status >= 500 ? "unavailable" : undefined };
  throw new ApiError(res.status, problem);
}

/** Only same-origin relative paths are allowed as post-login destinations (`/x`, never `//host`). */
export function safeNext(next: string | null | undefined, fallback = "/"): string {
  if (!next || typeof next !== "string") return fallback;
  if (!next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return fallback;
  return next;
}
