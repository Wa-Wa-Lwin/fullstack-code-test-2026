"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ApiError, getTransaction } from "@/lib/api";
import { formatAmount, paymentMethodLabel } from "@/lib/format";
import type { Transaction } from "@/lib/types";

const POLL_INTERVAL_MS = 3_000;

/**
 * Shows a payment's status as stored by the API, never what the 2C2P redirect said: the
 * customer can arrive here before or after 2C2P's backend notification. While the status is
 * Pending, it asks the API again every few seconds.
 */
export default function PaymentResult({ invoiceNo }: { invoiceNo: string }) {
  const [transaction, setTransaction] = useState<Transaction | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    async function load() {
      try {
        const latest = await getTransaction(invoiceNo);
        if (cancelled) return;

        setTransaction(latest);
        setError(null);
        if (latest.status === "Pending") {
          timer = setTimeout(load, POLL_INTERVAL_MS);
        }
      } catch (e) {
        if (cancelled) return;

        if (e instanceof ApiError && e.status === 404) {
          setError(`No payment was found with invoice number ${invoiceNo}.`);
          return; // It will not appear later, so stop asking.
        }

        // Most likely a brief network or API problem: keep trying.
        setError(e instanceof Error ? e.message : "Could not load the payment.");
        timer = setTimeout(load, POLL_INTERVAL_MS);
      }
    }

    load();

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [invoiceNo]);

  if (!transaction) {
    return error ? <ErrorMessage message={error} /> : <p className="text-sm text-gray-500">Loading payment…</p>;
  }

  return (
    <div className="space-y-6">
      {transaction.status === "Pending" && <Processing invoiceNo={transaction.invoiceNo} />}
      {transaction.status === "Succeeded" && <Succeeded transaction={transaction} />}
      {transaction.status === "Failed" && <Failed transaction={transaction} />}

      {/* A failed refresh while polling: the last known status stays on screen. */}
      {error && <p className="text-sm text-amber-700">Still trying to reach the server: {error}</p>}
    </div>
  );
}

function Processing({ invoiceNo }: { invoiceNo: string }) {
  return (
    <div className="flex items-start gap-4">
      <span
        aria-hidden
        className="mt-1 size-6 shrink-0 animate-spin rounded-full border-4 border-blue-200 border-t-blue-600"
      />
      <div>
        <h1 className="text-xl font-semibold text-gray-900">Processing payment</h1>
        <p className="mt-1 text-sm text-gray-600">
          We are waiting for 2C2P to confirm payment {invoiceNo}. This usually takes a few seconds,
          and this page updates automatically. You do not need to pay again.
        </p>
      </div>
    </div>
  );
}

function Succeeded({ transaction }: { transaction: Transaction }) {
  return (
    <>
      <h1 className="text-xl font-semibold text-green-700">Payment succeeded</h1>
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="text-gray-500">Product</dt>
        <dd className="text-gray-900">{transaction.productName}</dd>
        <dt className="text-gray-500">Amount</dt>
        <dd className="text-gray-900">{formatAmount(transaction.amount, transaction.currency)}</dd>
        <dt className="text-gray-500">Invoice number</dt>
        <dd className="font-mono text-gray-900">{transaction.invoiceNo}</dd>
        <dt className="text-gray-500">Payment method</dt>
        <dd className="text-gray-900">{paymentMethodLabel(transaction.paymentChannel)}</dd>
        <dt className="text-gray-500">Card / account</dt>
        <dd className="font-mono text-gray-900">{transaction.maskedCardNo ?? "—"}</dd>
      </dl>
      <Link href="/" className="inline-block text-sm font-medium text-blue-600 hover:underline">
        Make another payment
      </Link>
    </>
  );
}

function Failed({ transaction }: { transaction: Transaction }) {
  // Back to checkout with the same customer and product selected, so the customer can retry.
  // The checkout page creates a new Idempotency-Key, because this is a new attempt.
  const retryHref = `/?customerId=${transaction.customerId}&productId=${transaction.productId}`;

  return (
    <>
      <h1 className="text-xl font-semibold text-red-700">Payment failed</h1>
      <p className="text-sm text-gray-700">The payment did not go through.</p>
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="text-gray-500">Reason</dt>
        <dd className="text-gray-900">{transaction.respDesc || "2C2P gave no reason."}</dd>
        <dt className="text-gray-500">Product</dt>
        <dd className="text-gray-900">{transaction.productName}</dd>
        <dt className="text-gray-500">Amount</dt>
        <dd className="text-gray-900">{formatAmount(transaction.amount, transaction.currency)}</dd>
        <dt className="text-gray-500">Invoice number</dt>
        <dd className="font-mono text-gray-900">{transaction.invoiceNo}</dd>
      </dl>
      <p className="text-sm text-gray-600">
        To try again, go back to checkout. The same customer and product will already be selected,
        and a new payment is started when you click Checkout.
      </p>
      <Link
        href={retryHref}
        className="inline-block rounded-lg bg-blue-600 px-4 py-2.5 font-semibold text-white hover:bg-blue-700"
      >
        Go back and try again
      </Link>
    </>
  );
}

function ErrorMessage({ message }: { message: string }) {
  return (
    <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
      {message}
    </p>
  );
}
