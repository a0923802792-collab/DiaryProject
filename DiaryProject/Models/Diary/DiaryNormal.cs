using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class DiaryNormal
{
    public long DiaryId { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public virtual Diary Diary { get; set; } = null!;
}
