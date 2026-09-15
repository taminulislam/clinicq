# ClinicQ architecture

ClinicQ is a single ASP.NET Core 8 web application that serves two faces over the same
domain and data access layer:

- a **Razor Pages portal** for clinic staff (cookie authentication), and
- a **versioned REST API** under `/api/v1` for integrations and mobile clients (JWT bearer).

Keeping both in one process means one deployment unit, one connection pool and one set of
business rules; the split is by presentation only.

## Components

```mermaid
flowchart TB
    subgraph Clients
        Browser["Clinic staff browser<br/>(Bootstrap 5, jQuery, DataTables)"]
        ApiClient["API client<br/>(Swagger UI, Postman/Newman)"]
    end

    subgraph WebApp["ClinicQ.Web (ASP.NET Core 8)"]
        Pages["Razor Pages<br/>cookie auth"]
        Api["REST controllers /api/v1<br/>JWT bearer"]
        Validation["FluentValidation<br/>request rules"]
        Services["Application services<br/>Appointment, Billing, Prescription,<br/>LabReport, Dashboard"]
        Repos["Dapper repositories<br/>+ ISqlDialect"]
        Jobs["Hangfire recurring jobs<br/>reminders, follow-ups, reconciliation"]
        Health["/health check"]
    end

    subgraph Domain["ClinicQ.Domain (no dependencies)"]
        StateMachine["AppointmentStateMachine"]
        Slots["SlotGenerator + BranchSlotRule"]
        Fees["FeeCalculator + DiscountPolicy"]
        Invoices["Invoice aggregate"]
    end

    subgraph Abstractions["Infrastructure abstractions"]
        Email["IEmailSender"]
        Storage["IFileStorage"]
        Pdf["IPdfService (QuestPDF)"]
    end

    subgraph External["External systems"]
        SqlServer[("Azure SQL / SQL Server<br/>tables + stored procedures")]
        Sqlite[("SQLite app.db<br/>local fallback")]
        Blob[("Azure Blob Storage<br/>lab reports")]
        Disk[("Local disk<br/>storage fallback")]
        SendGrid["SendGrid"]
        Console["Console logger<br/>email fallback"]
        AppInsights["Application Insights"]
    end

    Browser --> Pages
    ApiClient --> Api
    Pages --> Services
    Api --> Validation --> Services
    Services --> Domain
    Services --> Repos
    Services --> Abstractions
    Jobs --> Services
    Jobs --> Repos
    Repos --> SqlServer
    Repos --> Sqlite
    Storage --> Blob
    Storage --> Disk
    Email --> SendGrid
    Email --> Console
    Health --> Repos
    WebApp -. Serilog .-> AppInsights
```

### Projects

| Project | Responsibility |
| --- | --- |
| `src/ClinicQ.Domain` | Entities, the appointment state machine, slot rules, fee and discount calculation, invoice aggregate, domain exceptions. No framework or database dependencies, so it is trivially unit-testable. |
| `src/ClinicQ.Web` | Razor Pages, REST controllers, FluentValidation validators, Dapper repositories, Hangfire jobs, QuestPDF documents, email/storage abstractions, DI composition. |
| `tests/ClinicQ.Tests` | xUnit tests: domain rules, validators, repositories on SQLite, services, jobs and `WebApplicationFactory` API tests. |

## Data access

Data access is Dapper over hand-written SQL. There is no ORM mapping layer, so the SQL in the
repositories is the same SQL that runs in production.

Two providers are supported and chosen at startup from configuration:

| | SQL Server / Azure SQL | SQLite (fallback) |
| --- | --- | --- |
| Selected when | `ConnectionStrings:Default` is set | `ConnectionStrings:Default` is empty |
| Schema | Deployed from `db/001_schema.sql`, `002_seed.sql`, `003_procs.sql` | Created at startup from the embedded `Data/Scripts/sqlite_schema.sql` |
| Slot lookup | `dbo.usp_GetAvailableSlots` | `SqliteSlotRepository` + `SlotGenerator` |
| Billing reconciliation | `dbo.usp_ReconcileBilling` | `SqliteBillingReconciliationRepository` (equivalent inline SQL) |
| Branch revenue | `dbo.usp_GetBranchRevenue` | Inline SQL in `DashboardRepository` |

