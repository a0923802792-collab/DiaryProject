using DiaryProject.Models;
using DiaryProject.Models.Task;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TaskScheduleRule> TaskScheduleRules { get; set; }
        public DbSet<TaskChecking> TaskChecking { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}