using DiaryProject.Models.Front;
using DiaryProject.Models.Task;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TaskScheduleRule> TaskScheduleRules { get; set; }
        public DbSet<TaskChecking> TaskChecking { get; set; }

        // Front：使用者 / 通知
        public DbSet<User> Users { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // task
            // =========================
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("task");
                entity.HasKey(e => e.TaskId);

                entity.Property(e => e.TaskId).HasColumnName("task_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(100);
                entity.Property(e => e.RhythmType).HasColumnName("rhythm_type").HasMaxLength(20);
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<TaskScheduleRule>(entity =>
            {
                entity.ToTable("task_schedule_rule");
                entity.HasKey(e => e.RuleId);

                entity.Property(e => e.RuleId).HasColumnName("rule_id");
                entity.Property(e => e.TaskId).HasColumnName("task_id");
                entity.Property(e => e.WeeklyTargetCount).HasColumnName("weekly_target_count");
                entity.Property(e => e.StartDate).HasColumnName("start_date");
                entity.Property(e => e.EndDate).HasColumnName("end_date");

                entity.HasIndex(e => e.TaskId).IsUnique();

                entity.HasOne<TaskItem>()
                    .WithOne()
                    .HasForeignKey<TaskScheduleRule>(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaskChecking>(entity =>
            {
                entity.ToTable("task_checkin_log");
                entity.HasKey(e => e.CheckingId);

                entity.Property(e => e.CheckingId).HasColumnName("checkin_id");
                entity.Property(e => e.TaskId).HasColumnName("task_id");
                entity.Property(e => e.CheckinAt).HasColumnName("checkin_at");
                entity.Property(e => e.CheckingDate).HasColumnName("checkin_date");
                entity.Property(e => e.CheckinType).HasColumnName("checkin_type").HasMaxLength(20);

                entity.HasIndex(e => new { e.TaskId, e.CheckingDate }).IsUnique();

                entity.HasOne<TaskItem>()
                    .WithMany()
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // User
            // =========================
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");
                entity.HasKey(e => e.UserId);

                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.Email).HasColumnName("Email");
                entity.Property(e => e.Password).HasColumnName("Password");
                entity.Property(e => e.Phone).HasColumnName("Phone");
                entity.Property(e => e.Nickname).HasColumnName("Nickname");
                entity.Property(e => e.birthday).HasColumnName("Birthday");
                entity.Property(e => e.ResetCode).HasColumnName("ResetCode");
                entity.Property(e => e.ResetCodeExpiration).HasColumnName("ResetCodeExpiration");
                entity.Property(e => e.IsNotificationEnabled).HasColumnName("IsNotificationEnabled");
                entity.Property(e => e.Theme).HasColumnName("Theme");
                entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
                entity.Property(e => e.DeletedAt).HasColumnName("DeletedAt");
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            });

            // =========================
            // Notification
            // =========================
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId");
                entity.Property(e => e.Title).HasColumnName("Title");
                entity.Property(e => e.Type).HasColumnName("Type");
                entity.Property(e => e.IsRead).HasColumnName("IsRead");
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}