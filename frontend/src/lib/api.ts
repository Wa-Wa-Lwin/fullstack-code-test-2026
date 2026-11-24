import type {
  CheckoutRequest,
  CheckoutResponse,
  Customer,
  CustomerCard,
  ProblemDetails,
  Product,
  Transaction,
} from "./types";

// Every path is relative: the browser calls this Next.js app, which forwards /api/* to the
// .NET API (see next.config.ts).

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function getCustomers(): Promise<Customer[]> {
  return getJson("/api/customers");
}

export function getProducts(): Promise<Product[]> {
  return getJson("/api/products");
}

export function getCustomerCards(customerId: number): Promise<CustomerCard[]> {
  return getJson(`/api/customers/${customerId}/cards`);
}

/** Always read fresh: the result page must never show a cached status. */
export function getTransaction(invoiceNo: string): Promise<Transaction> {
  return getJson(`/api/transactions/${encodeURIComponent(invoiceNo)}`);
}

// How often to retry while another request with the same Idempotency-Key is still running.
const MAX_CONFLICT_RETRIES = 5;
const DEFAULT_RETRY_AFTER_MS = 2_000;
const MAX_RETRY_AFTER_MS = 10_000;

/**
 * Starts a payment. Every attempt for the same purchase must send the same idempotencyKey,
 * so the API returns the first result instead of charging again.
 *
 * A 409 means a request with this key is still being processed. The same key is sent again
 * after the Retry-After delay, and the API then answers with that request's result.
 */
export async function checkout(
  request: CheckoutRequest,
  idempotencyKey: string,
): Promise<CheckoutResponse> {
  for (let attempt = 0; ; attempt++) {
    const response = await fetch("/api/checkout", {
      method: "POST",
      cache: "no-store",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "Idempotency-Key": idempotencyKey,
      },
      body: JSON.stringify(request),
    });

    if (response.status === 409 && attempt < MAX_CONFLICT_RETRIES) {
      await delay(retryAfterMs(response));
      continue;
    }

    if (!response.ok) {
      throw await toApiError(response);
    }

    return (await response.json()) as CheckoutResponse;
  }
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, {
    cache: "no-store",
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  let message = `The request failed (HTTP ${response.status}).`;

  try {
    const problem = (await response.json()) as ProblemDetails;
    const fieldErrors = Object.values(problem.errors ?? {}).flat();
    message = fieldErrors.length > 0 ? fieldErrors.join(" ") : (problem.detail ?? problem.title ?? message);
  } catch {
    // Not a problem-details body (for example, the API is down): keep the generic message.
  }

  return new ApiError(message, response.status);
}

function retryAfterMs(response: Response): number {
  const seconds = Number(response.headers.get("Retry-After"));
  if (!Number.isFinite(seconds) || seconds <= 0) {
    return DEFAULT_RETRY_AFTER_MS;
  }
  return Math.min(seconds * 1_000, MAX_RETRY_AFTER_MS);
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
