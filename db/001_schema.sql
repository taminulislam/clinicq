/*
    ClinicQ - SQL Server schema
    Run order: 001_schema.sql -> 002_seed.sql -> 003_procs.sql
    Idempotent: safe to re-run against an existing database.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------- Branches */
IF OBJECT_ID('dbo.Branches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Branches
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Branches PRIMARY KEY,
        Code            NVARCHAR(10)   NOT NULL CONSTRAINT UQ_Branches_Code UNIQUE,
        Name            NVARCHAR(100)  NOT NULL,
        AddressLine     NVARCHAR(200)  NOT NULL,
        City            NVARCHAR(100)  NOT NULL,
        State           NVARCHAR(50)   NOT NULL,
        PostalCode      NVARCHAR(20)   NOT NULL,
        Phone           NVARCHAR(30)   NOT NULL,
        TimeZoneId      NVARCHAR(60)   NOT NULL CONSTRAINT DF_Branches_TimeZone DEFAULT ('Central Standard Time'),
        TaxRatePercent  DECIMAL(5,2)   NOT NULL CONSTRAINT DF_Branches_Tax DEFAULT (0),
        IsActive        BIT            NOT NULL CONSTRAINT DF_Branches_IsActive DEFAULT (1)
    );
END
GO

/* --------------------------------------------------------- BranchSlotRules */
IF OBJECT_ID('dbo.BranchSlotRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchSlotRules
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BranchSlotRules PRIMARY KEY,
        BranchId            INT          NOT NULL CONSTRAINT FK_BranchSlotRules_Branch REFERENCES dbo.Branches(Id),
        OpenTime            NVARCHAR(5)  NOT NULL,   -- HH:mm, stored as text so SQLite and SQL Server behave identically
        CloseTime           NVARCHAR(5)  NOT NULL,
        SlotDurationMinutes INT          NOT NULL CONSTRAINT CK_SlotRules_Duration CHECK (SlotDurationMinutes BETWEEN 5 AND 240),
        WorkingDaysMask     INT          NOT NULL,
        MaxBookingsPerSlot  INT          NOT NULL CONSTRAINT DF_SlotRules_Max DEFAULT (1),
        MinLeadTimeHours    INT          NOT NULL CONSTRAINT DF_SlotRules_Lead DEFAULT (1),
        MaxAdvanceDays      INT          NOT NULL CONSTRAINT DF_SlotRules_Advance DEFAULT (60),
        CONSTRAINT UQ_BranchSlotRules_Branch UNIQUE (BranchId)
    );
END
GO

/* ----------------------------------------------------------------- Doctors */
IF OBJECT_ID('dbo.Doctors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Doctors
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Doctors PRIMARY KEY,
        BranchId      INT           NOT NULL CONSTRAINT FK_Doctors_Branch REFERENCES dbo.Branches(Id),
        FullName      NVARCHAR(120) NOT NULL,
        Specialty     NVARCHAR(80)  NOT NULL,
        Email         NVARCHAR(200) NOT NULL,
        LicenseNumber NVARCHAR(40)  NOT NULL,
        IsActive      BIT           NOT NULL CONSTRAINT DF_Doctors_IsActive DEFAULT (1)
    );
    CREATE INDEX IX_Doctors_Branch ON dbo.Doctors(BranchId);
END
GO

