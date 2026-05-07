namespace DiaryProject.Models.Diary
{
    public class DiaryMedia
    {
        public string MediaId { get; set; } = "";

        public long DiaryId { get; set; }

        public string MediaType { get; set; } = "";

        public string FileUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public Diary Diary { get; set; } = null!;
    }
}