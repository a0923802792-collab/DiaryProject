using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class Tag
{
    public string TagId { get; set; } = null!;

    public int? UserId { get; set; }

    public string TagName { get; set; } = null!;

    public string TagType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual User? User { get; set; }

    public virtual ICollection<Diary> Diaries { get; set; } = new List<Diary>();
}
