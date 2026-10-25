/*
  Employee Training Management System - database creation script
  --------------------------------------------------------------
  Generated from the EF Core migrations (dotnet ef migrations script --idempotent).
  Run it in SSMS, Azure Data Studio or sqlcmd. It is safe to run more than once: it creates the
  database if missing, then applies only the migrations that have not been applied yet.

  You do NOT need this script to run the API - the API applies the same migrations by itself on start-up
  (Database:MigrateOnStartup). It is provided as the "database creation script" deliverable.
*/
IF DB_ID(N'TrainingManagementDb') IS NULL
    CREATE DATABASE [TrainingManagementDb];
GO

USE [TrainingManagementDb];
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025184029_InitialCreate'
)
BEGIN
    CREATE TABLE [Employee] (
        [EmployeeId] int NOT NULL IDENTITY,
        [EmployeeName] nvarchar(100) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [Department] nvarchar(100) NOT NULL,
        [JoiningDate] datetime2 NOT NULL,
        [Experience] int NOT NULL,
        CONSTRAINT [PK_Employee] PRIMARY KEY ([EmployeeId]),
        CONSTRAINT [CK_Employee_Experience_NonNegative] CHECK ([Experience] >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025184029_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Employee_Email] ON [Employee] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025184029_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025184029_InitialCreate', N'8.0.33');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025185003_AddCourse'
)
BEGIN
    CREATE TABLE [Course] (
        [CourseId] int NOT NULL IDENTITY,
        [CourseName] nvarchar(150) NOT NULL,
        [Duration] int NOT NULL,
        [TrainerName] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Course] PRIMARY KEY ([CourseId]),
        CONSTRAINT [CK_Course_Duration_Positive] CHECK ([Duration] > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025185003_AddCourse'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025185003_AddCourse', N'8.0.33');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025185150_AddTrainingAssignment'
)
BEGIN
    CREATE TABLE [TrainingAssignment] (
        [AssignmentId] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [CourseId] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_TrainingAssignment] PRIMARY KEY ([AssignmentId]),
        CONSTRAINT [CK_TrainingAssignment_EndDate_OnOrAfterStartDate] CHECK ([EndDate] >= [StartDate]),
        CONSTRAINT [CK_TrainingAssignment_Status_Valid] CHECK ([Status] IN ('Assigned', 'In Progress', 'Completed')),
        CONSTRAINT [FK_TrainingAssignment_Course_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Course] ([CourseId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TrainingAssignment_Employee_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employee] ([EmployeeId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025185150_AddTrainingAssignment'
)
BEGIN
    CREATE INDEX [IX_TrainingAssignment_CourseId] ON [TrainingAssignment] ([CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025185150_AddTrainingAssignment'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TrainingAssignment_Employee_Course] ON [TrainingAssignment] ([EmployeeId], [CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025185150_AddTrainingAssignment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025185150_AddTrainingAssignment', N'8.0.33');
END;
GO

COMMIT;
GO

