using Microsoft.EntityFrameworkCore;
using DiaryProject.Models;

namespace DiaryProject.Data
{
    public class AppDbContext : DbContext
    {

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<TaskItem> Tasks { get; set; }

        public DbSet<TaskScheduleRule> TaskScheduleRules { get; set; }

        public DbSet<TaskChecking> TaskChecking { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.ToTable("task_任務");
                entity.HasKey(e => e.TaskId);
                entity.Property(e => e.TaskId).HasColumnName("task_id_任務ID");
                entity.Property(e => e.UserId).HasColumnName("user_id_使用者ID");
                entity.Property(e => e.Title).HasColumnName("title_任務名稱");
                entity.Property(e => e.RhythmType).HasColumnName("rhythm_type_執行節奏類型");
                entity.Property(e => e.Status).HasColumnName("status_任務狀態");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at_建立時間");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at_更新時間");
            });

            modelBuilder.Entity<TaskScheduleRule>(entity =>
            {
                entity.ToTable("task_schedule_rule_任務節奏規則");

                entity.HasKey(e => e.RuleId);

                entity.Property(e => e.RuleId).HasColumnName("rule_id_規則ID");
                entity.Property(e => e.TaskId).HasColumnName("task_id_任務ID");
                entity.Property(e => e.WeeklyTargetCount).HasColumnName("weekly_target_count_每週目標次數");
                entity.Property(e => e.StartDate).HasColumnName("start_date_開始日期");
                entity.Property(e => e.EndDate).HasColumnName("end_date_結束日期");
            });

            modelBuilder.Entity<TaskChecking>(entity =>
            {
                entity.ToTable("task_checkin_log_任務完成紀錄");

                entity.HasKey(e => e.CheckingId);

                entity.Property(e => e.CheckingId).HasColumnName("checkin_id_打卡ID");
                entity.Property(e => e.TaskId).HasColumnName("task_id_任務ID");
                entity.Property(e => e.CheckinAt).HasColumnName("checkin_at_打卡時間");
                entity.Property(e => e.CheckingDate).HasColumnName("checkin_date_打卡日期");
                entity.Property(e => e.CheckinType).HasColumnName("checkin_type_打卡類型");
            });
        }    
    }
}
