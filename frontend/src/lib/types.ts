// Shapes of the .NET API's JSON (ASP.NET Core writes property names in camelCase).

export type PaymentType = "OneTime" | "Recurring";

export type TransactionStatus = "Pending" | "Succeeded" | "Failed";

export interface Customer {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  currency: string;
  paymentType: PaymentType;
}

/** A saved card. The API never sends the 2C2P customer token to the browser. */
export interface CustomerCard {
  id: number;
  customerId: number;
  maskedCardNo: string;
  createdAt: string;
}

export interface CheckoutRequest {
  customerId: number;
  productId: number;
  /** Charge this saved card directly. Leave it out to pay on the 2C2P payment page. */
  customerCardId?: number;
}

export interface CheckoutResponse {
  transactionId: number;
  invoiceNo: string;
  amount: number;
  currency: string;
  status: TransactionStatus;
  /** The 2C2P payment page or a 3-D Secure page, or null when there is nowhere to go. */
  redirectUrl: string | null;
}

export interface Transaction {
  id: number;
  customerId: number;
  productId: number;
  productName: string;
  invoiceNo: string;
  amount: number;
  currency: string;
  status: TransactionStatus;
  paymentChannel: string | null;
  maskedCardNo: string | null;
  respDesc: string | null;
  createdAt: string;
  paidAt: string | null;
}

/** An RFC 7807 error response. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
