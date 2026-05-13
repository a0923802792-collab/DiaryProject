using DiaryProject.ViewModels.Review;

namespace DiaryProject.Services.Review
{
    public interface IReviewService
    {
        Task<ReviewTimePageViewModel> GetTimeReviewAsync(
            int userId,
            int year,
            int month,
            DateTime? selectedDate = null,
            long? selectedDiaryId = null);

        Task<ReviewDaySummaryPanelViewModel> GetDaySummaryPanelAsync(
            int userId,
            DateTime date);

        Task<ReviewPhotoPageViewModel> GetPhotoReviewAsync(int userId);

        Task<ReviewDaySummaryViewModel?> GetDayDetailAsync(int userId, long diaryId);

        Task<ReviewPhotoItemViewModel?> GetPhotoDetailAsync(int userId, string mediaId);

        Task<ReviewPhotoDetailViewModel?> GetPhotoDetailSlidesAsync(int userId, string mediaId);

        Task<List<ReviewPhotoItemViewModel>> GetFeaturedPhotosAsync(int userId);
    }
}