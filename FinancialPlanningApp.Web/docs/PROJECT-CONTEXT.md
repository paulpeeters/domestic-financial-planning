# Project Context (Compact, Current)

## 1) Stack and Runtime
- ASP.NET Razor Pages, Dapper, MySQL/MariaDB.
- SQL migrations via embedded scripts in `Database/Migrations`.
- `SqlDateOnlyTypeHandler` is registered in `Program.cs`.
- Repository hygiene is active: `.gitignore` excludes build output, IDE state, local imports/financial files, and `secrets.json`.
- Appsettings may contain placeholders such as `@{DB_HOST}`. Database connection placeholders are resolved from `FinancialPlanningApp.Web/secrets.json` or same-named environment variables; `secrets.template.json` documents the required keys and contains no real values.
- Application version is pinned through csproj assembly metadata (`Version` currently `1.0.0.0`) and shown in a shared Bootstrap "Over" modal from the main navigation.
- Request localization is configured with default `nl-BE` (EUR currency formatting).
- Decimal form binding accepts both comma and dot decimal separators; payment amount inputs use text + decimal input mode to avoid browser locale conversion issues.
- The visible application UI is Dutch (`nl-BE`); internal route names, enum values, provider keys, and database codes remain English/stable.

## 2) Imports and Parsing
- CODA parser follows Febelfin 2025 reference: `docs/CODA-REFERENCE.md`.
- CODA unique key (`SourceReference`) is strict and built from bank-defined identity fields (`account#year#page#transaction`).
- CODA duplicates are checked strictly on `SourceReference` (not on description/amount).
- Missing CODA unique key => invalid transaction, not duplicate.
- Credit card import is PDF-based (Europabank statements); CSV path is not active for credit card statements.
- Import allowed only for registered sources:
  - `registered_bank_accounts` (CODA),
  - `registered_credit_cards` (credit card PDF).
- Import summary includes duplicate detail breakdown (same-source vs cross-source + matched existing row info).

## 3) Reconciliation and Tracking
- Mapping supports recurring payment, card settlement, planned/extra deposit, internal transfer, extra expense.
- Card settlements are excluded from cost totals because the underlying card transactions carry the actual expenses.
- Search includes both description and notes.
- Reconciliation search also matches the description and matching keywords of an already mapped recurring payment template.
- Mapped rows show actual selected mapping in UI.
- Candidate ranking includes keyword matching and amount proximity.
- Payment/report logic uses mapped month where applicable (not only transaction month).

## 4) Reports
- Dashboard shows current month, previous month, and two months ago because CODA statements are usually only complete around the 6th of the following month and some costs, such as energy, are paid with additional lag.
- Dashboard paid/provisioned totals and latest account balance are tenant-scoped so every user in the active tenant sees the same figures.
- Dashboard shows presumed paid-but-not-registered automatic costs separately from actual paid costs. These are due recurrent payments with `DirectDebit` or `CreditCard` payment method whose due/payment date has passed and for which no mapped payment exists yet; partially paid/mapped payments are not included as presumed paid. This pending layer is grouped by due/payment month, while actual paid costs remain grouped by mapped reporting month.
- Dashboard shows presumed provision deposits separately from actual provisioned totals. Tenant settings control monthly provision day and optional fixed monthly provision amount; when no amount is configured the dashboard falls back to planned yearly cost divided by 12 and rounded up.
- Dashboard recent-month cards list the individual presumed/unregistered items so mismatches can be traced to a specific template, payment method, or due date.
- Report actuals for mapped recurring costs and mapped expense drill-downs are tenant-scoped, not current-user scoped.
- `Monthly Planned vs Paid` includes mapped-to-month logic and yearly totals, and uses the same positive paid-cost and paid-minus-expected diff presentation as the dashboard.
- Dashboard and `Monthly Planned vs Paid` expected costs include historical templates when their schedule and `ActiveFrom`/`ActiveUntil` are valid for the report month, even if they are no longer currently enabled.
- `Monthly Cost Variance` exists; it explains monthly expected-vs-paid differences per recurring payment, includes mapped transactions, excludes card settlements from paid costs, and treats templates as planned for the selected report month when their schedule and `ActiveFrom`/`ActiveUntil` are valid even if the template is no longer currently enabled.
- For the current month, `Monthly Cost Variance` also shows presumed paid-but-not-registered automatic payments separately. These are grouped by due/payment month and include detail rows with the mapped reporting month; they are not stored as actual payments.
- `Provision Forecast` exists; it shows expected recurrent costs, actual recurrent costs, recurrent variance, yearly average, provision paid/forecast, projected end balance, actual month-end balance, and balance difference.
- `Planned Items Overview` exists, includes:
  - planned schedule,
  - planned vs actual monthly/yearly,
  - previous year actual,
  - totals row.
