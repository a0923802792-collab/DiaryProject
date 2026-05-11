namespace DiaryProject.Models.Front
{
    public class User
    {
        public int    UserId { get; set; } // Primary Key -> 不使用 E-mail，主鍵傳來傳去會爆炸。
        public string Email    { get; set; } = string.Empty; // 註冊時填寫的 E-mail
        public string Password { get; set; } // 註冊填的密碼
        public string Phone    { get; set; }
        public string Nickname { get; set; }
        // Models/User.cs
        // User.cs
        public DateTime? birthday { get; set; }
        public string? ResetCode { get; set; }             // 驗證碼
        public DateTime? ResetCodeExpiration { get; set; } // 驗證碼保留時間
        public bool IsNotificationEnabled { get; set; } = true; // 預設開啟
        public string Theme { get; set; } = "Beige";            // 主題
        public bool IsDeleted { get; set; } = false;            // 帳號狀態 -> 未刪除
        public DateTime? DeletedAt { get; set; }                // 刪除時間
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }

    // 專門給註冊用的 DTO
    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string? Phone { get; set; }
        public string? Nickname { get; set; }
        public string? Birthday { get; set; }  // 接收字串
    }
}