`ISqlDialect` isolates the few fragments that genuinely differ (identity retrieval, paging,
case-insensitive `LIKE`); everything else is written in the common subset both engines accept.
The stored procedures and the SQLite/domain implementations are deliberately kept equivalent, and
`SlotGeneratorTests` plus `RepositoryTests` pin that behaviour down.

## Data model

```mermaid
erDiagram
    BRANCHES ||--o{ DOCTORS : employs
    BRANCHES ||--|| BRANCH_SLOT_RULES : "booking rules"
    BRANCHES ||--o{ FEE_SCHEDULE : prices
    BRANCHES ||--o{ APPOINTMENTS : hosts
    BRANCHES ||--o{ INVOICES : bills
    BRANCHES ||--o{ RECONCILIATION_RUNS : reconciles
    PATIENTS  ||--o{ APPOINTMENTS : books
    PATIENTS  ||--o{ LAB_REPORTS : has
    PATIENTS  ||--o{ PRESCRIPTIONS : receives
    DOCTORS   ||--o{ APPOINTMENTS : attends
    DOCTORS   ||--o{ PRESCRIPTIONS : issues
    APPOINTMENTS ||--o| INVOICES : "generates (one live)"
    APPOINTMENTS ||--o{ PRESCRIPTIONS : produces
    APPOINTMENTS ||--o{ LAB_REPORTS : attaches
    INVOICES  ||--|{ INVOICE_LINES : contains
    INVOICES  ||--o{ PAYMENTS : "settled by"
    PRESCRIPTIONS ||--|{ PRESCRIPTION_ITEMS : lists
    USERS     }o--o| BRANCHES : "assigned to"

    BRANCHES {
        int Id PK
        string Code UK
        string Name
        decimal TaxRatePercent
        bool IsActive
    }
    BRANCH_SLOT_RULES {
        int Id PK
        int BranchId FK
        string OpenTime
        string CloseTime
        int SlotDurationMinutes
        int WorkingDaysMask
        int MaxBookingsPerSlot
        int MinLeadTimeHours
        int MaxAdvanceDays
    }
    DOCTORS {
        int Id PK
        int BranchId FK
        string FullName
        string Specialty
        string LicenseNumber
    }
    PATIENTS {
        int Id PK
        string Mrn UK
        string FirstName
        string LastName
        datetime DateOfBirth
        string Allergies
    }
    APPOINTMENTS {
        int Id PK
        int PatientId FK
        int DoctorId FK
        int BranchId FK
        datetime ScheduledStart
        datetime ScheduledEnd
        string Status
        string Reason
        datetime CheckedInAt
        datetime ConsultationStartedAt
        datetime CompletedAt
    }
    FEE_SCHEDULE {
        int Id PK
        int BranchId FK
        string ServiceCode
        decimal Amount
        bool IsActive
        datetime EffectiveFrom
    }
    INVOICES {
        int Id PK
        string InvoiceNumber UK
        int AppointmentId FK
        int BranchId FK
        string Status
        decimal Subtotal
        decimal DiscountAmount
        decimal TaxAmount
        decimal Total
        decimal AmountPaid
    }
    INVOICE_LINES {
        int Id PK
        int InvoiceId FK
        string ServiceCode
        int Quantity
        decimal UnitPrice
        decimal LineTotal
    }
    PAYMENTS {
        int Id PK
        int InvoiceId FK
        decimal Amount
        string Method
        datetime PaidAt
    }
    PRESCRIPTIONS {
        int Id PK
        int AppointmentId FK
        int PatientId FK
        int DoctorId FK
        datetime IssuedAt
    }
    PRESCRIPTION_ITEMS {
        int Id PK
        int PrescriptionId FK
        string Medication
        string Dosage
        string Frequency
        int DurationDays
    }
    LAB_REPORTS {
        int Id PK
        int PatientId FK
        int AppointmentId FK
        string FileName
        string StoragePath
        long SizeBytes
    }
    RECONCILIATION_RUNS {
        int Id PK
        int BranchId FK
        datetime RunAt
        decimal TotalInvoiced
        decimal TotalPaid
        decimal Outstanding
        int UnbilledCompletedAppointments
    }
    USERS {
        int Id PK
        string Username UK
        string PasswordHash
        string Role
        int BranchId FK
    }
```

