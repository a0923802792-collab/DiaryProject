using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class PostReactionCount
{
    public long DiaryId { get; set; }

    public string ReactionType { get; set; } = null!;

    public int Count { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Diary Diary { get; set; } = null!;
}
