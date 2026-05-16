using DiaryProject.Data;
using DiaryProject.Models;
using DiaryProject.Services;
using DiaryProject.Services.Review;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC + API Controllers
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// 2026-05-16
// 提高表單大小限制：DiaryEdit 把圖片以 base64 塞進單一 hidden input (mediaItemsJson)，
// 預設的 FormOptions.ValueLengthLimit 只有 4 MB（字元數），多張或單張較大圖片就會超過，
// 導致請求在 model binding 之前就被拒絕，使用者會看到「頁面跳掉」。
// 這裡把單欄與整體上限拉到 50 MB，配合 Kestrel 的 MaxRequestBodySize 一起放寬。
// 注意：之後若改成 multipart 上傳 API（建議方向），這裡可以再調回較低值。
const long MaxUploadBytes = 50L * 1024 * 1024; // 50 MB

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = (int)MaxUploadBytes;        // 單一表單欄位字元上限
    options.MultipartBodyLengthLimit = MaxUploadBytes;     // multipart 整體 body 上限
    options.MultipartHeadersLengthLimit = 32 * 1024;       // multipart header 上限
    options.KeyLengthLimit = 4 * 1024;                     // 欄位名稱長度上限
    options.ValueCountLimit = 4 * 1024;                    // 表單欄位數上限（避免被嫌少）
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxUploadBytes;    // Kestrel 整體請求 body 上限
});

// Project services
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// CORS for React dev frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFront", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// EF Core DbContext
builder.Services.AddDbContext<DiarySystemDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(120)));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(120)));

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseCors("ReactFront");

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Entry}/{action=Welcome}/{id?}")
    .WithStaticAssets();

app.MapControllers();

app.Run();