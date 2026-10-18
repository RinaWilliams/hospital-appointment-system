-- Run this ONLY if you already created the database with an older schema.sql
-- (it adds the phone-reminder column without wiping your data).
USE hospital_appointment_system;
ALTER TABLE Users ADD COLUMN CalendarToken VARCHAR(64) NULL UNIQUE;
