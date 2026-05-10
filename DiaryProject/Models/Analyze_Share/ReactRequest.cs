namespace DiaryProject.Models;

// =====================================================================
// POST /api/sharewall4/react 的請求 Body
//
// VisitorId 設計說明：
//   現在：前端 identity.js 產生的匠名 UUID（"anon-xxxx"）
//   未來：登入後將改為真實 userId（"user-123"）
//   後端 DB 層用此欄位實施 UNIQUE 防重複，與前端使用了哪種儲存方式無關
// =====================================================================
public sealed record ReactRequest(long DiaryId, string ReactionType, string? VisitorId);
