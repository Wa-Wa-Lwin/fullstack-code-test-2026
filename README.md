# fullstack-code-test-2026

## Running locally

You need the .NET 10 SDK and Docker.

```sh
docker compose up -d --wait     # MySQL on host port 3307, Redis on 6379; returns once both are healthy
cd backend
cp PaymentIntegration.Api/appsettings.Development.example.json PaymentIntegration.Api/appsettings.Development.json
                                # then fill in the 2C2P merchant ID and secret
dotnet run --project PaymentIntegration.Api     # http://localhost:5263, Swagger UI at /swagger
```

In Development, the API applies any pending EF Core migrations when it starts, so the first run creates the tables and the seed data below. In production, migrations would run as a separate deployment step instead, so that several app instances never migrate the same database at once.

### 2C2P settings

The `TwoC2P` settings are split in two. The merchant ID and secret key go only in the gitignored `appsettings.Development.json`. The committed `appsettings.json` holds the rest:

- `BaseUrl` includes the PGW API version (`https://sandbox-pgw.2c2p.com/payment/4.3/`), so moving to another version is a config change.
- `BackendReturnUrl` is where 2C2P posts the payment notification. 2C2P cannot reach `localhost`, so to receive notifications locally, expose the API through a tunnel (for example ngrok) and override this URL in `appsettings.Development.json`.

The settings are checked the first time a payment is made, not at startup, so the customer and product endpoints work without them.

To add a migration after changing the model:

```sh
cd backend
dotnet tool restore     # installs the pinned dotnet-ef version
dotnet ef migrations add <Name> -p PaymentIntegration.Infrastructure -s PaymentIntegration.Api -o Persistence/Migrations
```

### Seed data

There are no endpoints for creating customers or products, so the first migration inserts these rows with fixed IDs.

| ID | Customer | Email |
| --- | --- | --- |
| 1 | Alice Tan | alice.tan@example.com |
| 2 | Ben Lim | ben.lim@example.com |
| 3 | Chloe Ng | chloe.ng@example.com |

| ID | Product | Price | Payment type |
| --- | --- | --- | --- |
| 1 | Wireless Mouse | SGD 29.90 | OneTime |
| 2 | Mechanical Keyboard | SGD 149.00 | OneTime |
| 3 | Cloud Storage Plan | SGD 9.99 | Recurring (saves the card) |
| 4 | Music Streaming Membership | SGD 12.98 | Recurring (saves the card) |

## Database changes

### Columns added to `transactions`

| Column | Type | Why it was added |
| --- | --- | --- |
| `payment_token` | varchar(512), nullable | The token 2C2P returns from its Payment Token API. The spec asks for it to be stored. It is null until 2C2P has returned one. |
| `redirect_url` | varchar(2048), nullable | The 2C2P payment page URL that comes back with the token. A repeat checkout request with the same `Idempotency-Key` returns this stored URL instead of calling 2C2P a second time. |
| `customer_card_id` | int, nullable, foreign key to `customer_cards` | The saved card used for the payment. It is null when the customer did not pick a saved card. |

### Status rules

A transaction starts as `Pending`. Its status changes only through two methods on the entity:

- `MarkSucceeded(...)` sets the status to `Succeeded`, records the 2C2P result fields and sets `PaidAt`.
- `MarkFailed(...)` sets the status to `Failed` and records the 2C2P result fields.

Both methods act only while the status is `Pending`. After a transaction becomes `Succeeded` or `Failed`, it never changes. A later call returns `false` and leaves the record as it is, so a duplicate or late 2C2P notification cannot overturn a final result. `Status`, `PaidAt` and the result fields (`RespCode`, `RespDesc`, `ProviderRef`, `PaymentChannel`, `MaskedCardNo`) have private setters, so no other code can change them directly.
