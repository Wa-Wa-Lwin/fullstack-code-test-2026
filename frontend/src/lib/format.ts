import type { Customer } from "./types";

/** For example "SGD 49.90". */
export function formatAmount(amount: number, currency: string): string {
  return new Intl.NumberFormat("en-SG", {
    style: "currency",
    currency,
    currencyDisplay: "code",
  }).format(amount);
}

export function customerName(customer: Customer): string {
  return `${customer.firstName} ${customer.lastName}`;
}

// 2C2P channel codes seen in the sandbox, with a name a customer recognises. Any other code
// is shown as it is.
const PAYMENT_METHODS: Record<string, string> = {
  CC: "Credit / debit card",
  VI: "Visa",
  MA: "Mastercard",
  UP: "UnionPay",
};

export function paymentMethodLabel(channelCode: string | null): string {
  if (!channelCode) {
    return "—";
  }
  return PAYMENT_METHODS[channelCode] ?? channelCode;
}
