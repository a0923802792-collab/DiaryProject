using System;
using System.Collections.Generic;

namespace DiaryProject.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Nickname { get; set; } = null!;

    public string Birthday { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? ResetCode { get; set; }

    public DateTime? ResetCodeExpiration { get; set; }

    public bool IsNotificationEnabled { get; set; }

    public string Theme { get; set; } = null!;

    public virtual ICollection<Diary> Diaries { get; set; } = new List<Diary>();

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