- In planned overview, templates with no valid occurrence in selected year are excluded (date-range based, not only active flag).
- `Recurring Payment Provision` exists:
  - rows are active recurring payment templates with valid occurrences in the selected year,
  - 12 month columns show mapped paid amounts per template/month,
  - template `ActiveFrom`/`ActiveUntil` bounds determine which months count for planning and future expected totals,
  - actual paid amounts are shown by mapped period month even when the mapped month is outside the planned occurrence months,
  - month cells show `-` when no payment is expected and none is paid; paid amounts in non-expected months are highlighted red,
  - yearly planned, year-to-date paid, future expected, and deviation percent are shown,
  - yearly planned is based on concrete due dates within `ActiveFrom`/`ActiveUntil`,
  - configurable deviation threshold defaults to 2%,
  - evaluation mode can be projected year-end or year-to-date pro-rata,
  - schedule uses compact notation such as `15/[01-12]`, `15/04`, `15/[02,05,07,11]`, with payment lag shown as `+2m`,
  - green highlights expected expenses below planned by more than the threshold; red highlights expected expenses above planned by more than the threshold.

## 5) Tenancy, Auth, and Roles
- Multi-tenant foundation is active (`tenants`, `user_tenants`, `tenant_id` across business tables).
- User session carries `tenant_id` claim and optional `global_admin` claim.
- Tenant switch is supported:
  - dedicated select page,
  - quick switch from header tenant dropdown.
- Preferred tenant is stored per user and used at login when multiple memberships exist.
- Roles in tenant: `OWNER`, `ADMIN`, `EDITOR`, `VIEWER`.
- Global role: `GLOBAL_ADMIN` (implemented as `app_users.is_global_admin` + claim/policy).
- Self-registration toggle exists via app setting `allow_self_registration`.

## 6) Management UI
- Header:
  - left/right includes current tenant short display + full name hover,
  - user avatar + first name dropdown,
  - user display uses tenant-scoped member overrides for the active tenant when present.
- Destructive or state-changing confirmations use the shared Bootstrap confirmation modal (`data-confirm-*`), not browser JavaScript alerts.
- Reconciliation has a manual entry page at `/Reconciliation/Manual` for users who do not import bank statements:
  - lists expected recurrent payments for a selected month and lets the user manually mark them paid,
  - manual payments are stored as `payment_executions` with `source_type = MANUAL` and mapped to the selected reporting month,
  - monthly provision deposits can be entered as `PLANNED_DEPOSIT`; the suggested amount is the planned yearly cost divided by 12 and rounded up,
  - current/month-end bank balance can be entered into `account_monthly_balances`,
  - manual credit card settlements can be entered as `CARD_SETTLEMENT`; they are mapped but excluded from cost totals to avoid double counting card transactions.
- Global management page:
  - global user profile fields (first name, last name, avatar URL), active state, and global-admin flag are edited through Bootstrap modals,
  - global admin grants; removing the flag from inactive users is allowed because the "last active global admin" safeguard only applies to active users,
  - create tenant,
  - deactivate/reactivate tenants,
  - purge inactive tenants after captcha, typed confirmation, checkbox, and final modal confirmation; purge deletes tenant data and users linked only to that tenant,
  - add user to tenant,
  - create user + assign tenant/role,
  - deactivate/reactivate users,
  - reset passwords for any user via Bootstrap modal,
  - purge inactive non-global-admin users after captcha, typed confirmation, checkbox, and final modal confirmation; purge deletes memberships, login history, and user-owned financial data,
  - self-registration enable/disable.
- Mail settings page:
  - global-admin only at `/Account/MailSettings`,
  - settings are stored in `mail_settings` with the global row `tenant_id = NULL` and `scope_key = 'global'`,
  - provider/API and SMTP relay fallback fields are persisted, with stored secrets retained when secret fields are left blank,
  - test email sending is available from the page for Brevo, Resend, Postmark, SendGrid, Mailgun, and Custom SMTP; "Save and send test" persists the current form values before sending,
  - Custom SMTP supports Gmail app-password sending through `smtp.gmail.com`, SSL/TLS, port `465` and also STARTTLS-style SMTP such as port `587`.
