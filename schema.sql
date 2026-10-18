-- =========================================================
-- Hospital Appointment System - MySQL Schema (v2)
-- Run this whole file in MySQL Workbench or the mysql CLI
-- to create the database and every table the API needs.
-- =========================================================
DROP DATABASE IF EXISTS hospital_appointment_system;
CREATE DATABASE hospital_appointment_system CHARACTER SET utf8mb4;
USE hospital_appointment_system;

-- ---------------------------------------------------------
-- Users: Admin, Doctor, Patient, Pharmacist, LabTechnician
-- ---------------------------------------------------------
CREATE TABLE Users (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    FullName      VARCHAR(150)  NOT NULL,
    Email         VARCHAR(150)  NOT NULL UNIQUE,
    PasswordHash  VARCHAR(255)  NOT NULL,
    Role          ENUM('Admin','Doctor','Patient','Pharmacist','LabTechnician') NOT NULL,
    Phone         VARCHAR(30)   NULL,
    IsActive      BOOLEAN       NOT NULL DEFAULT TRUE,
    CalendarToken VARCHAR(64)   NULL UNIQUE,   -- secret key for the phone reminder (calendar) feed
    CreatedAt     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ---------------------------------------------------------
-- Facilities (created before DoctorProfiles, which references it)
-- ---------------------------------------------------------
CREATE TABLE Facilities (
    Id       INT AUTO_INCREMENT PRIMARY KEY,
    Name     VARCHAR(150) NOT NULL,
    Address  VARCHAR(255) NULL,
    Phone    VARCHAR(30)  NULL
);

-- ---------------------------------------------------------
-- Role-specific profile extensions
-- ---------------------------------------------------------
CREATE TABLE DoctorProfiles (
    UserId              INT PRIMARY KEY,
    Specialization      VARCHAR(150) NOT NULL,
    LicenseNumber       VARCHAR(60)  NULL,
    Bio                 TEXT         NULL,
    ConsultationFee     DECIMAL(10,2) NOT NULL DEFAULT 5000.00,
    SlotDurationMinutes INT NOT NULL DEFAULT 30,
    FacilityId          INT NULL,
    CONSTRAINT fk_doctor_user     FOREIGN KEY (UserId)     REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT fk_doctor_facility FOREIGN KEY (FacilityId) REFERENCES Facilities(Id) ON DELETE SET NULL
);

CREATE TABLE PatientProfiles (
    UserId          INT PRIMARY KEY,
    DateOfBirth     DATE         NULL,
    Gender          ENUM('Male','Female','Other') NULL,
    Address         VARCHAR(255) NULL,
    MedicalHistory  TEXT         NULL,
    CONSTRAINT fk_patient_user FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ---------------------------------------------------------
-- Doctor weekly availability windows (DayOfWeek: 0=Sunday..6=Saturday)
-- ---------------------------------------------------------
CREATE TABLE DoctorSchedules (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    DoctorUserId INT NOT NULL,
    DayOfWeek    TINYINT NOT NULL,
    StartTime    TIME NOT NULL,
    EndTime      TIME NOT NULL,
    CONSTRAINT fk_sched_doctor FOREIGN KEY (DoctorUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ---------------------------------------------------------
-- One-off blocked dates for a doctor (holidays, half-days),
-- separate from the recurring weekly pattern above
-- ---------------------------------------------------------
CREATE TABLE DoctorTimeOffs (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    DoctorUserId INT NOT NULL,
    Date         DATE NOT NULL,
    StartTime    TIME NULL,
    EndTime      TIME NULL,
    Reason       VARCHAR(255) NULL,
    CONSTRAINT fk_timeoff_doctor FOREIGN KEY (DoctorUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ---------------------------------------------------------
-- Appointments: payment-gated booking flow
-- PendingPayment -> Pending -> Approved -> Completed
--                            \-> Rejected
--        (Patient/Admin can Cancel from PendingPayment/Pending/Approved)
-- ---------------------------------------------------------
CREATE TABLE Appointments (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    PatientUserId    INT NOT NULL,
    DoctorUserId     INT NOT NULL,
    AppointmentDate  DATE NOT NULL,
    AppointmentTime  TIME NOT NULL,
    Reason           VARCHAR(255) NOT NULL,
    DoctorNote       VARCHAR(255) NULL,
    FeeAmount        DECIMAL(10,2) NOT NULL,
    Status           ENUM('PendingPayment','Pending','Approved','Completed','Rejected','Cancelled') NOT NULL DEFAULT 'PendingPayment',
    CreatedAt        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_appt_patient FOREIGN KEY (PatientUserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT fk_appt_doctor  FOREIGN KEY (DoctorUserId)  REFERENCES Users(Id) ON DELETE RESTRICT
);

-- ---------------------------------------------------------
-- Payments: one row per appointment, created at booking time
-- ---------------------------------------------------------
CREATE TABLE Payments (
    Id             INT AUTO_INCREMENT PRIMARY KEY,
    AppointmentId  INT NOT NULL UNIQUE,
    PatientUserId  INT NOT NULL,
    Amount         DECIMAL(10,2) NOT NULL,
    Reference      VARCHAR(40) NOT NULL,
    Status         ENUM('Pending','Paid','Failed') NOT NULL DEFAULT 'Pending',
    PaymentDate    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_payment_appt    FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE CASCADE,
    CONSTRAINT fk_payment_patient FOREIGN KEY (PatientUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ---------------------------------------------------------
-- Drug formulary (a prescribing reference catalog, not stock inventory)
-- ---------------------------------------------------------
CREATE TABLE Drugs (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    Name      VARCHAR(150) NOT NULL,
    Form      VARCHAR(60)  NULL,
    Strength  VARCHAR(60)  NULL,
    IsActive  BOOLEAN NOT NULL DEFAULT TRUE
);

-- ---------------------------------------------------------
-- Prescriptions issued by doctors, dispensed by pharmacists
-- ---------------------------------------------------------
CREATE TABLE Prescriptions (
    Id                INT AUTO_INCREMENT PRIMARY KEY,
    AppointmentId     INT NULL,
    DoctorUserId      INT NOT NULL,
    PatientUserId     INT NOT NULL,
    DrugId            INT NULL,
    Medication        VARCHAR(150) NOT NULL,
    Dosage            VARCHAR(100) NOT NULL,
    Instructions      VARCHAR(255) NULL,
    Quantity          INT NULL,
    Status            ENUM('Pending','Dispensed') NOT NULL DEFAULT 'Pending',
    DispensedByUserId INT NULL,
    IssuedDate        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DispensedDate     DATETIME NULL,
    CONSTRAINT fk_presc_appt    FOREIGN KEY (AppointmentId)     REFERENCES Appointments(Id) ON DELETE SET NULL,
    CONSTRAINT fk_presc_doctor  FOREIGN KEY (DoctorUserId)      REFERENCES Users(Id) ON DELETE RESTRICT,
    CONSTRAINT fk_presc_patient FOREIGN KEY (PatientUserId)     REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT fk_presc_drug    FOREIGN KEY (DrugId)            REFERENCES Drugs(Id) ON DELETE SET NULL,
    CONSTRAINT fk_presc_pharm   FOREIGN KEY (DispensedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
);

-- ---------------------------------------------------------
-- Lab orders (ordered -> completed, with a free-text result)
-- ---------------------------------------------------------
CREATE TABLE LabOrders (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    PatientUserId    INT NOT NULL,
    DoctorUserId     INT NOT NULL,
    TestName         VARCHAR(150) NOT NULL,
    Notes            VARCHAR(255) NULL,
    Status           ENUM('Ordered','Completed') NOT NULL DEFAULT 'Ordered',
    ResultText       TEXT NULL,
    CompletedByUserId INT NULL,
    RequestedDate    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CompletedAt      DATETIME NULL,
    CONSTRAINT fk_lab_patient   FOREIGN KEY (PatientUserId)     REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT fk_lab_doctor    FOREIGN KEY (DoctorUserId)      REFERENCES Users(Id) ON DELETE RESTRICT,
    CONSTRAINT fk_lab_completer FOREIGN KEY (CompletedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
);

-- ---------------------------------------------------------
-- Patient medical record entries (doctor's visit notes)
-- ---------------------------------------------------------
CREATE TABLE PatientRecords (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    PatientUserId INT NOT NULL,
    DoctorUserId  INT NOT NULL,
    Notes         TEXT NOT NULL,
    RecordDate    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_record_patient FOREIGN KEY (PatientUserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT fk_record_doctor  FOREIGN KEY (DoctorUserId)  REFERENCES Users(Id) ON DELETE RESTRICT
);

-- ---------------------------------------------------------
-- Complaints: patients submit, admins respond
-- ---------------------------------------------------------
CREATE TABLE Complaints (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    PatientUserId INT NOT NULL,
    Subject       VARCHAR(150) NOT NULL,
    Message       TEXT NOT NULL,
    Status        ENUM('Open','InReview','Resolved') NOT NULL DEFAULT 'Open',
    AdminResponse TEXT NULL,
    CreatedAt     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_complaint_patient FOREIGN KEY (PatientUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- =========================================================
-- No manual seed data needed - after running the backend,
-- call POST /api/auth/seed-demo once to create 5 demo
-- accounts (one per role), a demo facility, and a Mon-Fri
-- 9am-4pm availability window for the demo doctor.
-- =========================================================
