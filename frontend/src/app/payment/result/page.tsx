import Link from "next/link";
import { Suspense } from "react";
import PaymentResult from "@/components/PaymentResult";

// Two ways in, both with ?invoiceNo=:
// - the backend's 2C2P return endpoint redirects here after the payment page or 3-D Secure
//   (it first runs a Payment Inquiry if the payment is still Pending);
// - the checkout page comes here directly when a saved-card payment needs no redirect.
export default function PaymentResultPage({ searchParams }: PageProps<"/payment/result">) {
  return (
    <>
      {/* A link, not the browser's history: after a payment, the previous entries are 2C2P's
          page and the backend's return redirect, so "back" would lead into the payment flow. */}
      <Link
        href="/"
        className="mb-6 inline-flex items-center gap-1 text-sm font-medium text-blue-600 hover:underline"
      >
        <span aria-hidden>←</span> Back to checkout
      </Link>

      {/* The query string is only known at request time, so it is read inside Suspense
          (required with Cache Components). */}
      <Suspense fallback={<p className="text-sm text-gray-500">Loading payment…</p>}>
        {searchParams.then(({ invoiceNo }) => {
          const value = Array.isArray(invoiceNo) ? invoiceNo[0] : invoiceNo;
          return value ? <PaymentResult invoiceNo={value} /> : <MissingInvoice />;
        })}
      </Suspense>
    </>
  );
}

function MissingInvoice() {
  return (
    <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
      No invoice number was given, so there is no payment to show.
    </p>
  );
}
