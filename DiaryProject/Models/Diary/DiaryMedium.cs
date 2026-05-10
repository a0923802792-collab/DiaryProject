using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class DiaryMedium
{
    public string MediaId { get; set; } = null!;

    public long DiaryId { get; set; }

    public string MediaType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Diary Diary { get; set; } = null!;
}
