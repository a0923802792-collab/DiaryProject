using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class Mood
{
    public string MoodId { get; set; } = null!;

    public string MoodName { get; set; } = null!;

    public string MoodEmoji { get; set; } = null!;

    public bool IsPositive { get; set; }

    public bool IsHighEnergy { get; set; }

    public virtual ICollection<Diary> Diaries { get; set; } = new List<Diary>();
}
