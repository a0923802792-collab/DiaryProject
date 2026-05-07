using DiaryProject.Models.Diary;

namespace DiaryProject.Models.Diary
{
    public class Mood
    {
        public string MoodId { get; set; } = "";

        public string MoodName { get; set; } = "";


        public string MoodEmoji { get; set; } = "";

        public bool IsPositive { get; set; }

        public bool IsHighEnergy { get; set; }

        public ICollection<DiaryMoodSelection> DiaryMoodSelections { get; set; } = new List<DiaryMoodSelection>();
    }
}