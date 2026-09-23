import { useMemo, useRef } from "react";
import { ApiError, parseResponse } from "./types";

export { ApiError, isApiError, safeNext } from "./types";

export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export interface SendOptions {
  /**
   * Replay-protection key for mutations. Pass the same key when retrying the *same logical submit*
   * (see useIdempotencyKey) so the API returns the stored response instead of acting twice.
   * Omitted → a fresh UUID per call.
   */
  idempotencyKey?: string;
  signal?: AbortSignal;
  headers?: Record<string, string>;
}

/** Reads a cookie value in the browser (undefined on the server). */
export function readCookie(name: string): string | undefined {
  if (typeof document === "undefined") return undefined;
  const prefix = `${name}=`;
  for (const part of document.cookie.split(";")) {
    const c = part.trim();
    if (c.startsWith(prefix)) return decodeURIComponent(c.slice(prefix.length));
  }
  return undefined;
}

export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) return crypto.randomUUID();
  return `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;
}

function buildHeaders(method: HttpMethod, opts: SendOptions, json: boolean): Record<string, string> {
  const h: Record<string, string> = { accept: "application/json", ...opts.headers };
  if (json) h["content-type"] = "application/json";
  // CSRF double-submit: read on every call — the token rotates after MFA, context switch and owner sign-in.
  const csrf = readCookie("rahoon_csrf");
  if (csrf) h["X-CSRF-Token"] = csrf;
  if (method !== "GET") h["Idempotency-Key"] = opts.idempotencyKey ?? newIdempotencyKey();
  return h;
}

async function send<T>(method: HttpMethod, path: string, init: { body?: BodyInit; json: boolean }, opts: SendOptions): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`/api${path}`, {
      method,
      credentials: "same-origin",
      cache: "no-store",
      headers: buildHeaders(method, opts, init.json),
      body: init.body,
      signal: opts.signal,
    });
  } catch (e) {
    if (e instanceof DOMException && e.name === "AbortError") throw e;
    const offline = typeof navigator !== "undefined" && navigator.onLine === false;
    throw new ApiError(0, { code: offline ? "offline" : "network", title: "", status: 0 });
  }
  return parseResponse<T>(res);
}

/**
 * Browser → Next origin → (rewrite) → API. JSON in/out; throws ApiError with the RFC 7807 fields
 * (`title`, `code`, `errors`, `reasons`, `remainingAttempts`, `lockedUntil`). Never redirects on its own:
 * a 401 on a login form is a normal validation outcome, so callers branch on `error.code`.
 */
export function apiSend<T = unknown>(method: HttpMethod, path: string, body?: unknown, opts: SendOptions = {}): Promise<T> {
  const hasBody = body !== undefined && method !== "GET";
  return send<T>(method, path, { body: hasBody ? JSON.stringify(body) : undefined, json: hasBody }, opts);
}

/** Multipart upload (documents). The browser sets the multipart boundary. */
export function apiUpload<T = unknown>(path: string, formData: FormData, opts: SendOptions = {}): Promise<T> {
  return send<T>("POST", path, { body: formData, json: false }, opts);
}

/**
 * One idempotency key per logical submit: `get()` returns the same key until `reset()` (call reset after
 * success or when the form content changes), so a retry after a network error does not double-submit.
 */
export function useIdempotencyKey() {
  const ref = useRef<string | null>(null);
  return useMemo(
    () => ({
      get: () => (ref.current ??= newIdempotencyKey()),
      reset: () => {
        ref.current = null;
      },
    }),
    [],
  );
}