- Tenant management page (tabbed):
  - current members are shown first,
  - tenant display fields (name + short name max 10) are edited through a Bootstrap modal,
  - tenant-specific monthly provision day and optional fixed provision amount are edited through the same tenant settings modal,
  - tenant admins can edit member first name, last name, and avatar as tenant-scoped display overrides on `user_tenants`, so they do not change the global `app_users` profile used by other tenants,
  - create/add/update/remove tenant members with safeguards,
  - adding an existing user uses a Bootstrap modal with users not yet linked to the active tenant,
  - tenant member role/access edits and password resets use Bootstrap modals.
  - tenant member removal is access removal (membership deactivation); inactive members can be restored from the same page.
- Profile page:
  - users can change their own password via Bootstrap modal; current password is required.
- Forgot-password flow:
  - `/Account/ForgotPassword` sends a reset link without revealing whether an email exists,
  - `/Account/ResetPassword?token=...` lets the user set a new password,
  - reset tokens are stored hashed in `password_reset_tokens`, expire after 30 minutes, and open tokens are expired when a new one is issued or used,
  - reset links use Mail Settings `base_url` when configured, otherwise the current request URL.
- Recurring payments UI:
  - create/edit forms share the same Razor partial and group fields by basics, planning, recognition, custom payment months, and monthly profile; fixed amount fields are hidden for monthly profile mode, monthly profile fields are only shown for monthly profile mode, and custom payment months are only shown for the custom-months yearly budget periodicity,
  - custom payment months are edited with 12 month toggle buttons and stored as the existing comma-separated `payment_months` value; matching keywords are edited as UI tags and stored as the existing comma-separated keyword text,
  - monthly profile edit shows all 12 months in one table and calculates the yearly total client-side,
  - display order is managed from the payments overview through a reorder mode with drag-and-drop rows plus up/down buttons; newly created templates are appended automatically,
  - edit navigation from the payments overview uses a short-lived session snapshot (`NavKey` + index) of the ordered template ids, so previous/next navigation remains stable even when description or keywords are edited; saving/cancelling returns to the grid page that contains the last edited template and anchors to that row,
  - edit actions include save-and-return, save-stay, save-and-next, previous, next, and cancel; previous/next/cancel show a Bootstrap unsaved-changes modal when the form is dirty,
  - create/edit forms include amount mode, amount per payment or monthly profile, periodicity, expected payment day, expected payment month/custom months when relevant, payment method, validity range, and matching keywords,
  - payments overview shows expected payment day/month schedule and validity status.
  - "currently active" is derived dynamically from `ActiveFrom`/`ActiveUntil`; `is_active` is kept only as list visibility/archive state, not as a period-validity or planning rule.
  - recurring payments can be created from an existing template; expired templates can be restarted by cloning them into a new validity period.
  - creating/editing a recurring payment blocks overlapping validity periods with the same description and payment method to prevent accidental duplicate planning when a stopped payment is restarted.
  - payments overview displays payment methods in Dutch (`Domiciliëring`, `Kredietkaart`, `Overschrijving`) and uses compact Dutch schedule labels such as `11de vd maand`, `17de in feb, mei, jul, nov`, and `1ste apr`.
  - periodicity supports custom months yearly budget via `payment_months`; amount is the yearly budget and expected occurrences divide it across the selected months.
  - expected amount mode supports `Fixed` and `MonthlyProfile`; monthly profile stores 12 expected amounts and is intended for seasonal variable costs such as energy.
  - `payment_lag_months` can shift reconciliation suggestions from payment month to reporting/consumption month; e.g. lag 2 maps a February payment to December consumption.

## 7) Security and Audit
- Login history is implemented:
  - table: `auth_login_attempts`,
  - logs success/failure, reason, email, user_id, IP, user-agent,
  - UI page: `/Account/LoginHistory` (global admin only).
- Current deployment note:
  - with `sslh` non-transparent mode, recorded IP may be a proxy/host IP instead of the real client IP.

## 8) Open Technical Notes
- Reverse proxy chain currently prioritizes stability over real client IP propagation.
- Optional future work:
  - deeper proxy/IP tracing improvements,
  - additional UI polish for management screens,
  - optional throttling/lockout policy on repeated login failures.
