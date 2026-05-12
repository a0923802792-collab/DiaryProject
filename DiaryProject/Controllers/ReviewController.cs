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

        private int? GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        private IActionResult RedirectToLogin()
        {
            return RedirectToAction("Welcome", "Entry");
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
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

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

            ReviewTimePageViewModel vm;

            try
            {
                vm = await _reviewService.GetTimeReviewAsync(
                    userId.Value,
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
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            ReviewPhotoPageViewModel vm;

            try
            {
                vm = await _reviewService.GetPhotoReviewAsync(userId.Value);
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
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var vm = await _reviewService.GetDayDetailAsync(userId.Value, id);

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
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var vm = await _reviewService.GetDaySummaryPanelAsync(userId.Value, date);

            return PartialView("Partials/_DaySummary", vm);
        }

        [HttpGet]
        public async Task<IActionResult> DayDetail(long id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var vm = await _reviewService.GetDayDetailAsync(userId.Value, id);

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

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var vm = await _reviewService.GetPhotoDetailAsync(userId.Value, id);

            if (vm == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_PhotoDetail", vm);
        }

        [HttpGet]
        public async Task<IActionResult> FeaturedSlides(string? startMediaId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var vm = await _reviewService.GetFeaturedPhotosAsync(userId.Value);

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

        [HttpGet]
        public IActionResult GoToDiary(long id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            if (id <= 0)
            {
                return RedirectToAction(nameof(Time));
            }

            return RedirectToAction(
                actionName: "DiaryDetail",
                controllerName: "Diary",
                routeValues: new { id = (int)id }
            );
        }
    }
}