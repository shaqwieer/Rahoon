import { newIdempotencyKey, readCookie } from "@/lib/api/client";
import { ApiError, parseResponse } from "@/lib/api/types";

/**
 * Multipart upload with progress (XHR, which reports upload progress; fetch doesn't). Same headers as lib/api/client: CSRF
 * double-submit and an Idempotency-Key, so a retry of the same file after a dropped connection never stores it twice.
 */
export function uploadWithProgress<T>(path: string, form: FormData, opts: { onProgress?: (fraction: number) => void; signal?: AbortSignal; idempotencyKey?: string } = {}): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/api${path}`);
    xhr.withCredentials = true;
    xhr.setRequestHeader("accept", "application/json");
    const csrf = readCookie("rahoon_csrf");
    if (csrf) xhr.setRequestHeader("X-CSRF-Token", csrf);
    xhr.setRequestHeader("Idempotency-Key", opts.idempotencyKey ?? newIdempotencyKey());
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) opts.onProgress?.(e.loaded / e.total);
    };
    xhr.onload = () => {
      const res = new Response(xhr.responseText || null, { status: xhr.status, headers: { "content-type": xhr.getResponseHeader("content-type") ?? "application/json" } });
      parseResponse<T>(res).then(resolve, reject);
    };
    xhr.onerror = () => reject(new ApiError(0, { code: navigator.onLine === false ? "offline" : "network", title: "", status: 0 }));
    xhr.onabort = () => reject(new DOMException("Aborted", "AbortError"));
    opts.signal?.addEventListener("abort", () => xhr.abort());
    xhr.send(form);
  });
}
