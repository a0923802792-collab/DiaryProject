using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using DiaryProject.Data;
using DiaryProject.Models.Front;

namespace DiaryProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public UserController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }
        [HttpGet("me")]
        public IActionResult Me()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            var user = _db.Users.FirstOrDefault(u => u.UserId == userId.Value && !u.IsDeleted);
            if (user == null)
            {
                return NotFound(new { message = "找不到使用者" });
            }

            return Ok(new
            {
                id = user.UserId,
                email = user.Email,
                nickname = user.Nickname,
                birthday = user.birthday,
                phone = user.Phone,
                theme = user.Theme,
                isNotificationEnabled = user.IsNotificationEnabled
            });
        }
        /* ===== Block 1: 註冊 ===== */
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and Password are required.");

            bool emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email);
            if (emailExists)
                return Conflict("此 Email 已被註冊！");

            var newUser = new User
            {
                Email = request.Email.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? "未填寫" : request.Phone,
                Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? "未填寫" : request.Nickname,
                birthday = DateTime.TryParse(request.Birthday, out var parsedDate) ? parsedDate : null,
                IsNotificationEnabled = true,
                Theme = "Beige",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "註冊成功！",
                userId = newUser.UserId,
                email = newUser.Email,
                buildTime = newUser.CreatedAt
            });
        }

        /* ===== Block 2: 登入 ===== */
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == request.Email);

            if (user == null || user.IsDeleted)
            {
                return Unauthorized("信箱或密碼錯誤！");
            }

            bool passwordOk;
            try
            {
                passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
            }
            catch
            {
                return Unauthorized("信箱或密碼錯誤！");
            }

            if (!passwordOk)
            {
                return Unauthorized("信箱或密碼錯誤！");
            }

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Nickname ?? "");
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");

            return Ok(new
            {
                message = "登入成功！",
                user = new
                {
                    id = user.UserId,
                    email = user.Email,
                    nickname = user.Nickname,
                    birthday = user.birthday,
                    phone = user.Phone,
                    theme = user.Theme,
                    isNotificationEnabled = user.IsNotificationEnabled
                }
            });
        }
        /* ===== Block 2: 登出 ===== */
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "已登出" });
        }
        /* ===== Block 3: 更新個人資料 ===== */
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("找不到該用戶！");
            }

            user.Nickname = request.Nickname;
            user.Email = request.Email;
            user.Phone = request.Phone;
            user.birthday = DateTime.TryParse(request.Birthday, out var parsedDate) ? parsedDate : null;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "資料更新成功！",
                user = new
                {
                    id = user.UserId,
                    email = user.Email,
                    nickname = user.Nickname,
                    phone = user.Phone,
                    birthday = user.birthday
                }
            });
        }

        /* ===== Block 4: 驗證舊密碼 ===== */
        [HttpPost("verify-password")]
        public IActionResult VerifyPassword([FromBody] VerifyPasswordRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.UserId == request.UserId);
            if (user == null)
            {
                return NotFound("找不到該用戶");
            }

            bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
            if (!isPasswordCorrect)
            {
                return Unauthorized("舊密碼錯誤");
            }

            return Ok(new { message = "驗證成功，允許修改密碼" });
        }

        /* ===== Block 5: 修改密碼 ===== */
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.UserId == request.UserId);
            if (user == null)
            {
                return NotFound("找不到該用戶");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
            {
                return Unauthorized("舊密碼驗證失敗，無法修改");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "密碼修改成功" });
        }

        /* ===== Block 6: 忘記密碼 - 寄送驗證信 ===== */
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null)
            {
                return NotFound("找不到此信箱註冊的帳號");
            }

            Random rnd = new Random();
            string code = rnd.Next(1000, 9999).ToString();

            user.ResetCode = code;
            user.ResetCodeExpiration = DateTime.UtcNow.AddMinutes(10);
            await _db.SaveChangesAsync();

            try
            {
                // 2026-05-17
                string systemEmail = _config["EmailSettings:SystemEmail"];
                string systemAppPassword = _config["EmailSettings:AppPassword"];

                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(systemEmail, "Moody 行動中心");
                mail.To.Add(request.Email);
                mail.Subject = "Moody 密碼重設驗證碼";
                mail.Body = $@"<h3>您好，{user.Nickname}：</h3>
                               <p>您的密碼重設驗證碼為：
                               <strong style='font-size:24px;color:#A1A34E;'>{code}</strong></p>
                               <p>請在 10 分鐘內輸入此驗證碼。若非本人操作，請忽略此信件。</p>";
                mail.IsBodyHtml = true;

                using (SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.Credentials = new NetworkCredential(systemEmail, systemAppPassword);
                    smtp.EnableSsl = true;
                    await smtp.SendMailAsync(mail);
                }

                return Ok(new { message = "驗證碼已寄出，請至信箱收取" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("寄信失敗: " + ex.Message);
                return StatusCode(500, "寄信系統發生錯誤，請稍後再試");
            }
        }

        /* ===== Block 7: 驗證重設碼 ===== */
        [HttpPost("verify-reset-code")]
        public IActionResult VerifyResetCode([FromBody] VerifyCodeRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("找不到此用戶");

            if (user.ResetCode != request.Code)
            {
                return BadRequest("驗證碼錯誤");
            }

            if (!user.ResetCodeExpiration.HasValue || DateTime.UtcNow > user.ResetCodeExpiration.Value)
            {
                return BadRequest("驗證碼已過期，請重新發送");
            }

            return Ok(new { message = "驗證成功，允許重設密碼" });
        }

        /* ===== Block 8: 重設密碼 ===== */
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("找不到此用戶");

            if (user.ResetCode != request.Code ||
                !user.ResetCodeExpiration.HasValue ||
                DateTime.UtcNow > user.ResetCodeExpiration.Value)
            {
                return BadRequest("驗證無效或已過期");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.ResetCode = null;
            user.ResetCodeExpiration = null;

            await _db.SaveChangesAsync();
            return Ok(new { message = "密碼重設成功，請使用新密碼登入" });
        }

        /* ===== Block 9: 更新系統設定 ===== */
        [HttpPut("update-settings/{userId}")]
        public async Task<IActionResult> UpdateSettings(int userId, [FromBody] UpdateSettingsRequest request)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound("找不到該用戶！");

            user.IsNotificationEnabled = request.IsNotificationEnabled;
            user.Theme = request.Theme;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "設定已自動儲存",
                user = new
                {
                    id = user.UserId,
                    isNotificationEnabled = user.IsNotificationEnabled,
                    theme = user.Theme
                }
            });
        }

        /* ===== Block 10: 刪除帳戶（隱藏狀態） ===== */
        [HttpPut("delete/{userId}")]
        public async Task<IActionResult> DeleteAccount(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null || user.IsDeleted)
            {
                return NotFound("找不到該用戶！");
            }

            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "帳戶已刪除（隱藏狀態）" });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class UpdateUserRequest
    {
        public string Nickname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Birthday { get; set; }
    }

    public class VerifyPasswordRequest
    {
        public int UserId { get; set; }
        public string Password { get; set; } = "";
    }

    public class ChangePasswordRequest
    {
        public int UserId { get; set; }
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = "";
    }

    public class VerifyCodeRequest
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class UpdateSettingsRequest
    {
        public bool IsNotificationEnabled { get; set; }
        public string Theme { get; set; } = "";
    }
}