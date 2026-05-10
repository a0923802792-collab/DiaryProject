using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class Diary
{
    public long DiaryId { get; set; }

    public int UserId { get; set; }

    public string TemplateType { get; set; } = null!;

    public string? PreviewText { get; set; }

    public DateOnly DiaryDate { get; set; }

    public TimeOnly DiaryTime { get; set; }

    public string? WeatherType { get; set; }

    public string Visibility { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<DiaryMedium> DiaryMedia { get; set; } = new List<DiaryMedium>();

    public virtual DiaryMood? DiaryMood { get; set; }

    public virtual DiaryNormal? DiaryNormal { get; set; }

    public virtual ICollection<PostReactionCount> PostReactionCounts { get; set; } = new List<PostReactionCount>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Mood> Moods { get; set; } = new List<Mood>();

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
