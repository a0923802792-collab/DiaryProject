using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Models;

public partial class DiarySystemDbContext : DbContext
{
    public DiarySystemDbContext()
    {
    }

    public DiarySystemDbContext(DbContextOptions<DiarySystemDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Diary> Diaries { get; set; }

    public virtual DbSet<DiaryMedium> DiaryMedia { get; set; }

    public virtual DbSet<DiaryMood> DiaryMoods { get; set; }

    public virtual DbSet<DiaryNormal> DiaryNormals { get; set; }

    public virtual DbSet<Mood> Moods { get; set; }

    public virtual DbSet<PostReactionCount> PostReactionCounts { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // 交給 Program.cs 的 AddDbContext 與 DefaultConnection
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Diary>(entity =>
        {
            entity.ToTable("Diary");

            entity.HasIndex(e => new { e.UserId, e.DiaryDate, e.CreatedAt }, "IX_Diary_User_DiaryDate_CreatedAt").IsDescending(false, true, true);

            entity.HasIndex(e => new { e.UserId, e.Status }, "IX_Diary_User_Status");

            entity.HasIndex(e => new { e.UserId, e.Visibility }, "IX_Diary_User_Visibility");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Diary_CreatedAt");
            entity.Property(e => e.DiaryTime).HasPrecision(0);
            entity.Property(e => e.PreviewText).HasMaxLength(300);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("draft", "DF_Diary_Status");
            entity.Property(e => e.TemplateType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Diary_UpdatedAt");
            entity.Property(e => e.Visibility)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("private", "DF_Diary_Visibility");
            entity.Property(e => e.WeatherType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.Diaries)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Diary_User");

            entity.HasMany(d => d.Moods).WithMany(p => p.Diaries)
                .UsingEntity<Dictionary<string, object>>(
                    "DiaryMoodSelection",
                    r => r.HasOne<Mood>().WithMany()
                        .HasForeignKey("MoodId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_DiaryMoodSelection_Mood"),
                    l => l.HasOne<Diary>().WithMany()
                        .HasForeignKey("DiaryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_DiaryMoodSelection_Diary"),
                    j =>
                    {
                        j.HasKey("DiaryId", "MoodId");
                        j.ToTable("DiaryMoodSelection");
                        j.IndexerProperty<string>("MoodId")
                            .HasMaxLength(20)
                            .IsUnicode(false);
                    });

            entity.HasMany(d => d.Tags).WithMany(p => p.Diaries)
                .UsingEntity<Dictionary<string, object>>(
                    "DiaryTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_DiaryTag_Tag"),
                    l => l.HasOne<Diary>().WithMany()
                        .HasForeignKey("DiaryId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_DiaryTag_Diary"),
                    j =>
                    {
                        j.HasKey("DiaryId", "TagId");
                        j.ToTable("DiaryTag");
                        j.HasIndex(new[] { "TagId" }, "IX_DiaryTag_TagId");
                        j.IndexerProperty<string>("TagId")
                            .HasMaxLength(20)
                            .IsUnicode(false);
                    });
        });

        modelBuilder.Entity<DiaryMedium>(entity =>
        {
            entity.HasKey(e => e.MediaId);

            entity.HasIndex(e => new { e.DiaryId, e.CreatedAt }, "IX_DiaryMedia_DiaryId_CreatedAt");

            entity.Property(e => e.MediaId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_DiaryMedia_CreatedAt");
            entity.Property(e => e.FileUrl).HasMaxLength(300);
            entity.Property(e => e.MediaType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Diary).WithMany(p => p.DiaryMedia)
                .HasForeignKey(d => d.DiaryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiaryMedia_Diary");
        });

        modelBuilder.Entity<DiaryMood>(entity =>
        {
            entity.HasKey(e => e.DiaryId);

            entity.ToTable("DiaryMood");

            entity.Property(e => e.DiaryId).ValueGeneratedNever();
            entity.Property(e => e.EventNote).HasMaxLength(500);
            entity.Property(e => e.NeedNote).HasMaxLength(500);
            entity.Property(e => e.ThoughtNote).HasMaxLength(500);

            entity.HasOne(d => d.Diary).WithOne(p => p.DiaryMood)
                .HasForeignKey<DiaryMood>(d => d.DiaryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiaryMood_Diary");
        });

        modelBuilder.Entity<DiaryNormal>(entity =>
        {
            entity.HasKey(e => e.DiaryId);

            entity.ToTable("DiaryNormal");

            entity.Property(e => e.DiaryId).ValueGeneratedNever();
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Diary).WithOne(p => p.DiaryNormal)
                .HasForeignKey<DiaryNormal>(d => d.DiaryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiaryNormal_Diary");
        });

        modelBuilder.Entity<Mood>(entity =>
        {
            entity.ToTable("Mood");

            entity.Property(e => e.MoodId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MoodEmoji).HasMaxLength(10);
            entity.Property(e => e.MoodName).HasMaxLength(50);
        });

        modelBuilder.Entity<PostReactionCount>(entity =>
        {
            entity.HasKey(e => new { e.DiaryId, e.ReactionType });

            entity.ToTable("PostReactionCount");

            entity.Property(e => e.ReactionType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .UseCollation("SQL_Latin1_General_CP1_CI_AS");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Diary).WithMany(p => p.PostReactionCounts)
                .HasForeignKey(d => d.DiaryId)
                .HasConstraintName("FK_PostReactionCount_Diary");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tag");

            entity.HasIndex(e => e.TagName, "UX_Tag_System_TagName")
                .IsUnique()
                .HasFilter("([UserId] IS NULL)");

            entity.HasIndex(e => new { e.UserId, e.TagName }, "UX_Tag_User_TagName")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");

            entity.Property(e => e.TagId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Tag_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Tag_IsActive");
            entity.Property(e => e.TagName).HasMaxLength(50);
            entity.Property(e => e.TagType)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.Tags)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Tag_User");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK_Users");

            entity.ToTable("User");

            entity.Property(e => e.Birthday).HasColumnName("birthday");
            entity.Property(e => e.Theme).HasDefaultValue("");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}