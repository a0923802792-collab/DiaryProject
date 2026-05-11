using DiaryProject.Models;
using DiaryProject.ViewModels.Review;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Services.Review
{
    public class ReviewService : IReviewService
    {
        private readonly DiarySystemDbContext _context;

        public ReviewService(DiarySystemDbContext context)
        {
            _context = context;
        }

        public async Task<ReviewTimePageViewModel> GetTimeReviewAsync(
            int userId,
            int year,
            int month,
            DateTime? selectedDate = null,
            long? selectedDiaryId = null)
        {
            var today = DateTime.Today;
            var startDate = new DateOnly(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var diaries = await _context.Diaries
                .AsNoTracking()
                .AsSplitQuery()
                .Where(d =>
                    d.UserId == userId &&
                    d.Status != "deleted" &&
                    d.DiaryDate >= startDate &&
                    d.DiaryDate < endDate)
                .Include(d => d.DiaryNormal)
                .Include(d => d.DiaryMood)
                .Include(d => d.Moods)
                .Include(d => d.Tags)
                .Include(d => d.DiaryMedia)
                .OrderByDescending(d => d.DiaryDate)
                .ThenByDescending(d => d.DiaryTime)
                .ToListAsync();

            var selectedDiary = selectedDiaryId.HasValue
                ? diaries.FirstOrDefault(d => d.DiaryId == selectedDiaryId.Value)
                : null;

            var finalSelectedDate =
                selectedDiary?.DiaryDate.ToDateTime(TimeOnly.MinValue).Date
                ?? selectedDate?.Date
                ?? (year == today.Year && month == today.Month
                    ? today.Date
                    : new DateTime(year, month, 1));

            var diaryGroups = diaries
                .GroupBy(d => d.DiaryDate.ToDateTime(TimeOnly.MinValue).Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(d => d.DiaryTime).ToList()
                );

            var firstDayOfMonth = new DateTime(year, month, 1);
            var startOffset = (int)firstDayOfMonth.DayOfWeek;
            var calendarStartDate = firstDayOfMonth.AddDays(-startOffset);

            var calendarDays = new List<ReviewCalendarDayViewModel>();

            for (var i = 0; i < 42; i++)
            {
                var currentDate = calendarStartDate.AddDays(i);
                var isCurrentMonth = currentDate.Year == year && currentDate.Month == month;
                var isFuture = currentDate.Date > today;

                diaryGroups.TryGetValue(currentDate.Date, out var dayDiaries);

                var mainDiary = dayDiaries?.FirstOrDefault();
                var mainMood = mainDiary?.Moods.FirstOrDefault();

                calendarDays.Add(new ReviewCalendarDayViewModel
                {
                    DiaryId = mainDiary?.DiaryId,
                    Date = currentDate,
                    DayNumber = currentDate.Day,
                    IsCurrentMonth = isCurrentMonth,
                    IsFuture = isFuture,
                    HasDiary = mainDiary != null,
                    DiaryCount = dayDiaries?.Count ?? 0,
                    HasPhoto = dayDiaries?.Any(d => d.DiaryMedia.Any()) ?? false,
                    IsSelected = currentDate.Date == finalSelectedDate.Date,
                    MainMoodName = mainMood?.MoodName,
                    MainMoodEmoji = mainMood?.MoodEmoji,
                    StressValue = mainDiary?.DiaryMood?.StressValue
                });
            }

            var recentDiaries = diaries
                .Take(5)
                .Select(ToDaySummaryViewModel)
                .ToList();

            var selectedPanel = BuildSelectedDayPanel(
                finalSelectedDate,
                today,
                diaryGroups);

            return new ReviewTimePageViewModel
            {
                Year = year,
                Month = month,
                SelectedDate = finalSelectedDate,
                CalendarDays = calendarDays,
                RecentDiaries = recentDiaries,
                SelectedDayPanel = selectedPanel
            };
        }

        public async Task<ReviewDaySummaryPanelViewModel> GetDaySummaryPanelAsync(
            int userId,
            DateTime date)
        {
            var today = DateTime.Today;
            var targetDate = DateOnly.FromDateTime(date.Date);

            if (date.Date > today)
            {
                return new ReviewDaySummaryPanelViewModel
                {
                    Date = date.Date,
                    IsFuture = true,
                    Diary = null
                };
            }

            var diaries = await _context.Diaries
                .AsNoTracking()
                .Where(d =>
                    d.UserId == userId &&
                    d.Status != "deleted" &&
                    d.DiaryDate == targetDate)
                .Include(d => d.DiaryNormal)
                .Include(d => d.DiaryMood)
                .Include(d => d.Moods)
                .Include(d => d.Tags)
                .Include(d => d.DiaryMedia)
                .OrderByDescending(d => d.DiaryTime)
                .ToListAsync();

            var diary = diaries.FirstOrDefault();

            return new ReviewDaySummaryPanelViewModel
            {
                Date = date.Date,
                IsFuture = false,
                Diary = diary == null ? null : ToDaySummaryViewModel(diary)
            };
        }

        public async Task<ReviewPhotoPageViewModel> GetPhotoReviewAsync(int userId)
        {
            var photos = await GetPhotoQuery(userId)
                .OrderByDescending(m => m.Diary.DiaryDate)
                .ThenByDescending(m => m.CreatedAt)
                .ToListAsync();

            var photoItems = photos.Select(ToPhotoItemViewModel).ToList();

            return new ReviewPhotoPageViewModel
            {
                Photos = photoItems,
                FeaturedPhotos = photoItems
                    .Where(p => p.IsFeatured)
                    .Take(3)
                    .ToList()
            };
        }

        public async Task<ReviewDaySummaryViewModel?> GetDayDetailAsync(int userId, long diaryId)
        {
            var diary = await _context.Diaries
                .AsNoTracking()
                .Include(d => d.DiaryNormal)
                .Include(d => d.DiaryMood)
                .Include(d => d.Moods)
                .Include(d => d.Tags)
                .Include(d => d.DiaryMedia)
                .FirstOrDefaultAsync(d =>
                    d.UserId == userId &&
                    d.DiaryId == diaryId &&
                    d.Status != "deleted");

            if (diary == null)
            {
                return null;
            }

            return ToDaySummaryViewModel(diary);
        }

        public async Task<ReviewPhotoItemViewModel?> GetPhotoDetailAsync(int userId, string mediaId)
        {
            var media = await GetPhotoQuery(userId)
                .FirstOrDefaultAsync(m => m.MediaId == mediaId);

            if (media == null)
            {
                return null;
            }

            return ToPhotoItemViewModel(media);
        }

        public async Task<List<ReviewPhotoItemViewModel>> GetFeaturedPhotosAsync(int userId)
        {
            var photos = await GetPhotoQuery(userId)
                .OrderByDescending(m => m.Diary.DiaryDate)
                .ThenByDescending(m => m.CreatedAt)
                .ToListAsync();

            return photos
                .Select(ToPhotoItemViewModel)
                .Where(p => p.IsFeatured)
                .Take(5)
                .ToList();
        }

        private ReviewDaySummaryPanelViewModel BuildSelectedDayPanel(
            DateTime selectedDate,
            DateTime today,
            Dictionary<DateTime, List<Diary>> diaryGroups)
        {
            if (selectedDate.Date > today.Date)
            {
                return new ReviewDaySummaryPanelViewModel
                {
                    Date = selectedDate.Date,
                    IsFuture = true,
                    Diary = null
                };
            }

            diaryGroups.TryGetValue(selectedDate.Date, out var dayDiaries);

            var diary = dayDiaries?.FirstOrDefault();

            return new ReviewDaySummaryPanelViewModel
            {
                Date = selectedDate.Date,
                IsFuture = false,
                Diary = diary == null ? null : ToDaySummaryViewModel(diary)
            };
        }

        private IQueryable<DiaryMedium> GetPhotoQuery(int userId)
        {
            return _context.DiaryMedia
                .AsNoTracking()
                .Where(m => m.MediaType == "image" || m.MediaType == "drawing")
                .Include(m => m.Diary)
                    .ThenInclude(d => d.DiaryMood)
                .Include(m => m.Diary)
                    .ThenInclude(d => d.Moods)
                .Include(m => m.Diary)
                    .ThenInclude(d => d.Tags)
                .Where(m =>
                    m.Diary.UserId == userId &&
                    m.Diary.Status != "deleted");
        }

        private ReviewDaySummaryViewModel ToDaySummaryViewModel(Diary diary)
        {
            var mainMood = diary.Moods.FirstOrDefault();

            return new ReviewDaySummaryViewModel
            {
                DiaryId = diary.DiaryId,
                DiaryDate = diary.DiaryDate.ToDateTime(TimeOnly.MinValue),
                TemplateType = diary.TemplateType,
                Title = diary.DiaryNormal?.Title,
                PreviewText = diary.PreviewText,
                WeatherType = diary.WeatherType,
                MainMoodName = mainMood?.MoodName,
                MainMoodEmoji = mainMood?.MoodEmoji,
                EnergyValue = diary.DiaryMood?.EnergyValue,
                StressValue = diary.DiaryMood?.StressValue,
                SleepValue = diary.DiaryMood?.SleepValue,
                Tags = diary.Tags
                    .Select(t => t.TagName)
                    .ToList(),
                PhotoUrls = diary.DiaryMedia
                    .Select(m => m.FileUrl)
                    .ToList()
            };
        }

        private ReviewPhotoItemViewModel ToPhotoItemViewModel(DiaryMedium media)
        {
            var mainMood = media.Diary.Moods.FirstOrDefault();

            return new ReviewPhotoItemViewModel
            {
                MediaId = media.MediaId,
                DiaryId = media.DiaryId,
                FileUrl = media.FileUrl,
                MediaType = media.MediaType,
                DiaryDate = media.Diary.DiaryDate.ToDateTime(TimeOnly.MinValue),
                PreviewText = media.Diary.PreviewText,
                MainMoodName = mainMood?.MoodName,
                MainMoodEmoji = mainMood?.MoodEmoji,
                Tags = media.Diary.Tags
                    .Select(t => t.TagName)
                    .ToList(),
                IsFeatured =
                    media.Diary.DiaryMood != null &&
                    (media.Diary.DiaryMood.StressValue ?? 10) <= 4
            };
        }
    }
}