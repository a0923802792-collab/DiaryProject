using DiaryProject.Data;
using DiaryProject.ViewModels.Review;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Services.Review
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;

        public ReviewService(AppDbContext context)
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
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var diaries = await _context.Diaries
                .AsNoTracking()
                .Where(d =>
                    d.UserId == userId &&
                    d.Status != "deleted" &&
                    d.DiaryDate >= startDate &&
                    d.DiaryDate < endDate)
                .Include(d => d.DiaryNormal)
                .Include(d => d.DiaryMood)
                .Include(d => d.DiaryMoodSelections)
                    .ThenInclude(ms => ms.Mood)
                .Include(d => d.DiaryTags)
                    .ThenInclude(dt => dt.Tag)
                .Include(d => d.DiaryMedias)
                .OrderByDescending(d => d.DiaryDate)
                .ThenByDescending(d => d.DiaryTime)
                .ToListAsync();

            var selectedDiary = selectedDiaryId.HasValue
                ? diaries.FirstOrDefault(d => d.DiaryId == selectedDiaryId.Value)
                : null;

            var finalSelectedDate =
                selectedDiary?.DiaryDate.Date
                ?? selectedDate?.Date
                ?? (year == today.Year && month == today.Month
                    ? today.Date
                    : startDate.Date);

            var diaryGroups = diaries
                .GroupBy(d => d.DiaryDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(d => d.DiaryTime)
                        .ToList()
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
                var mainMood = mainDiary?.DiaryMoodSelections
                    .Select(ms => ms.Mood)
                    .FirstOrDefault();

                calendarDays.Add(new ReviewCalendarDayViewModel
                {
                    DiaryId = mainDiary?.DiaryId,
                    Date = currentDate,
                    DayNumber = currentDate.Day,
                    IsCurrentMonth = isCurrentMonth,
                    IsFuture = isFuture,
                    HasDiary = mainDiary != null,
                    DiaryCount = dayDiaries?.Count ?? 0,
                    HasPhoto = dayDiaries?.Any(d => d.DiaryMedias.Any()) ?? false,
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
            var targetDate = date.Date;

            if (targetDate > today)
            {
                return new ReviewDaySummaryPanelViewModel
                {
                    Date = targetDate,
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
                .Include(d => d.DiaryMoodSelections)
                    .ThenInclude(ms => ms.Mood)
                .Include(d => d.DiaryTags)
                    .ThenInclude(dt => dt.Tag)
                .Include(d => d.DiaryMedias)
                .OrderByDescending(d => d.DiaryTime)
                .ToListAsync();

            var diary = diaries.FirstOrDefault();

            return new ReviewDaySummaryPanelViewModel
            {
                Date = targetDate,
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
                .Include(d => d.DiaryMoodSelections)
                    .ThenInclude(ms => ms.Mood)
                .Include(d => d.DiaryTags)
                    .ThenInclude(dt => dt.Tag)
                .Include(d => d.DiaryMedias)
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
            Dictionary<DateTime, List<Models.Diary.Diary>> diaryGroups)
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

        private IQueryable<Models.Diary.DiaryMedia> GetPhotoQuery(int userId)
        {
            return _context.DiaryMedias
                .AsNoTracking()
                .Where(m => m.MediaType == "image" || m.MediaType == "drawing")
                .Include(m => m.Diary)
                    .ThenInclude(d => d.DiaryMood)
                .Include(m => m.Diary)
                    .ThenInclude(d => d.DiaryMoodSelections)
                        .ThenInclude(ms => ms.Mood)
                .Include(m => m.Diary)
                    .ThenInclude(d => d.DiaryTags)
                        .ThenInclude(dt => dt.Tag)
                .Where(m =>
                    m.Diary.UserId == userId &&
                    m.Diary.Status != "deleted");
        }

        private ReviewDaySummaryViewModel ToDaySummaryViewModel(Models.Diary.Diary diary)
        {
            var mainMood = diary.DiaryMoodSelections
                .Select(ms => ms.Mood)
                .FirstOrDefault();

            return new ReviewDaySummaryViewModel
            {
                DiaryId = diary.DiaryId,
                DiaryDate = diary.DiaryDate,
                TemplateType = diary.TemplateType,
                Title = diary.DiaryNormal?.Title,
                PreviewText = diary.PreviewText,
                WeatherType = diary.WeatherType,
                MainMoodName = mainMood?.MoodName,
                MainMoodEmoji = mainMood?.MoodEmoji,
                EnergyValue = diary.DiaryMood?.EnergyValue,
                StressValue = diary.DiaryMood?.StressValue,
                SleepValue = diary.DiaryMood?.SleepValue,
                Tags = diary.DiaryTags
                    .Select(dt => dt.Tag.TagName)
                    .ToList(),
                PhotoUrls = diary.DiaryMedias
                    .Select(m => m.FileUrl)
                    .ToList()
            };
        }

        private ReviewPhotoItemViewModel ToPhotoItemViewModel(Models.Diary.DiaryMedia media)
        {
            var mainMood = media.Diary.DiaryMoodSelections
                .Select(ms => ms.Mood)
                .FirstOrDefault();

            return new ReviewPhotoItemViewModel
            {
                MediaId = media.MediaId,
                DiaryId = media.DiaryId,
                FileUrl = media.FileUrl,
                MediaType = media.MediaType,
                DiaryDate = media.Diary.DiaryDate,
                PreviewText = media.Diary.PreviewText,
                MainMoodName = mainMood?.MoodName,
                MainMoodEmoji = mainMood?.MoodEmoji,
                Tags = media.Diary.DiaryTags
                    .Select(dt => dt.Tag.TagName)
                    .ToList(),
                IsFeatured =
                    media.Diary.DiaryMood != null &&
                    media.Diary.DiaryMood.StressValue <= 4
            };
        }
    }
}