namespace DiaryProject.ViewModels.Review
{
    public class ReviewPhotoDayGroupViewModel
    {
        public DateTime DiaryDate { get; set; }

        public string DateTitle => DiaryDate.ToString("M月d日 dddd");

        public List<ReviewPhotoItemViewModel> Photos { get; set; } = new();
    }
}