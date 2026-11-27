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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Activities] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(30) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Status] nvarchar(30) NOT NULL,
        [ScheduledAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [ReminderAt] datetimeoffset NULL,
        [CustomerId] uniqueidentifier NULL,
        [LeadId] uniqueidentifier NULL,
        [OpportunityId] uniqueidentifier NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Activities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Activities_TargetRequired] CHECK (([CustomerId] IS NOT NULL OR [LeadId] IS NOT NULL OR [OpportunityId] IS NOT NULL))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NULL,
        [OldValuesJson] nvarchar(max) NULL,
        [NewValuesJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(500) NULL,
        [IsSuccess] bit NOT NULL,
        [Detail] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TaxCode] nvarchar(30) NULL,
        [Industry] nvarchar(100) NULL,
        [Website] nvarchar(200) NULL,
        [Phone] nvarchar(20) NULL,
        [Email] nvarchar(200) NULL,
        [Address] nvarchar(300) NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(200) NULL,
        [Phone] nvarchar(20) NULL,
        [JobTitle] nvarchar(100) NULL,
        [Address] nvarchar(300) NULL,
        [Status] nvarchar(30) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CompanyId] uniqueidentifier NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Customers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Leads] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(200) NULL,
        [Phone] nvarchar(20) NULL,
        [CompanyName] nvarchar(200) NULL,
        [Source] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [EstimatedValue] decimal(18,2) NULL,
        [Notes] nvarchar(2000) NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NULL,
        [ConvertedCustomerId] uniqueidentifier NULL,
        [ConvertedCompanyId] uniqueidentifier NULL,
        [ConvertedAt] datetimeoffset NULL,
        [ConvertedBy] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Leads_Companies_ConvertedCompanyId] FOREIGN KEY ([ConvertedCompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Leads_Customers_ConvertedCustomerId] FOREIGN KEY ([ConvertedCustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Opportunities] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [CompanyId] uniqueidentifier NULL,
        [SourceLeadId] uniqueidentifier NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NULL,
        [Value] decimal(18,2) NOT NULL,
        [Stage] nvarchar(30) NOT NULL,
        [Probability] int NOT NULL,
        [ExpectedCloseDate] datetimeoffset NOT NULL,
        [ActualCloseDate] datetimeoffset NULL,
        [LossReason] nvarchar(500) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetimeoffset NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Opportunities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Opportunities_Probability] CHECK ([Probability] >= 0 AND [Probability] <= 100),
        CONSTRAINT [CK_Opportunities_Value] CHECK ([Value] >= 0),
        CONSTRAINT [FK_Opportunities_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Opportunities_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Opportunities_Leads_SourceLeadId] FOREIGN KEY ([SourceLeadId]) REFERENCES [Leads] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedByIp] nvarchar(64) NULL,
        [RevokedAt] datetimeoffset NULL,
        [RevokedReason] nvarchar(250) NULL,
        [ReplacedByTokenHash] nvarchar(128) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [ManagerUserId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [UserName] nvarchar(50) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [TeamId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [IsLocked] bit NOT NULL,
        [LockoutEndAt] datetimeoffset NULL,
        [FailedLoginCount] int NOT NULL,
        [LastLoginAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [AssignedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_CustomerId] ON [Activities] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_LeadId] ON [Activities] ([LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_OpportunityId] ON [Activities] ([OpportunityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_OwnerId_TeamId_Status_ScheduledAt] ON [Activities] ([OwnerId], [TeamId], [Status], [ScheduledAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Activities_TeamId] ON [Activities] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName_EntityId_CreatedAt] ON [AuditLogs] ([EntityName], [EntityId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAt] ON [AuditLogs] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_OwnerId_TeamId_IsDeleted] ON [Companies] ([OwnerId], [TeamId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Companies_TaxCode] ON [Companies] ([TaxCode]) WHERE [TaxCode] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_TeamId] ON [Companies] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_CompanyId] ON [Customers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_Email] ON [Customers] ([Email]) WHERE [Email] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_OwnerId_TeamId_IsDeleted] ON [Customers] ([OwnerId], [TeamId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_Phone] ON [Customers] ([Phone]) WHERE [Phone] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_TeamId] ON [Customers] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_ConvertedCompanyId] ON [Leads] ([ConvertedCompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_ConvertedCustomerId] ON [Leads] ([ConvertedCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_Email] ON [Leads] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_OwnerId_TeamId_Status_IsDeleted] ON [Leads] ([OwnerId], [TeamId], [Status], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_Phone] ON [Leads] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_TeamId] ON [Leads] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Opportunities_CompanyId] ON [Opportunities] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Opportunities_CustomerId] ON [Opportunities] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Opportunities_OwnerId_TeamId_Stage_IsDeleted] ON [Opportunities] ([OwnerId], [TeamId], [Stage], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Opportunities_SourceLeadId] ON [Opportunities] ([SourceLeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Opportunities_Stage_ExpectedCloseDate] ON [Opportunities] ([Stage], [ExpectedCloseDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Opportunities_TeamId] ON [Opportunities] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId_RevokedAt] ON [RefreshTokens] ([UserId], [RevokedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teams_Code] ON [Teams] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teams_ManagerUserId] ON [Teams] ([ManagerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_TeamId] ON [Users] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Opportunities_OpportunityId] FOREIGN KEY ([OpportunityId]) REFERENCES [Opportunities] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Companies] ADD CONSTRAINT [FK_Companies_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Companies] ADD CONSTRAINT [FK_Companies_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Leads] ADD CONSTRAINT [FK_Leads_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Leads] ADD CONSTRAINT [FK_Leads_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Opportunities] ADD CONSTRAINT [FK_Opportunities_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Opportunities] ADD CONSTRAINT [FK_Opportunities_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    ALTER TABLE [Teams] ADD CONSTRAINT [FK_Teams_Users_ManagerUserId] FOREIGN KEY ([ManagerUserId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261127032312_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127032312_InitialCreate', N'10.0.15');
END;

COMMIT;
GO