/* ---------------------------------------------------------------- Patients */
IF OBJECT_ID('dbo.Patients', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Patients
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Patients PRIMARY KEY,
        Mrn         NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Patients_Mrn UNIQUE,
        FirstName   NVARCHAR(80)  NOT NULL,
        LastName    NVARCHAR(80)  NOT NULL,
        DateOfBirth DATETIME2(0)  NOT NULL,
        Gender      NVARCHAR(20)  NOT NULL,
        Email       NVARCHAR(200) NOT NULL,
        Phone       NVARCHAR(30)  NOT NULL,
        AddressLine NVARCHAR(200) NULL,
        City        NVARCHAR(100) NULL,
        State       NVARCHAR(50)  NULL,
        PostalCode  NVARCHAR(20)  NULL,
        Allergies   NVARCHAR(500) NULL,
        CreatedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Patients_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_Patients_LastName ON dbo.Patients(LastName, FirstName);
END
GO

/* ------------------------------------------------------------ Appointments */
IF OBJECT_ID('dbo.Appointments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Appointments
    (
        Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Appointments PRIMARY KEY,
        PatientId             INT           NOT NULL CONSTRAINT FK_Appointments_Patient REFERENCES dbo.Patients(Id),
        DoctorId              INT           NOT NULL CONSTRAINT FK_Appointments_Doctor REFERENCES dbo.Doctors(Id),
        BranchId              INT           NOT NULL CONSTRAINT FK_Appointments_Branch REFERENCES dbo.Branches(Id),
        ScheduledStart        DATETIME2(0)  NOT NULL,
        ScheduledEnd          DATETIME2(0)  NOT NULL,
        Status                NVARCHAR(20)  NOT NULL CONSTRAINT CK_Appointments_Status
                              CHECK (Status IN ('Requested','Confirmed','CheckedIn','InConsultation','Completed','Cancelled','NoShow')),
        Reason                NVARCHAR(300) NOT NULL,
        Notes                 NVARCHAR(2000) NULL,
        CreatedAt             DATETIME2(3)  NOT NULL,
        ConfirmedAt           DATETIME2(3)  NULL,
        CheckedInAt           DATETIME2(3)  NULL,
        ConsultationStartedAt DATETIME2(3)  NULL,
        CompletedAt           DATETIME2(3)  NULL,
        CancelledAt           DATETIME2(3)  NULL,
        CancellationReason    NVARCHAR(300) NULL
    );
    CREATE INDEX IX_Appointments_Doctor_Start ON dbo.Appointments(DoctorId, ScheduledStart) INCLUDE (Status);
    CREATE INDEX IX_Appointments_Patient ON dbo.Appointments(PatientId);
    CREATE INDEX IX_Appointments_Branch_Start ON dbo.Appointments(BranchId, ScheduledStart);
END
GO

/* ------------------------------------------------------------- FeeSchedule */
IF OBJECT_ID('dbo.FeeSchedule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeSchedule
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeeSchedule PRIMARY KEY,
        BranchId      INT           NOT NULL CONSTRAINT FK_FeeSchedule_Branch REFERENCES dbo.Branches(Id),
        ServiceCode   NVARCHAR(30)  NOT NULL,
        Description   NVARCHAR(200) NOT NULL,
        Amount        DECIMAL(10,2) NOT NULL CONSTRAINT CK_FeeSchedule_Amount CHECK (Amount >= 0),
        IsActive      BIT           NOT NULL CONSTRAINT DF_FeeSchedule_IsActive DEFAULT (1),
        EffectiveFrom DATETIME2(0)  NOT NULL,
        CONSTRAINT UQ_FeeSchedule_Branch_Code_Effective UNIQUE (BranchId, ServiceCode, EffectiveFrom)
    );
END
GO

/* ---------------------------------------------------------------- Invoices */
IF OBJECT_ID('dbo.Invoices', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoices
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
        InvoiceNumber   NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Invoices_Number UNIQUE,
        AppointmentId   INT           NOT NULL CONSTRAINT FK_Invoices_Appointment REFERENCES dbo.Appointments(Id),
        PatientId       INT           NOT NULL CONSTRAINT FK_Invoices_Patient REFERENCES dbo.Patients(Id),
        BranchId        INT           NOT NULL CONSTRAINT FK_Invoices_Branch REFERENCES dbo.Branches(Id),
        Status          NVARCHAR(20)  NOT NULL CONSTRAINT CK_Invoices_Status
                        CHECK (Status IN ('Draft','Issued','PartiallyPaid','Paid','Void')),
        Subtotal        DECIMAL(10,2) NOT NULL,
        DiscountPercent DECIMAL(5,2)  NOT NULL CONSTRAINT DF_Invoices_DiscountPct DEFAULT (0),
        DiscountAmount  DECIMAL(10,2) NOT NULL CONSTRAINT DF_Invoices_DiscountAmt DEFAULT (0),
        TaxRatePercent  DECIMAL(5,2)  NOT NULL CONSTRAINT DF_Invoices_TaxPct DEFAULT (0),
        TaxAmount       DECIMAL(10,2) NOT NULL CONSTRAINT DF_Invoices_TaxAmt DEFAULT (0),
        Total           DECIMAL(10,2) NOT NULL,
        AmountPaid      DECIMAL(10,2) NOT NULL CONSTRAINT DF_Invoices_Paid DEFAULT (0),
        IssuedAt        DATETIME2(3)  NOT NULL,
        DueDate         DATETIME2(0)  NOT NULL,
        PaidAt          DATETIME2(3)  NULL,
        CONSTRAINT UQ_Invoices_Appointment UNIQUE (AppointmentId)
    );
    CREATE INDEX IX_Invoices_Branch_Issued ON dbo.Invoices(BranchId, IssuedAt) INCLUDE (Status, Total, AmountPaid);
END
GO

/* ------------------------------------------------------------ InvoiceLines */
IF OBJECT_ID('dbo.InvoiceLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceLines
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceLines PRIMARY KEY,
        InvoiceId   INT           NOT NULL CONSTRAINT FK_InvoiceLines_Invoice REFERENCES dbo.Invoices(Id) ON DELETE CASCADE,
        ServiceCode NVARCHAR(30)  NOT NULL,
        Description NVARCHAR(200) NOT NULL,
        Quantity    INT           NOT NULL CONSTRAINT CK_InvoiceLines_Qty CHECK (Quantity > 0),
        UnitPrice   DECIMAL(10,2) NOT NULL,
        LineTotal   DECIMAL(10,2) NOT NULL
    );
    CREATE INDEX IX_InvoiceLines_Invoice ON dbo.InvoiceLines(InvoiceId);
END
GO

/* ---------------------------------------------------------------- Payments */
IF OBJECT_ID('dbo.Payments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments
    (
        Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
        InvoiceId  INT           NOT NULL CONSTRAINT FK_Payments_Invoice REFERENCES dbo.Invoices(Id),
        Amount     DECIMAL(10,2) NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        Method     NVARCHAR(20)  NOT NULL CONSTRAINT CK_Payments_Method CHECK (Method IN ('Cash','Card','Insurance','BankTransfer')),
        Reference  NVARCHAR(100) NULL,
        PaidAt     DATETIME2(3)  NOT NULL,
        ReceivedBy NVARCHAR(100) NOT NULL
    );
    CREATE INDEX IX_Payments_Invoice ON dbo.Payments(InvoiceId);
END
GO

/* ----------------------------------------------------------- Prescriptions */
IF OBJECT_ID('dbo.Prescriptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Prescriptions
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Prescriptions PRIMARY KEY,
        AppointmentId INT            NOT NULL CONSTRAINT FK_Prescriptions_Appointment REFERENCES dbo.Appointments(Id),
        PatientId     INT            NOT NULL CONSTRAINT FK_Prescriptions_Patient REFERENCES dbo.Patients(Id),
        DoctorId      INT            NOT NULL CONSTRAINT FK_Prescriptions_Doctor REFERENCES dbo.Doctors(Id),
        IssuedAt      DATETIME2(3)   NOT NULL,
        Diagnosis     NVARCHAR(300)  NULL,
        Instructions  NVARCHAR(1000) NULL
    );
    CREATE INDEX IX_Prescriptions_Patient ON dbo.Prescriptions(PatientId);
END
GO

IF OBJECT_ID('dbo.PrescriptionItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrescriptionItems
    (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PrescriptionItems PRIMARY KEY,
        PrescriptionId INT           NOT NULL CONSTRAINT FK_PrescriptionItems_Prescription REFERENCES dbo.Prescriptions(Id) ON DELETE CASCADE,
        Medication     NVARCHAR(150) NOT NULL,
        Dosage         NVARCHAR(80)  NOT NULL,
        Frequency      NVARCHAR(80)  NOT NULL,
        DurationDays   INT           NOT NULL,
        Notes          NVARCHAR(300) NULL
    );
END
GO

/* -------------------------------------------------------------- LabReports */
IF OBJECT_ID('dbo.LabReports', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LabReports
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LabReports PRIMARY KEY,
        PatientId     INT           NOT NULL CONSTRAINT FK_LabReports_Patient REFERENCES dbo.Patients(Id),
        AppointmentId INT           NULL CONSTRAINT FK_LabReports_Appointment REFERENCES dbo.Appointments(Id),
        FileName      NVARCHAR(260) NOT NULL,
        ContentType   NVARCHAR(100) NOT NULL,
        SizeBytes     BIGINT        NOT NULL,
        StoragePath   NVARCHAR(500) NOT NULL,
        TestName      NVARCHAR(150) NOT NULL,
        Notes         NVARCHAR(500) NULL,
        UploadedAt    DATETIME2(3)  NOT NULL,
        UploadedBy    NVARCHAR(100) NOT NULL
    );
    CREATE INDEX IX_LabReports_Patient ON dbo.LabReports(PatientId, UploadedAt DESC);
END
GO

/* ------------------------------------------------------------------- Users */
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username     NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
        DisplayName  NVARCHAR(100) NOT NULL,
        Email        NVARCHAR(200) NOT NULL,
        PasswordHash NVARCHAR(300) NOT NULL,
        Role         NVARCHAR(30)  NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin','Receptionist','Doctor','Billing')),
        BranchId     INT           NULL CONSTRAINT FK_Users_Branch REFERENCES dbo.Branches(Id),
        IsActive     BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1)
    );
END
GO

/* ------------------------------------------------------ ReconciliationRuns */
IF OBJECT_ID('dbo.ReconciliationRuns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReconciliationRuns
    (
        Id                            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReconciliationRuns PRIMARY KEY,
        BranchId                      INT           NOT NULL CONSTRAINT FK_ReconciliationRuns_Branch REFERENCES dbo.Branches(Id),
        RunAt                         DATETIME2(3)  NOT NULL,
        PeriodStart                   DATETIME2(0)  NOT NULL,
        PeriodEnd                     DATETIME2(0)  NOT NULL,
        InvoiceCount                  INT           NOT NULL,
        TotalInvoiced                 DECIMAL(12,2) NOT NULL,
        TotalPaid                     DECIMAL(12,2) NOT NULL,
        Outstanding                   DECIMAL(12,2) NOT NULL,
        UnbilledCompletedAppointments INT           NOT NULL,
        Notes                         NVARCHAR(500) NOT NULL CONSTRAINT DF_ReconciliationRuns_Notes DEFAULT ('')
    );
    CREATE INDEX IX_ReconciliationRuns_Branch_RunAt ON dbo.ReconciliationRuns(BranchId, RunAt DESC);
END
GO
