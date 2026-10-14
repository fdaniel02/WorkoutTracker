-- Creates a dedicated SQL login for WorkoutTracker with rights only to its own database.
-- Run as sysadmin (e.g. sa) once. Replace <STRONG_PASSWORD> before running, and don't save it back to this file.
--
-- Roles:
--   db_datareader / db_datawriter  - read and write workout data
--   db_ddladmin                    - the app applies EF Core migrations at startup

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'workouttracker')
    CREATE LOGIN [workouttracker]
        WITH PASSWORD = N'<STRONG_PASSWORD>',
             DEFAULT_DATABASE = [WorkoutTracker],
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF;
GO

USE [WorkoutTracker];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'workouttracker')
    CREATE USER [workouttracker] FOR LOGIN [workouttracker];
GO

ALTER ROLE [db_datareader] ADD MEMBER [workouttracker];
ALTER ROLE [db_datawriter] ADD MEMBER [workouttracker];
ALTER ROLE [db_ddladmin]   ADD MEMBER [workouttracker];
GO