An appointment may hold only **one live invoice**, enforced by a filtered unique index on
`AppointmentId where Status <> 'Void'`. Voiding an invoice therefore frees the visit so a
corrected invoice can be issued, which a plain unique constraint would have blocked.

## Appointment state machine

Status changes only ever happen through guarded domain methods; the API and the portal both call
the same code, so an invalid move is impossible from either surface.

```mermaid
stateDiagram-v2
    [*] --> Requested
    Requested --> Confirmed
    Requested --> Cancelled
    Confirmed --> CheckedIn
    Confirmed --> Cancelled
    Confirmed --> NoShow
    CheckedIn --> InConsultation
    CheckedIn --> Cancelled
    InConsultation --> Completed
    Completed --> [*]
    Cancelled --> [*]
    NoShow --> [*]
```

Rules worth calling out:

- `NoShow` is only reachable from `Confirmed`, and only once the slot has started.
- `Cancelled` requires a reason and is not reachable once the consultation has begun.
- Wait time is derived as `ConsultationStartedAt - CheckedInAt` and feeds the dashboard.
- `InvalidAppointmentTransitionException` maps to **409 Conflict**; other `DomainException`s map
  to **400 Bad Request** and `EntityNotFoundException` to **404**, all as RFC 7807 ProblemDetails.

## Booking rules

`AppointmentService.RequestAsync` enforces the per-branch `BranchSlotRule` before anything is
written: the branch must be open that weekday, the start must fall exactly on a slot boundary
inside opening hours, the minimum lead time and maximum advance window must be respected, and the
slot must have capacity left (`MaxBookingsPerSlot` allows deliberate overbooking). Cancelled and
no-show appointments do not consume capacity, so a cancellation frees the slot again.

## Background jobs

Hangfire runs in-process with in-memory storage (no extra infrastructure to operate for a demo;
swap in `Hangfire.SqlServer` for a real deployment so jobs survive restarts and scale out safely).

| Job | Schedule | Purpose |
| --- | --- | --- |
| `AppointmentReminderJob` | daily 08:00 | Emails patients confirmed for tomorrow. |
| `FollowUpAlertJob` | daily 09:30 | Checks in on visits completed seven days ago. |
| `BillingReconciliationJob` | daily 02:00 | Totals invoiced/paid per branch for the previous day and flags completed visits with no invoice. |

The dashboard at `/hangfire` is restricted to the `Admin` role (plus local requests in Development).

## Cross-cutting concerns

- **Authentication** - cookie for the portal, JWT bearer for the API. Both read the same `Users`
  table and PBKDF2-SHA256 hashes, so one account works on both surfaces. Roles: `Admin`,
  `Receptionist`, `Doctor`, `Billing`.
- **Validation** - FluentValidation validators are shared between the API and the Razor Pages
  forms, so a rule is written once and enforced on both.
- **Logging** - Serilog with the console sink and request logging; the Application Insights sink is
  documented and commented in `Program.cs`.
- **Health** - `/health` runs a `SELECT 1` against the configured provider and is used by the
  App Service health check and the pipeline's staging smoke test.
- **Secrets** - never in the repository. `appsettings.json` ships empty values; Azure supplies them
  through Key Vault references resolved by the web app's system-assigned managed identity.
