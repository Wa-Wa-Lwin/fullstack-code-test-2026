"use client";

import { useRouter } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { checkout, getCustomerCards, getCustomers, getProducts } from "@/lib/api";
import { customerName, formatAmount } from "@/lib/format";
import type { Customer, CustomerCard, Product } from "@/lib/types";

interface Props {
  /** Preselected by the result page's Go back button, so a failed payment can be retried. */
  initialCustomerId: number | null;
  initialProductId: number | null;
}

export default function CheckoutForm({ initialCustomerId, initialProductId }: Props) {
  const router = useRouter();

  const [customers, setCustomers] = useState<Customer[] | null>(null);
  const [products, setProducts] = useState<Product[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [customerId, setCustomerId] = useState(initialCustomerId);
  const [productId, setProductId] = useState(initialProductId);
  // null means "Pay with a new payment method".
  const [cardId, setCardId] = useState<number | null>(null);

  // The cards and the customer they were loaded for, so cards from a previous customer are
  // never shown while the next customer's cards load.
  const [loadedCards, setLoadedCards] = useState<{ customerId: number; cards: CustomerCard[] } | null>(null);
  const [cardsError, setCardsError] = useState<string | null>(null);

  const [submitting, setSubmitting] = useState(false);
  const [checkoutError, setCheckoutError] = useState<string | null>(null);

  // One key per purchase. It is created when the page loads and again whenever the customer,
  // product or card changes, because that is a different purchase. Double clicks and retries
  // of the same purchase reuse it, so the API never charges twice.
  const idempotencyKey = useRef<string | null>(null);
  useEffect(() => {
    idempotencyKey.current = crypto.randomUUID();
  }, [customerId, productId, cardId]);

  useEffect(() => {
    Promise.all([getCustomers(), getProducts()])
      .then(([customerList, productList]) => {
        setCustomers(customerList);
        setProducts(productList);
      })
      .catch((error: unknown) => setLoadError(messageOf(error)));
  }, []);

  useEffect(() => {
    if (customerId === null) {
      return;
    }

    let cancelled = false;
    getCustomerCards(customerId)
      .then((cards) => {
        if (!cancelled) {
          setLoadedCards({ customerId, cards });
          setCardsError(null);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setCardsError(messageOf(error));
        }
      });

    return () => {
      cancelled = true;
    };
  }, [customerId]);

  // With Cache Components, Next.js does not unmount this page when the customer moves to
  // another page in the app: it hides it (React <Activity>) and shows it again as it was.
  // A finished checkout must not come back with the button stuck on "Processing…" and the
  // old purchase still selected, so after a checkout has gone through, the form resets when
  // the page is hidden. Only then: a half-filled form is kept if the customer just looks
  // elsewhere. (Effects run again when the page is shown, so the Idempotency-Key effect
  // above creates a new key for the next purchase.)
  const resetWhenHidden = useRef(false);
  useLayoutEffect(() => {
    return () => {
      if (resetWhenHidden.current) {
        resetWhenHidden.current = false;
        setCustomerId(initialCustomerId);
        setProductId(initialProductId);
        setCardId(null);
        setSubmitting(false);
        setCheckoutError(null);
      }
    };
  }, [initialCustomerId, initialProductId]);

  // When the customer comes back from 2C2P with the browser's Back button, the page may be
  // restored from the back/forward cache with the button still disabled.
  useEffect(() => {
    function onPageShow(event: PageTransitionEvent) {
      if (event.persisted) {
        setSubmitting(false);
      }
    }
    window.addEventListener("pageshow", onPageShow);
    return () => window.removeEventListener("pageshow", onPageShow);
  }, []);

  const customer = customers?.find((c) => c.id === customerId) ?? null;
  const product = products?.find((p) => p.id === productId) ?? null;
  const cards = customer && loadedCards?.customerId === customer.id ? loadedCards.cards : null;
  const card = cards?.find((c) => c.id === cardId) ?? null;

  function selectCustomer(value: string) {
    setCustomerId(value === "" ? null : Number(value));
    setCardId(null); // A saved card belongs to one customer.
    setCheckoutError(null);
  }

  function selectProduct(value: string) {
    setProductId(value === "" ? null : Number(value));
    setCheckoutError(null);
  }

  async function handleCheckout() {
    const key = idempotencyKey.current;
    if (!customer || !product || !key || submitting) {
      return;
    }

    setSubmitting(true);
    setCheckoutError(null);

    try {
      const result = await checkout(
        { customerId: customer.id, productId: product.id, customerCardId: card?.id },
        key,
      );

      // The button stays disabled from here on: the page is about to change.
      resetWhenHidden.current = true;
      if (result.redirectUrl) {
        // The 2C2P payment page or 3-D Secure. 2C2P later sends the customer to the backend's
        // return endpoint, which redirects to the result page.
        window.location.assign(result.redirectUrl);
      } else {
        // A saved-card payment that needed no 3-D Secure, or a checkout that already failed.
        router.push(`/payment/result?invoiceNo=${encodeURIComponent(result.invoiceNo)}`);
      }
    } catch (error) {
      // The same key is kept, so trying again cannot create a second payment.
      setCheckoutError(messageOf(error));
      setSubmitting(false);
    }
  }

  if (loadError) {
    return <Alert>Could not load customers and products: {loadError}</Alert>;
  }

  if (!customers || !products) {
    return <p className="text-sm text-gray-500">Loading…</p>;
  }

  return (
    <div className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block text-sm font-medium text-gray-700">
          Customer
          <select
            className={selectClasses}
            value={customer ? String(customer.id) : ""}
            onChange={(e) => selectCustomer(e.target.value)}
            disabled={submitting}
          >
            <option value="">Select a customer</option>
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {customerName(c)}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm font-medium text-gray-700">
          Product
          <select
            className={selectClasses}
            value={product ? String(product.id) : ""}
            onChange={(e) => selectProduct(e.target.value)}
            disabled={submitting}
          >
            <option value="">Select a product</option>
            {products.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      {customer && (
        <fieldset className="space-y-2" disabled={submitting}>
          <legend className="text-sm font-medium text-gray-700">Payment method</legend>
          {cardsError && <Alert>Could not load saved cards: {cardsError}</Alert>}
          {!cards && !cardsError && <p className="text-sm text-gray-500">Loading saved cards…</p>}
          {cards?.map((c) => (
            <RadioOption
              key={c.id}
              checked={cardId === c.id}
              onChange={() => setCardId(c.id)}
              label={`Saved card ${c.maskedCardNo}`}
            />
          ))}
          <RadioOption
            checked={cardId === null}
            onChange={() => setCardId(null)}
            label="Pay with a new payment method"
          />
        </fieldset>
      )}

      {customer && product && (
        <section className="rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h2 className="mb-3 text-sm font-semibold text-gray-900">Payment summary</h2>
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
            <dt className="text-gray-500">Customer</dt>
            <dd className="text-gray-900">{customerName(customer)}</dd>
            <dt className="text-gray-500">Product</dt>
            <dd className="text-gray-900">{product.name}</dd>
            <dt className="text-gray-500">Payment type</dt>
            <dd className="text-gray-900">{product.paymentType === "Recurring" ? "Recurring" : "One-time"}</dd>
            <dt className="text-gray-500">Price</dt>
            <dd className="text-gray-900">{formatAmount(product.price, product.currency)}</dd>
            <dt className="text-gray-500">Currency</dt>
            <dd className="text-gray-900">{product.currency}</dd>
          </dl>
          <p className="mt-3 border-t border-gray-200 pt-3 text-sm text-gray-600">
            {nextStep(product, card)}
          </p>
        </section>
      )}

      {checkoutError && <Alert>{checkoutError}</Alert>}

      <button
        type="button"
        onClick={handleCheckout}
        disabled={!customer || !product || submitting}
        className="w-full rounded-lg bg-blue-600 px-4 py-2.5 font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
      >
        {submitting ? "Processing…" : "Checkout"}
      </button>
    </div>
  );
}

const selectClasses =
  "mt-1 block w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none disabled:bg-gray-100";

function RadioOption({ checked, onChange, label }: { checked: boolean; onChange: () => void; label: string }) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-lg border border-gray-200 px-3 py-2 text-sm text-gray-900 has-[:checked]:border-blue-500 has-[:checked]:bg-blue-50">
      <input type="radio" name="payment-method" checked={checked} onChange={onChange} />
      {label}
    </label>
  );
}

function Alert({ children }: { children: React.ReactNode }) {
  return (
    <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
      {children}
    </p>
  );
}

/** What happens after Checkout, so the customer knows whether a 2C2P page comes next. */
function nextStep(product: Product, card: CustomerCard | null): string {
  if (card) {
    return `Your saved card ${card.maskedCardNo} is charged directly. You will not see the 2C2P payment page, unless your bank asks for a one-time password.`;
  }
  if (product.paymentType === "Recurring") {
    return "You will pay on the 2C2P payment page. Recurring products accept cards only: tick the save-card option there to save your card for your next payment.";
  }
  return "You will pay on the 2C2P payment page, where you can choose any available payment method.";
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong.";
}
