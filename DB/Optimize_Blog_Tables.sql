-- ========================================================
-- Optimize Blog Tables: Indexes, Stats & YouTube EmbedId
-- Idempotent - safe to re-run
-- ========================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Add EmbedVideoId computed column to cache extracted YouTube ID
--    Extracts ID from patterns like:
--      https://www.youtube.com/embed/VIDEO_ID
--      https://youtu.be/VIDEO_ID
--      https://www.youtube.com/watch?v=VIDEO_ID
IF COL_LENGTH('dbo.BlogPosts', 'EmbedVideoId') IS NULL
BEGIN
    ALTER TABLE dbo.BlogPosts
        ADD EmbedVideoId AS (
            CASE
                WHEN VideoUrl LIKE '%youtube.com/embed/%'
                    THEN SUBSTRING(
                            VideoUrl,
                            CHARINDEX('/embed/', VideoUrl) + 7,
                            CASE
                                WHEN CHARINDEX('?', VideoUrl, CHARINDEX('/embed/', VideoUrl) + 7) > 0
                                    THEN CHARINDEX('?', VideoUrl, CHARINDEX('/embed/', VideoUrl) + 7)
                                         - (CHARINDEX('/embed/', VideoUrl) + 7)
                                ELSE LEN(VideoUrl)
                            END
                        )
                WHEN VideoUrl LIKE '%youtu.be/%'
                    THEN SUBSTRING(
                            VideoUrl,
                            CHARINDEX('youtu.be/', VideoUrl) + 9,
                            CASE
                                WHEN CHARINDEX('?', VideoUrl, CHARINDEX('youtu.be/', VideoUrl) + 9) > 0
                                    THEN CHARINDEX('?', VideoUrl, CHARINDEX('youtu.be/', VideoUrl) + 9)
                                         - (CHARINDEX('youtu.be/', VideoUrl) + 9)
                                ELSE LEN(VideoUrl)
                            END
                        )
                WHEN VideoUrl LIKE '%youtube.com/watch?v=%'
                    THEN SUBSTRING(
                            VideoUrl,
                            CHARINDEX('?v=', VideoUrl) + 3,
                            CASE
                                WHEN CHARINDEX('&', VideoUrl, CHARINDEX('?v=', VideoUrl) + 3) > 0
                                    THEN CHARINDEX('&', VideoUrl, CHARINDEX('?v=', VideoUrl) + 3)
                                         - (CHARINDEX('?v=', VideoUrl) + 3)
                                ELSE LEN(VideoUrl)
                            END
                        )
                ELSE NULL
            END
        ) PERSISTED;

    PRINT 'Added EmbedVideoId computed column to BlogPosts.';
END
ELSE
    PRINT 'EmbedVideoId column already exists - skipped.';
GO

-- 2. Add ReadTimeMinutes computed column (avg 250 words/min)
IF COL_LENGTH('dbo.BlogPosts', 'ReadTimeMinutes') IS NULL
BEGIN
    -- Word count approximation: count spaces in content / 250
    -- We store as a persisted column for performance
    ALTER TABLE dbo.BlogPosts
        ADD ReadTimeMinutes AS (
            CASE 
                WHEN Content IS NULL OR LEN(Content) < 250 THEN 1
                ELSE (LEN(Content) - LEN(REPLACE(Content, ' ', ''))) / 250 + 1
            END
        ) PERSISTED;

    PRINT 'Added ReadTimeMinutes computed column to BlogPosts.';
END
ELSE
    PRINT 'ReadTimeMinutes column already exists - skipped.';
GO

-- 3. Add missing indexes for performance
-- Index: Filter by IsFeatured for home page queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BlogPosts_IsFeatured' AND object_id = OBJECT_ID('dbo.BlogPosts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_BlogPosts_IsFeatured
        ON dbo.BlogPosts (IsFeatured, Status, PublishedAt DESC)
        INCLUDE (Title, Slug, ThumbnailUrl, CategoryId, AuthorUserId, ViewCount, Summary)
        WHERE IsFeatured = 1;
    PRINT 'Created IX_BlogPosts_IsFeatured index.';
END
ELSE
    PRINT 'IX_BlogPosts_IsFeatured already exists - skipped.';
GO

-- Index: Full-text search helper on Title + Summary
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BlogPosts_Title_Summary' AND object_id = OBJECT_ID('dbo.BlogPosts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_BlogPosts_Title_Summary
        ON dbo.BlogPosts (Status, PublishedAt DESC)
        INCLUDE (Title, Slug, ThumbnailUrl, CategoryId, AuthorUserId, ViewCount, Summary, IsFeatured);
    PRINT 'Created IX_BlogPosts_Title_Summary index.';
END
ELSE
    PRINT 'IX_BlogPosts_Title_Summary already exists - skipped.';
GO

-- Index: ViewCount for trending queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BlogPosts_ViewCount' AND object_id = OBJECT_ID('dbo.BlogPosts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_BlogPosts_ViewCount
        ON dbo.BlogPosts (Status, ViewCount DESC)
        INCLUDE (Title, Slug, ThumbnailUrl, PublishedAt, CategoryId);
    PRINT 'Created IX_BlogPosts_ViewCount index.';
END
ELSE
    PRINT 'IX_BlogPosts_ViewCount already exists - skipped.';
GO

-- 4. Create View: vw_PublishedBlogPosts - flattened view for FE queries
IF OBJECT_ID(N'dbo.vw_PublishedBlogPosts', N'V') IS NOT NULL
    DROP VIEW dbo.vw_PublishedBlogPosts;
GO

CREATE VIEW dbo.vw_PublishedBlogPosts AS
SELECT
    bp.BlogPostId,
    bp.Title,
    bp.Slug,
    bp.Summary,
    bp.ThumbnailUrl,
    bp.VideoUrl,
    bp.EmbedVideoId,
    bp.ReadTimeMinutes,
    bp.IsFeatured,
    bp.ViewCount,
    bp.PublishedAt,
    bp.CreatedAt,
    bp.UpdatedAt,
    bc.CategoryId,
    bc.Name      AS CategoryName,
    bc.Slug      AS CategorySlug,
    ua.UserId    AS AuthorUserId,
    COALESCE(sp.FullName, ua.Email) AS AuthorName,
    ua.AvatarUrl AS AuthorAvatarUrl,
    dp.Qualification,
    dp.YearsOfExperience
FROM dbo.BlogPosts bp
INNER JOIN dbo.BlogCategories bc ON bc.CategoryId = bp.CategoryId
INNER JOIN dbo.UserAccounts   ua ON ua.UserId     = bp.AuthorUserId
LEFT  JOIN dbo.StaffProfiles  sp ON sp.UserId     = bp.AuthorUserId
LEFT  JOIN dbo.DentistProfiles dp ON dp.UserId    = bp.AuthorUserId
WHERE bp.Status = 'Published';
GO

-- 5. Create stored proc for incrementing ViewCount (atomic, no race condition)
IF OBJECT_ID(N'dbo.sp_IncrementBlogPostView', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_IncrementBlogPostView;
GO

CREATE PROCEDURE dbo.sp_IncrementBlogPostView
    @Slug NVARCHAR(250)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BlogPosts
    SET    ViewCount = ViewCount + 1
    WHERE  Slug = @Slug
      AND  Status = 'Published';
END;
GO

PRINT 'Blog optimization script completed successfully.';
GO
