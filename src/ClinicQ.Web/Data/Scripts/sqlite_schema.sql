-- ClinicQ - SQLite fallback schema (mirrors db/001_schema.sql for SQL Server).
-- Executed at startup when ConnectionStrings:Default is empty. Idempotent.
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Branches
(
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    Code           TEXT    NOT NULL UNIQUE,
    Name           TEXT    NOT NULL,
    AddressLine    TEXT    NOT NULL,
    City           TEXT    NOT NULL,
    State          TEXT    NOT NULL,
    PostalCode     TEXT    NOT NULL,
    Phone          TEXT    NOT NULL,
    TimeZoneId     TEXT    NOT NULL DEFAULT 'Central Standard Time',
    TaxRatePercent REAL    NOT NULL DEFAULT 0,
    IsActive       INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS BranchSlotRules
(
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    BranchId            INTEGER NOT NULL UNIQUE REFERENCES Branches(Id),
    OpenTime            TEXT    NOT NULL,
    CloseTime           TEXT    NOT NULL,
    SlotDurationMinutes INTEGER NOT NULL CHECK (SlotDurationMinutes BETWEEN 5 AND 240),
    WorkingDaysMask     INTEGER NOT NULL,
    MaxBookingsPerSlot  INTEGER NOT NULL DEFAULT 1,
    MinLeadTimeHours    INTEGER NOT NULL DEFAULT 1,
    MaxAdvanceDays      INTEGER NOT NULL DEFAULT 60
);

CREATE TABLE IF NOT EXISTS Doctors
(
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    BranchId      INTEGER NOT NULL REFERENCES Branches(Id),
    FullName      TEXT    NOT NULL,
    Specialty     TEXT    NOT NULL,
    Email         TEXT    NOT NULL,
    LicenseNumber TEXT    NOT NULL,
    IsActive      INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS IX_Doctors_Branch ON Doctors(BranchId);

CREATE TABLE IF NOT EXISTS Patients
(
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Mrn         TEXT NOT NULL UNIQUE,
    FirstName   TEXT NOT NULL,
    LastName    TEXT NOT NULL,
    DateOfBirth TEXT NOT NULL,
    Gender      TEXT NOT NULL,
    Email       TEXT NOT NULL,
    Phone       TEXT NOT NULL,
    AddressLine TEXT NULL,
    City        TEXT NULL,
    State       TEXT NULL,
    PostalCode  TEXT NULL,
    Allergies   TEXT NULL,
    CreatedAt   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Patients_LastName ON Patients(LastName, FirstName);

CREATE TABLE IF NOT EXISTS Appointments
(
    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    PatientId             INTEGER NOT NULL REFERENCES Patients(Id),
    DoctorId              INTEGER NOT NULL REFERENCES Doctors(Id),
    BranchId              INTEGER NOT NULL REFERENCES Branches(Id),
    ScheduledStart        TEXT    NOT NULL,
    ScheduledEnd          TEXT    NOT NULL,
    Status                TEXT    NOT NULL CHECK (Status IN ('Requested','Confirmed','CheckedIn','InConsultation','Completed','Cancelled','NoShow')),
    Reason                TEXT    NOT NULL,
    Notes                 TEXT    NULL,
    CreatedAt             TEXT    NOT NULL,
    ConfirmedAt           TEXT    NULL,
    CheckedInAt           TEXT    NULL,
    ConsultationStartedAt TEXT    NULL,
    CompletedAt           TEXT    NULL,
    CancelledAt           TEXT    NULL,
    CancellationReason    TEXT    NULL
);
CREATE INDEX IF NOT EXISTS IX_Appointments_Doctor_Start ON Appointments(DoctorId, ScheduledStart);
CREATE INDEX IF NOT EXISTS IX_Appointments_Patient ON Appointments(PatientId);
CREATE INDEX IF NOT EXISTS IX_Appointments_Branch_Start ON Appointments(BranchId, ScheduledStart);

CREATE TABLE IF NOT EXISTS FeeSchedule
(
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    BranchId      INTEGER NOT NULL REFERENCES Branches(Id),
    ServiceCode   TEXT    NOT NULL,
    Description   TEXT    NOT NULL,
    Amount        REAL    NOT NULL CHECK (Amount >= 0),
    IsActive      INTEGER NOT NULL DEFAULT 1,
    EffectiveFrom TEXT    NOT NULL,
    UNIQUE (BranchId, ServiceCode, EffectiveFrom)
);

CREATE TABLE IF NOT EXISTS Invoices
(
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceNumber   TEXT    NOT NULL UNIQUE,
    AppointmentId   INTEGER NOT NULL REFERENCES Appointments(Id),
    PatientId       INTEGER NOT NULL REFERENCES Patients(Id),
    BranchId        INTEGER NOT NULL REFERENCES Branches(Id),
    Status          TEXT    NOT NULL CHECK (Status IN ('Draft','Issued','PartiallyPaid','Paid','Void')),
    Subtotal        REAL    NOT NULL,
    DiscountPercent REAL    NOT NULL DEFAULT 0,
    DiscountAmount  REAL    NOT NULL DEFAULT 0,
    TaxRatePercent  REAL    NOT NULL DEFAULT 0,
    TaxAmount       REAL    NOT NULL DEFAULT 0,
    Total           REAL    NOT NULL,
    AmountPaid      REAL    NOT NULL DEFAULT 0,
    IssuedAt        TEXT    NOT NULL,
    DueDate         TEXT    NOT NULL,
    PaidAt          TEXT    NULL
);
CREATE INDEX IF NOT EXISTS IX_Invoices_Branch_Issued ON Invoices(BranchId, IssuedAt);
-- A visit may hold only one live invoice, but voiding it allows a corrected invoice to be issued.
CREATE UNIQUE INDEX IF NOT EXISTS UX_Invoices_Appointment_Active ON Invoices(AppointmentId) WHERE Status <> 'Void';

CREATE TABLE IF NOT EXISTS InvoiceLines
(
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceId   INTEGER NOT NULL REFERENCES Invoices(Id) ON DELETE CASCADE,
    ServiceCode TEXT    NOT NULL,
    Description TEXT    NOT NULL,
    Quantity    INTEGER NOT NULL CHECK (Quantity > 0),
    UnitPrice   REAL    NOT NULL,
    LineTotal   REAL    NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_InvoiceLines_Invoice ON InvoiceLines(InvoiceId);

CREATE TABLE IF NOT EXISTS Payments
(
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceId  INTEGER NOT NULL REFERENCES Invoices(Id),
    Amount     REAL    NOT NULL CHECK (Amount > 0),
    Method     TEXT    NOT NULL CHECK (Method IN ('Cash','Card','Insurance','BankTransfer')),
    Reference  TEXT    NULL,
    PaidAt     TEXT    NOT NULL,
    ReceivedBy TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Payments_Invoice ON Payments(InvoiceId);

CREATE TABLE IF NOT EXISTS Prescriptions
(
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    AppointmentId INTEGER NOT NULL REFERENCES Appointments(Id),
    PatientId     INTEGER NOT NULL REFERENCES Patients(Id),
    DoctorId      INTEGER NOT NULL REFERENCES Doctors(Id),
    IssuedAt      TEXT    NOT NULL,
    Diagnosis     TEXT    NULL,
    Instructions  TEXT    NULL
);
CREATE INDEX IF NOT EXISTS IX_Prescriptions_Patient ON Prescriptions(PatientId);

CREATE TABLE IF NOT EXISTS PrescriptionItems
(
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    PrescriptionId INTEGER NOT NULL REFERENCES Prescriptions(Id) ON DELETE CASCADE,
    Medication     TEXT    NOT NULL,
    Dosage         TEXT    NOT NULL,
    Frequency      TEXT    NOT NULL,
    DurationDays   INTEGER NOT NULL,
    Notes          TEXT    NULL
);

CREATE TABLE IF NOT EXISTS LabReports
(
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    PatientId     INTEGER NOT NULL REFERENCES Patients(Id),
    AppointmentId INTEGER NULL REFERENCES Appointments(Id),
    FileName      TEXT    NOT NULL,
    ContentType   TEXT    NOT NULL,
    SizeBytes     INTEGER NOT NULL,
    StoragePath   TEXT    NOT NULL,
    TestName      TEXT    NOT NULL,
    Notes         TEXT    NULL,
    UploadedAt    TEXT    NOT NULL,
    UploadedBy    TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_LabReports_Patient ON LabReports(PatientId, UploadedAt);

CREATE TABLE IF NOT EXISTS Users
(
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Username     TEXT    NOT NULL UNIQUE,
    DisplayName  TEXT    NOT NULL,
    Email        TEXT    NOT NULL,
    PasswordHash TEXT    NOT NULL,
    Role         TEXT    NOT NULL CHECK (Role IN ('Admin','Receptionist','Doctor','Billing')),
    BranchId     INTEGER NULL REFERENCES Branches(Id),
    IsActive     INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS ReconciliationRuns
(
    Id                            INTEGER PRIMARY KEY AUTOINCREMENT,
    BranchId                      INTEGER NOT NULL REFERENCES Branches(Id),
    RunAt                         TEXT    NOT NULL,
    PeriodStart                   TEXT    NOT NULL,
    PeriodEnd                     TEXT    NOT NULL,
    InvoiceCount                  INTEGER NOT NULL,
    TotalInvoiced                 REAL    NOT NULL,
    TotalPaid                     REAL    NOT NULL,
    Outstanding                   REAL    NOT NULL,
    UnbilledCompletedAppointments INTEGER NOT NULL,
    Notes                         TEXT    NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS IX_ReconciliationRuns_Branch_RunAt ON ReconciliationRuns(BranchId, RunAt);
