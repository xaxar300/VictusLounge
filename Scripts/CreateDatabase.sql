IF DB_ID(N'victus_lounge') IS NULL
BEGIN
    CREATE DATABASE victus_lounge;
END;
GO

USE victus_lounge;
GO

-- The application creates tables and seed data through Entity Framework Core.
-- Run this script only when SQL Server does not have the database yet.
