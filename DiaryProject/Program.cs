using DiaryProject.Data;
using DiaryProject.Models;
using DiaryProject.Services;
using DiaryProject.Services.Review;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC + API Controllers
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<DiarySystemDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

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

app.UseCors("ReactFront");

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Review}/{action=Time}/{id?}")
    .WithStaticAssets();

app.MapControllers();

app.Run();
