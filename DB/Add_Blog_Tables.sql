-- ========================================================
-- Task 6: Dental Blog & Health Articles Module Tables
-- Target: SQL Server / Azure SQL / LocalDB
-- Description: Idempotent script for BlogCategories & BlogPosts
-- ========================================================

IF DB_ID(N'DentalClinicManagementDB') IS NOT NULL
BEGIN
    USE DentalClinicManagementDB;
END;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. BlogCategories Table
IF OBJECT_ID(N'dbo.BlogCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BlogCategories (
        CategoryId INT IDENTITY(1,1) CONSTRAINT PK_BlogCategories PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Slug VARCHAR(150) NOT NULL CONSTRAINT UQ_BlogCategories_Slug UNIQUE,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BlogCategories_CreatedAt DEFAULT SYSDATETIME()
    );
END;
GO

-- 2. BlogPosts Table
IF OBJECT_ID(N'dbo.BlogPosts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BlogPosts (
        BlogPostId BIGINT IDENTITY(1,1) CONSTRAINT PK_BlogPosts PRIMARY KEY,
        CategoryId INT NOT NULL,
        AuthorUserId BIGINT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Slug VARCHAR(250) NOT NULL CONSTRAINT UQ_BlogPosts_Slug UNIQUE,
        Summary NVARCHAR(1000) NULL,
        Content NVARCHAR(MAX) NOT NULL,
        ThumbnailUrl NVARCHAR(500) NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_BlogPosts_Status DEFAULT 'Draft',
        IsFeatured BIT NOT NULL CONSTRAINT DF_BlogPosts_IsFeatured DEFAULT 0,
        ViewCount INT NOT NULL CONSTRAINT DF_BlogPosts_ViewCount DEFAULT 0,
        PublishedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BlogPosts_CreatedAt DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_BlogPosts_BlogCategories FOREIGN KEY (CategoryId) REFERENCES dbo.BlogCategories (CategoryId),
        CONSTRAINT FK_BlogPosts_UserAccounts FOREIGN KEY (AuthorUserId) REFERENCES dbo.UserAccounts (UserId) ON DELETE CASCADE,
        CONSTRAINT CK_BlogPosts_Status CHECK (Status IN ('Draft', 'PendingReview', 'Published', 'Archived'))
    );

    CREATE NONCLUSTERED INDEX IX_BlogPosts_CategoryId ON dbo.BlogPosts(CategoryId);
    CREATE NONCLUSTERED INDEX IX_BlogPosts_AuthorUserId ON dbo.BlogPosts(AuthorUserId);
    CREATE NONCLUSTERED INDEX IX_BlogPosts_Status_PublishedAt ON dbo.BlogPosts(Status, PublishedAt DESC);
END;
GO

-- 3. Seed Default BlogCategories if none exist
IF NOT EXISTS (SELECT 1 FROM dbo.BlogCategories)
BEGIN
    INSERT INTO dbo.BlogCategories (Name, Slug, CreatedAt)
    VALUES 
        (N'Niềng răng - Chỉnh nha', 'nieng-rang-chinh-nha', SYSDATETIME()),
        (N'Cấy ghép Implant', 'cay-ghep-implant', SYSDATETIME()),
        (N'Nha khoa thẩm mỹ', 'nha-khoa-tham-my', SYSDATETIME()),
        (N'Nha khoa trẻ em', 'nha-khoa-tre-em', SYSDATETIME()),
        (N'Chăm sóc răng miệng', 'cham-soc-rang-mieng', SYSDATETIME()),
        (N'Bệnh lý răng miệng', 'benh-ly-rang-mieng', SYSDATETIME());
END;
GO
