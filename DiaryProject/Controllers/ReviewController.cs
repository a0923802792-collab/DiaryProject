using DiaryProject.Services.Review;
using DiaryProject.ViewModels.Review;
using Microsoft.AspNetCore.Mvc;

namespace DiaryProject.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Time));
        }

        public async Task<IActionResult> Time(
            int? year,
            int? month,
            DateTime? selectedDate,
            long? selectedDiaryId)
        {
            var today = DateTime.Today;

            var targetYear = year ?? today.Year;
            var targetMonth = month ?? today.Month;

            if (targetYear > today.Year)
            {
                targetYear = today.Year;
            }

            if (targetYear == today.Year && targetMonth > today.Month)
            {
                targetMonth = today.Month;
            }

            if (targetMonth < 1)
            {
                targetMonth = 1;
            }

            if (targetMonth > 12)
            {
                targetMonth = 12;
            }

            var userId = 1;

            ReviewTimePageViewModel vm;

            try
            {
                vm = await _reviewService.GetTimeReviewAsync(
                    userId,
                    targetYear,
                    targetMonth,
                    selectedDate,
                    selectedDiaryId);
            }
            catch (Exception ex)
            {
                return Content(ex.ToString());
            }

            return View(vm);
        }

        public async Task<IActionResult> Photos()
        {
            var userId = 1;

            ReviewPhotoPageViewModel vm;

            try
            {
                vm = await _reviewService.GetPhotoReviewAsync(userId);
            }
            catch
            {
                vm = new ReviewPhotoPageViewModel
                {
                    Photos = new List<ReviewPhotoItemViewModel>(),
                    FeaturedPhotos = new List<ReviewPhotoItemViewModel>()
                };
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> DaySummary(long id)
        {
            var userId = 1;

            var vm = await _reviewService.GetDayDetailAsync(userId, id);

            if (vm == null)
            {
                return NotFound();
            }

            var panelVm = new ReviewDaySummaryPanelViewModel
            {
                Date = vm.DiaryDate,
                IsFuture = false,
                Diary = vm
            };

            return PartialView("Partials/_DaySummary", panelVm);
        }

        [HttpGet]
        public async Task<IActionResult> DaySummaryByDate(DateTime date)
        {
            var userId = 1;

            var vm = await _reviewService.GetDaySummaryPanelAsync(userId, date);

            return PartialView("Partials/_DaySummary", vm);
        }

        [HttpGet]
        public async Task<IActionResult> DayDetail(long id)
        {
            var userId = 1;

            var vm = await _reviewService.GetDayDetailAsync(userId, id);

            if (vm == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_DayDetail", vm);
        }

        [HttpGet]
        public async Task<IActionResult> PhotoDetail(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest();
            }

            var userId = 1;

            var vm = await _reviewService.GetPhotoDetailAsync(userId, id);

            if (vm == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_PhotoDetail", vm);
        }

        [HttpGet]
        public async Task<IActionResult> FeaturedSlides(string? startMediaId)
        {
            var userId = 1;

            var vm = await _reviewService.GetFeaturedPhotosAsync(userId);

            if (!string.IsNullOrWhiteSpace(startMediaId))
            {
                var startIndex = vm.FindIndex(x => x.MediaId == startMediaId);

                if (startIndex > 0)
                {
                    vm = vm
                        .Skip(startIndex)
                        .Concat(vm.Take(startIndex))
                        .ToList();
                }
            }

            return PartialView("Partials/_FeaturedSlides", vm);
        }
    }
}