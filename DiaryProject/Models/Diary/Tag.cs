using DiaryProject.Models.Diary;

namespace DiaryProject.Models.Diary
{
    public class Tag
    {
        public string TagId { get; set; } = "";

        public int? UserId { get; set; }

        public string TagName { get; set; } = "";

        public string TagType { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public ICollection<DiaryTag> DiaryTags { get; set; } = new List<DiaryTag>();
    }
}