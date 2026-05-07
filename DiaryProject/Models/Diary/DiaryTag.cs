namespace DiaryProject.Models.Diary
{
    public class DiaryTag
    {
        public long DiaryId { get; set; }

        public string TagId { get; set; } = "";

        public Diary Diary { get; set; } = null!;

        public Tag Tag { get; set; } = null!;
    }
}