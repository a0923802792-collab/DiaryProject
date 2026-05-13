namespace DiaryProject.ViewModels.Review
{
    public class ReviewPhotoMonthGroupViewModel
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public string MonthTitle => $"{Month}月";

        public List<ReviewPhotoDayGroupViewModel> DayGroups { get; set; } = new();
    }
}