-- =====================================================================
-- ShareWall 反應功能 DB 建表腳本
--
-- 執行方式：在 SQL Server Management Studio 或 Azure Data Studio 中
--   對 MoodDiary 資料庫執行此腳本
--
-- 包含：
--   1. PostReactionCount  — 各反應類型的計數快取
--   2. PostReactionLog    — 每位使用者的反應記錄（一人一篇只有一種反應）
--
-- 唯一鍵設計說明：
--   PostReactionLog 的 UNIQUE 約束設在 (DiaryId, VisitorId)，
--   確保同一個人對同一篇文只能有一種反應類型。
--   切換反應時 UPDATE 該筆記錄，而非新增。
-- =====================================================================

USE MoodDiary;
GO

-- ── 1. PostReactionCount ────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE name = 'PostReactionCount' AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.PostReactionCount (
        DiaryId      BIGINT       NOT NULL,
        ReactionType NVARCHAR(20) NOT NULL,
        Count        INT          NOT NULL CONSTRAINT DF_PostReactionCount_Count DEFAULT 0,
        UpdatedAt    DATETIME2    NOT NULL CONSTRAINT DF_PostReactionCount_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PostReactionCount PRIMARY KEY (DiaryId, ReactionType)
    );
    PRINT 'Created PostReactionCount';
END
ELSE
    PRINT 'PostReactionCount already exists, skipped.';
GO

-- ── 2. PostReactionLog ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE name = 'PostReactionLog' AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.PostReactionLog (
        LogId        BIGINT IDENTITY(1,1) NOT NULL,
        DiaryId      BIGINT        NOT NULL,
        VisitorId    NVARCHAR(200) NOT NULL,
        ReactionType NVARCHAR(20)  NOT NULL,
        CreatedAt    DATETIME2     NOT NULL CONSTRAINT DF_PostReactionLog_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt    DATETIME2     NOT NULL CONSTRAINT DF_PostReactionLog_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PostReactionLog PRIMARY KEY (LogId),
        -- 一人一篇只能有一種反應
        CONSTRAINT UQ_PostReactionLog_DiaryId_VisitorId UNIQUE (DiaryId, VisitorId)
    );
    PRINT 'Created PostReactionLog';
END
ELSE
BEGIN
    -- 資料表已存在：確認 UNIQUE 約束正確（一人一反應，不含 ReactionType）
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UQ_PostReactionLog_DiaryId_ReactionType_VisitorId'
          AND object_id = OBJECT_ID('dbo.PostReactionLog')
    )
    BEGIN
        ALTER TABLE dbo.PostReactionLog
        DROP CONSTRAINT UQ_PostReactionLog_DiaryId_ReactionType_VisitorId;
        PRINT 'Dropped old 3-column UNIQUE constraint';
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UQ_PostReactionLog_DiaryId_VisitorId'
          AND object_id = OBJECT_ID('dbo.PostReactionLog')
    )
    BEGIN
        -- 清除舊的重複資料（同一人同一篇有多筆，只保留最新）
        WITH CTE AS (
            SELECT LogId,
                   ROW_NUMBER() OVER (PARTITION BY DiaryId, VisitorId ORDER BY UpdatedAt DESC) AS rn
            FROM dbo.PostReactionLog
        )
        DELETE FROM CTE WHERE rn > 1;

        ALTER TABLE dbo.PostReactionLog
        ADD CONSTRAINT UQ_PostReactionLog_DiaryId_VisitorId UNIQUE (DiaryId, VisitorId);
        PRINT 'Added new (DiaryId, VisitorId) UNIQUE constraint';
    END
    ELSE
        PRINT 'PostReactionLog UNIQUE constraint already correct, skipped.';
END
GO

PRINT 'Setup complete.';
GO
