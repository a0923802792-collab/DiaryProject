namespace DiaryProject.ViewModels.Review
{
    public class ReviewPhotoPageViewModel
    {
        public List<ReviewPhotoItemViewModel> Photos { get; set; } = new();

        public List<ReviewPhotoItemViewModel> FeaturedPhotos { get; set; } = new();

        public List<ReviewPhotoMonthGroupViewModel> MonthGroups { get; set; } = new();
    }
}