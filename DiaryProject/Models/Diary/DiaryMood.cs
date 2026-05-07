namespace DiaryProject.Models.Diary
{
    public class DiaryMood
    {
        public long DiaryId { get; set; }

        public byte? EnergyValue { get; set; }

        public byte? StressValue { get; set; }

        public byte? SleepValue { get; set; }

        public string? EventNote { get; set; }

        public string? ThoughtNote { get; set; }

        public string? NeedNote { get; set; }

        public Diary Diary { get; set; } = null!;
    }
}