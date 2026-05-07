using DiaryProject.Models;
using DiaryProject.Models.Diary;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Diary> Diaries { get; set; }
        public DbSet<DiaryNormal> DiaryNormals { get; set; }
        public DbSet<DiaryMood> DiaryMoods { get; set; }
        public DbSet<Mood> Moods { get; set; }
        public DbSet<DiaryMoodSelection> DiaryMoodSelections { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<DiaryTag> DiaryTags { get; set; }
        public DbSet<DiaryMedia> DiaryMedias { get; set; }

        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TaskScheduleRule> TaskScheduleRules { get; set; }
        public DbSet<TaskChecking> TaskChecking { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // Task
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
            // Diary
            // =========================
            modelBuilder.Entity<Diary>(entity =>
            {
                entity.ToTable("Diary");

                entity.HasKey(e => e.DiaryId);

                entity.Property(e => e.TemplateType)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.PreviewText)
                    .HasMaxLength(300);

                entity.Property(e => e.WeatherType)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.Visibility)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.HasOne(e => e.DiaryNormal)
                    .WithOne(e => e.Diary)
                    .HasForeignKey<DiaryNormal>(e => e.DiaryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DiaryMood)
                    .WithOne(e => e.Diary)
                    .HasForeignKey<DiaryMood>(e => e.DiaryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DiaryNormal>(entity =>
            {
                entity.ToTable("DiaryNormal");

                entity.HasKey(e => e.DiaryId);

                entity.Property(e => e.Title)
                    .HasMaxLength(200);
            });

            modelBuilder.Entity<DiaryMood>(entity =>
            {
                entity.ToTable("DiaryMood");

                entity.HasKey(e => e.DiaryId);
            });

            modelBuilder.Entity<Mood>(entity =>
            {
                entity.ToTable("Mood");

                entity.HasKey(e => e.MoodId);

                entity.Property(e => e.MoodId)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.MoodName)
                    .HasMaxLength(50);

                entity.Property(e => e.MoodEmoji)
                    .HasMaxLength(10);
            });

            modelBuilder.Entity<DiaryMoodSelection>(entity =>
            {
                entity.ToTable("DiaryMoodSelection");

                entity.HasKey(e => new { e.DiaryId, e.MoodId });

                entity.Property(e => e.MoodId)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.HasOne(e => e.Diary)
                    .WithMany(e => e.DiaryMoodSelections)
                    .HasForeignKey(e => e.DiaryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Mood)
                    .WithMany(e => e.DiaryMoodSelections)
                    .HasForeignKey(e => e.MoodId);
            });

            modelBuilder.Entity<Tag>(entity =>
            {
                entity.ToTable("Tag");

                entity.HasKey(e => e.TagId);

                entity.Property(e => e.TagId)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.TagName)
                    .HasMaxLength(50);

                entity.Property(e => e.TagType)
                    .HasMaxLength(20)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<DiaryTag>(entity =>
            {
                entity.ToTable("DiaryTag");

                entity.HasKey(e => new { e.DiaryId, e.TagId });

                entity.Property(e => e.TagId)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.HasOne(e => e.Diary)
                    .WithMany(e => e.DiaryTags)
                    .HasForeignKey(e => e.DiaryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Tag)
                    .WithMany(e => e.DiaryTags)
                    .HasForeignKey(e => e.TagId);
            });

            modelBuilder.Entity<DiaryMedia>(entity =>
            {
                entity.ToTable("DiaryMedia");

                entity.HasKey(e => e.MediaId);

                entity.Property(e => e.MediaId)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.MediaType)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.FileUrl)
                    .HasMaxLength(300);

                entity.HasOne(e => e.Diary)
                    .WithMany(e => e.DiaryMedias)
                    .HasForeignKey(e => e.DiaryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}