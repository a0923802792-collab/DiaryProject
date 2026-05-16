using DiaryProject.Models;
using DiaryProject.ViewModels.Diary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;

namespace DiaryProject.Controllers
{
    public class DiaryController : Controller
    {
        private readonly DiarySystemDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DiaryController(DiarySystemDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private int? GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        private IActionResult RedirectToLogin()
        {
            return RedirectToAction("Welcome", "Entry");
        }

        public IActionResult DiaryList(
            string? templateType,
            string? visibility,
            string? keyword,
            string? sortOrder,
            string? period,
            string? startDate,
            string? endDate,
            string? tags)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            using var readTransaction = _context.Database.BeginTransaction(IsolationLevel.ReadUncommitted);

            // 先整理列表頁傳進來的篩選條件，避免空白或不合法的值影響查詢。
            // 這些參數主要用在「從詳情頁返回列表」時，讓列表可以還原原本的篩選狀態。
            templateType = NormalizeTemplateType(templateType);     // normal / mood
            visibility = NormalizeVisibility(visibility);           // private / shared
            keyword = NormalizeKeyword(keyword);                    // 搜尋字
            sortOrder = NormalizeSortOrder(sortOrder);              // desc / asc
            period = NormalizePeriod(period);                       // all / week / month / range
            var selectedTags = NormalizeTags(tags);                 // 多選標籤，以逗號分隔
            var parsedStartDate = NormalizeDateOnly(startDate);      // 特定範圍：開始日期
            var parsedEndDate = NormalizeDateOnly(endDate);          // 特定範圍：結束日期

            // 把目前篩選條件放進 ViewBag，讓 DiaryList.cshtml 與 diary_index.js 可以還原畫面狀態。
            ViewBag.FilterTemplateType = templateType;
            ViewBag.FilterVisibility = visibility;
            ViewBag.FilterKeyword = keyword;
            ViewBag.FilterSortOrder = sortOrder;
            ViewBag.FilterPeriod = period;
            ViewBag.FilterStartDate = parsedStartDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterEndDate = parsedEndDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterTags = string.Join(",", selectedTags);

            // 先建立 IQueryable，後面才可以依條件逐步加上 Where。
            // 這裡只抓目前登入使用者、且已發布的日記。
            var listQuery = _context.Diaries
                .AsNoTracking()
                .Where(d => d.UserId == userId.Value && d.Status == "published");

            // 套用和詳情頁上一篇 / 下一篇相同的篩選規則。
            listQuery = ApplyDiaryListFilters(
                listQuery,
                templateType,
                visibility,
                keyword,
                period,
                parsedStartDate,
                parsedEndDate,
                selectedTags);

            var rows = ApplyDiaryListOrdering(listQuery, sortOrder)
                .Select(d => new
                {
                    d.DiaryId,
                    d.DiaryDate,
                    d.DiaryTime,
                    d.TemplateType,
                    d.Visibility,
                    d.PreviewText,
                    NormalTitle = d.DiaryNormal != null ? d.DiaryNormal.Title : string.Empty,
                    MoodEvent = d.DiaryMood != null ? d.DiaryMood.EventNote : string.Empty,
                    MoodThought = d.DiaryMood != null ? d.DiaryMood.ThoughtNote : string.Empty,
                    MoodNeed = d.DiaryMood != null ? d.DiaryMood.NeedNote : string.Empty,
                    ImageCount = d.DiaryMedia.Count(m => m.MediaType == "image"),
                    DrawingCount = d.DiaryMedia.Count(m => m.MediaType == "drawing")
                })
                .ToList();

            // 將每篇日記的時間另外存到 ViewBag。
            // 這樣 DiaryList.cshtml 不必修改 ViewModel，也能把正確時間傳給前端 JS 排序。
            ViewBag.DiaryTimeById = rows.ToDictionary(x => (int)x.DiaryId, x => x.DiaryTime.ToString("HH:mm"));

            var diaryIds = rows.Select(d => d.DiaryId).ToList();
            if (diaryIds.Count == 0)
            {
                return View(new Diary_ListAll());
            }

            var tagRows = _context.Diaries
                .AsNoTracking()
                .Where(d => diaryIds.Contains(d.DiaryId))
                .SelectMany(d => d.Tags.Select(t => new { d.DiaryId, t.TagName }))
                .ToList();

            var moodRows = _context.Diaries
                .AsNoTracking()
                .Where(d => diaryIds.Contains(d.DiaryId))
                .SelectMany(d => d.Moods.Select(m => new { d.DiaryId, m.MoodEmoji }))
                .ToList();

            var reactionRows = _context.PostReactionCounts
                .AsNoTracking()
                .Where(r => diaryIds.Contains(r.DiaryId) && r.Count > 0)
                .Select(r => new { r.DiaryId, r.ReactionType, r.Count })
                .ToList();

            var tagsByDiary = tagRows
                .GroupBy(x => x.DiaryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.TagName)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .ToList());

            var moodsByDiary = moodRows
                .GroupBy(x => x.DiaryId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(" ", g.Select(x => x.MoodEmoji).Where(x => !string.IsNullOrWhiteSpace(x))));

            var reactionsByDiary = reactionRows
                .GroupBy(x => x.DiaryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(
                        x => (x.ReactionType ?? string.Empty).Trim().ToLowerInvariant(),
                        x => x.Count));

            var fixedReactionOrder = new[] { "like", "love", "hug", "empathy", "cheer" };
            var model = new Diary_ListAll();

            foreach (var row in rows)
            {
                var moodEvent = row.MoodEvent ?? string.Empty;
                var moodThought = row.MoodThought ?? string.Empty;
                var moodNeed = row.MoodNeed ?? string.Empty;
                var previewText = (row.PreviewText ?? string.Empty).Trim();

                var vm = new Diary_List
                {
                    DiaryId = row.DiaryId,
                    DiaryDate = row.DiaryDate.ToString("yyyy-MM-dd"),
                    TemplateType = row.TemplateType,
                    Title = row.TemplateType == "normal" ? row.NormalTitle ?? string.Empty : string.Empty,
                    Body = row.TemplateType == "normal" ? previewText : string.Empty,
                    MoodEmoji = row.TemplateType == "mood" && moodsByDiary.TryGetValue(row.DiaryId, out var moodEmoji) ? moodEmoji : string.Empty,
                    EventNote = row.TemplateType == "mood" ? moodEvent : string.Empty,
                    ThoughtNote = row.TemplateType == "mood" ? moodThought : string.Empty,
                    NeedNote = row.TemplateType == "mood" ? moodNeed : string.Empty,
                    PreviewText = string.IsNullOrWhiteSpace(previewText) ? "尚未輸入內容" : previewText,
                    ImageCount = row.ImageCount,
                    DrawingCount = row.DrawingCount,
                    IsShared = row.Visibility == "shared"
                };

                if (tagsByDiary.TryGetValue(row.DiaryId, out var diaryTags))
                {
                    vm.TagName.AddRange(diaryTags);
                }

                if (row.Visibility == "shared" && reactionsByDiary.TryGetValue(row.DiaryId, out var reactionMap))
                {
                    foreach (var reactionType in fixedReactionOrder)
                    {
                        if (!reactionMap.TryGetValue(reactionType, out var count)) continue;
                        vm.Reactions.Add($"{ToReactionEmoji(reactionType)} {count}");
                    }
                }

                model.Diaries.Add(vm);
            }

            model.TotalCount = model.Diaries.Count;
            model.NormalCount = model.Diaries.Count(x => x.TemplateType == "normal");
            model.MoodCount = model.Diaries.Count(x => x.TemplateType == "mood");

            return View(model);
        }

        public IActionResult DiaryDetail(
            int id,
            string? templateType,
            string? visibility,
            string? keyword,
            string? sortOrder,
            string? period,
            string? startDate,
            string? endDate,
            string? tags)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            using var readTransaction = _context.Database.BeginTransaction(IsolationLevel.ReadUncommitted);

            // 詳情頁也要接收列表頁的篩選條件。
            // 這樣「上一篇 / 下一篇」才會在篩選後的結果內切換，而不是全部日記一起切換。
            templateType = NormalizeTemplateType(templateType);
            visibility = NormalizeVisibility(visibility);
            keyword = NormalizeKeyword(keyword);
            sortOrder = NormalizeSortOrder(sortOrder);
            period = NormalizePeriod(period);
            var selectedTags = NormalizeTags(tags);
            var parsedStartDate = NormalizeDateOnly(startDate);
            var parsedEndDate = NormalizeDateOnly(endDate);

            // 放進 ViewBag 給 DiaryDetail.cshtml 使用，讓返回 / 上一篇 / 下一篇連結可以保留篩選條件。
            ViewBag.FilterTemplateType = templateType;
            ViewBag.FilterVisibility = visibility;
            ViewBag.FilterKeyword = keyword;
            ViewBag.FilterSortOrder = sortOrder;
            ViewBag.FilterPeriod = period;
            ViewBag.FilterStartDate = parsedStartDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterEndDate = parsedEndDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterTags = string.Join(",", selectedTags);

            if (id <= 0)
            {
                return RedirectToAction(nameof(DiaryList));
            }

            var row = _context.Diaries
                .AsNoTracking()
                .AsSplitQuery()
                .Include(d => d.DiaryNormal)
                .Include(d => d.DiaryMood)
                .Include(d => d.DiaryMedia)
                .Include(d => d.Moods)
                .Include(d => d.Tags)
                .FirstOrDefault(d => d.DiaryId == id && d.UserId == userId.Value && d.Status == "published");

            if (row == null)
            {
                return NotFound();
            }

            var vm = new Diary_Detail
            {
                DiaryId = (int)row.DiaryId,
                DiaryDate = row.DiaryDate.ToString("yyyy-MM-dd"),
                DiaryTime = row.DiaryTime.ToString("HH:mm"),
                WeatherType = ToWeatherDisplayText(row.WeatherType),
                Visibility = row.Visibility == "shared" ? "已分享" : "私人",
                TemplateType = row.TemplateType,
                Title = row.TemplateType == "normal"
                    ? (string.IsNullOrWhiteSpace(row.DiaryNormal?.Title) ? "未命名日記" : row.DiaryNormal!.Title!)
                    : string.Empty,
                Body = row.TemplateType == "normal"
                    ? (string.IsNullOrWhiteSpace(row.DiaryNormal?.Body) ? "尚未輸入內容" : row.DiaryNormal!.Body!)
                    : string.Empty,
                MoodEmoji = row.TemplateType == "mood" ? string.Join(" ", row.Moods.Select(m => m.MoodEmoji)) : string.Empty,
                EnergyValue = row.TemplateType == "mood" ? row.DiaryMood?.EnergyValue : null,
                StressValue = row.TemplateType == "mood" ? row.DiaryMood?.StressValue : null,
                SleepValue = row.TemplateType == "mood" ? row.DiaryMood?.SleepValue : null,
                EventNote = row.TemplateType == "mood" ? row.DiaryMood?.EventNote ?? string.Empty : string.Empty,
                ThoughtNote = row.TemplateType == "mood" ? row.DiaryMood?.ThoughtNote ?? string.Empty : string.Empty,
                NeedNote = row.TemplateType == "mood" ? row.DiaryMood?.NeedNote ?? string.Empty : string.Empty
            };

            vm.MoodChips.AddRange(row.Moods.Select(m => m.MoodName));
            vm.TagName.AddRange(row.Tags.Select(t => t.TagName));
            vm.MediaUrl.AddRange(row.DiaryMedia.OrderBy(m => m.CreatedAt).Select(m => NormalizeMediaUrl(m.FileUrl)));

            // 建立「詳情頁前後篇」使用的排序清單。
            // 重點：這裡必須和 DiaryList() 使用同一套篩選與排序，
            // 才能做到「下一篇 = 列表頁目前看到的下一張卡片」。
            var detailNavQuery = _context.Diaries
                .AsNoTracking()
                .Where(d => d.UserId == userId.Value && d.Status == "published");

            // 套用從列表頁帶進來的篩選條件，例如：只看心情日記、只看已分享、關鍵字搜尋。
            detailNavQuery = ApplyDiaryListFilters(
                detailNavQuery,
                templateType,
                visibility,
                keyword,
                period,
                parsedStartDate,
                parsedEndDate,
                selectedTags);

            var orderedDiaryIds = ApplyDiaryListOrdering(detailNavQuery, sortOrder)
                .Select(d => (int)d.DiaryId)
                .ToList();

            var currentIndex = orderedDiaryIds.IndexOf((int)row.DiaryId);

            // currentIndex - 1：排序清單中的上一筆，也就是列表頁上方 / 前一張卡片。
            ViewBag.PrevId = currentIndex > 0
                ? orderedDiaryIds[currentIndex - 1]
                : (int?)null;

            // currentIndex + 1：排序清單中的下一筆，也就是列表頁下方 / 後一張卡片。
            ViewBag.NextId = currentIndex >= 0 && currentIndex < orderedDiaryIds.Count - 1
                ? orderedDiaryIds[currentIndex + 1]
                : (int?)null;

            return View(vm);
        }

        public IActionResult DiaryEdit(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            using var readTransaction = _context.Database.BeginTransaction(IsolationLevel.ReadUncommitted);

            var row = id > 0
                ? _context.Diaries
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(d => d.DiaryNormal)
                    .Include(d => d.DiaryMood)
                    .Include(d => d.DiaryMedia)
                    .Include(d => d.Moods)
                    .Include(d => d.Tags)
                    .FirstOrDefault(d => d.DiaryId == id && d.UserId == userId.Value && d.Status != "deleted")
                : null;

            var model = new Diary_Edit();

            if (row == null)
            {
                model.DiaryId = 0;
                model.TemplateType = "normal";
                model.DiaryDate = DateTime.Today.ToString("yyyy-MM-dd");
                model.DiaryTime = string.Empty;
                model.WeatherType = "sunny";
            }
            else
            {
                model.DiaryId = (int)row.DiaryId;
                model.TemplateType = row.TemplateType;
                model.DiaryDate = row.DiaryDate.ToString("yyyy-MM-dd");
                model.DiaryTime = row.DiaryTime.ToString("HH:mm");
                model.WeatherType = row.WeatherType ?? "sunny";
                model.Title = row.DiaryNormal?.Title ?? string.Empty;
                model.Body = row.DiaryNormal?.Body ?? string.Empty;
                model.TagName.AddRange(row.Tags.Select(t => t.TagName));
                model.MoodId.AddRange(row.Moods.Select(m => m.MoodId));
                model.EnergyValue = row.DiaryMood?.EnergyValue;
                model.StressValue = row.DiaryMood?.StressValue;
                model.SleepValue = row.DiaryMood?.SleepValue;
                model.EventNote = row.DiaryMood?.EventNote ?? string.Empty;
                model.ThoughtNote = row.DiaryMood?.ThoughtNote ?? string.Empty;
                model.NeedNote = row.DiaryMood?.NeedNote ?? string.Empty;
                model.MediaItems.AddRange(row.DiaryMedia
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new Diary_EditMediaItem
                    {
                        Kind = string.Equals(m.MediaType, "drawing", StringComparison.OrdinalIgnoreCase) ? "drawing" : "image",
                        Src = NormalizeMediaUrl(m.FileUrl)
                    }));
            }

            var activeTags = _context.Tags
                .AsNoTracking()
                .Where(t => t.IsActive && (t.UserId == null || t.UserId == userId.Value))
                .OrderBy(t => t.TagName)
                .Select(t => new { t.UserId, t.TagName })
                .ToList();

            model.SystemTagName.AddRange(activeTags
                .Where(t => t.UserId == null)
                .Select(t => t.TagName));

            model.UserCustomTagName.AddRange(activeTags
                .Where(t => t.UserId == userId.Value)
                .Select(t => t.TagName));

            foreach (var selectedTag in model.TagName)
            {
                var existsInSystem = model.SystemTagName.Contains(selectedTag);
                var existsInCustom = model.UserCustomTagName.Contains(selectedTag);
                if (!existsInSystem && !existsInCustom)
                {
                    model.UserCustomTagName.Add(selectedTag);
                }
            }

            model.MoodSelection.AddRange(_context.Moods
                .AsNoTracking()
                .OrderBy(m => m.MoodName)
                .Select(m => new Diary_MoodSelection
                {
                    MoodId = m.MoodId,
                    MoodName = m.MoodName,
                    MoodEmoji = m.MoodEmoji,
                    IsSelected = model.MoodId.Contains(m.MoodId)
                })
                .ToList());

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DiaryEdit(
            Diary_Edit model,
            string status,
            string visibility,
            string selectedTagsCsv,
            string deletedCustomTagsCsv,
            string selectedMoodIdsCsv,
            string mediaItemsJson)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var templateType = model.TemplateType == "mood" ? "mood" : "normal";
            var diaryStatus = string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase) ? "draft" : "published";
            var diaryVisibility = string.Equals(visibility, "shared", StringComparison.OrdinalIgnoreCase) ? "shared" : "private";
            var hasValidDiaryDate = DateOnly.TryParse(model.DiaryDate, out var parsedDiaryDate);

            var timeText = string.IsNullOrWhiteSpace(model.DiaryTime) ? "00:00" : model.DiaryTime.Trim();
            if (timeText == "24:00") timeText = "23:59";
            var hasValidDiaryTime = TimeOnly.TryParse(timeText, out var parsedDiaryTime);

            var now = DateTime.Now;
            var row = model.DiaryId > 0
                ? _context.Diaries
                    .Include(d => d.DiaryNormal)
                    .Include(d => d.DiaryMood)
                    .Include(d => d.DiaryMedia)
                    .Include(d => d.Tags)
                    .Include(d => d.Moods)
                    .FirstOrDefault(d => d.DiaryId == model.DiaryId && d.UserId == userId.Value && d.Status != "deleted")
                : null;

            var isNew = row == null;
            if (isNew)
            {
                row = new Diary { UserId = userId.Value, CreatedAt = now };
                _context.Diaries.Add(row);
            }

            row!.TemplateType = templateType;
            row.DiaryDate = hasValidDiaryDate ? parsedDiaryDate : (isNew ? DateOnly.FromDateTime(DateTime.Today) : row.DiaryDate);
            row.DiaryTime = hasValidDiaryTime ? parsedDiaryTime : (isNew ? new TimeOnly(0, 0, 0) : row.DiaryTime);
            row.WeatherType = string.IsNullOrWhiteSpace(model.WeatherType) ? "sunny" : model.WeatherType.Trim();
            row.Visibility = diaryVisibility;
            row.Status = diaryStatus;
            row.UpdatedAt = now;

            var normalBody = (model.Body ?? string.Empty).Trim();
            var moodEvent = (model.EventNote ?? string.Empty).Trim();
            var moodThought = (model.ThoughtNote ?? string.Empty).Trim();
            var moodNeed = (model.NeedNote ?? string.Empty).Trim();

            if (templateType == "normal")
            {
                row.DiaryNormal ??= new DiaryNormal();
                row.DiaryNormal.Title = (model.Title ?? string.Empty).Trim();
                row.DiaryNormal.Body = normalBody;
                if (row.DiaryMood != null) _context.DiaryMoods.Remove(row.DiaryMood);
            }
            else
            {
                row.DiaryMood ??= new DiaryMood();
                row.DiaryMood.EventNote = moodEvent;
                row.DiaryMood.ThoughtNote = moodThought;
                row.DiaryMood.NeedNote = moodNeed;
                row.DiaryMood.EnergyValue = model.EnergyValue.HasValue ? (byte?)model.EnergyValue.Value : null;
                row.DiaryMood.StressValue = model.StressValue.HasValue ? (byte?)model.StressValue.Value : null;
                row.DiaryMood.SleepValue = model.SleepValue.HasValue ? (byte?)model.SleepValue.Value : null;
                if (row.DiaryNormal != null) _context.DiaryNormals.Remove(row.DiaryNormal);
            }

            var moodPreviewParts = new[] { moodEvent, moodThought, moodNeed }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var computedPreview = templateType == "mood" ? string.Join("；", moodPreviewParts) : normalBody;
            row.PreviewText = string.IsNullOrWhiteSpace(computedPreview)
                ? "尚未輸入內容"
                : (computedPreview.Length > 300 ? computedPreview[..300] : computedPreview);

            var tagNames = (selectedTagsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var deletedCustomTagNames = (deletedCustomTagsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preservedDeletedTags = deletedCustomTagNames.Count == 0
                ? new List<Tag>()
                : row.Tags
                    .Where(t => t.UserId == userId.Value && deletedCustomTagNames.Contains(t.TagName, StringComparer.OrdinalIgnoreCase))
                    .ToList();

            if (deletedCustomTagNames.Count > 0)
            {
                var deletedTags = _context.Tags
                    .Where(t => t.UserId == userId.Value && t.IsActive)
                    .AsEnumerable()
                    .Where(t => deletedCustomTagNames.Contains(t.TagName, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                foreach (var deletedTag in deletedTags)
                {
                    deletedTag.IsActive = false;
                }

                tagNames = tagNames
                    .Where(t => !deletedCustomTagNames.Contains(t, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            row.Tags.Clear();
            foreach (var preservedTag in preservedDeletedTags)
            {
                row.Tags.Add(preservedTag);
            }

            if (tagNames.Count > 0)
            {
                var tagPool = _context.Tags
                    .Where(t => t.IsActive && (t.UserId == null || t.UserId == userId.Value))
                    .ToList();

                foreach (var tagName in tagNames)
                {
                    var tag = tagPool.FirstOrDefault(t => string.Equals(t.TagName, tagName, StringComparison.OrdinalIgnoreCase));
                    if (tag == null)
                    {
                        tag = new Tag
                        {
                            TagId = CreateTagId(),
                            UserId = userId.Value,
                            TagName = tagName,
                            TagType = "custom",
                            CreatedAt = now,
                            IsActive = true
                        };
                        _context.Tags.Add(tag);
                        tagPool.Add(tag);
                    }
                    row.Tags.Add(tag);
                }
            }

            row.Moods.Clear();
            if (templateType == "mood")
            {
                var moodIds = (selectedMoodIdsCsv ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (moodIds.Count > 0)
                {
                    var moods = _context.Moods.Where(m => moodIds.Contains(m.MoodId)).ToList();
                    foreach (var mood in moods) row.Moods.Add(mood);
                }
            }

            var normalizedMedia = BuildPersistedMediaItems(mediaItemsJson, row.DiaryDate);
            AppendMediaDebugLog($"normalizedMedia count={normalizedMedia.Count}");

            var oldMedia = row.DiaryMedia.ToList();
            if (oldMedia.Count > 0)
            {
                _context.DiaryMedia.RemoveRange(oldMedia);
            }

            foreach (var media in normalizedMedia)
            {
                row.DiaryMedia.Add(media);
            }

            _context.SaveChanges();
            AppendMediaDebugLog($"SaveChanges done. diaryId={row.DiaryId}, mediaCount={normalizedMedia.Count}");

            if (isNew)
            {
                var reactionTypes = new[] { "like", "love", "hug", "empathy", "cheer" };
                foreach (var reactionType in reactionTypes)
                {
                    _context.PostReactionCounts.Add(new PostReactionCount
                    {
                        DiaryId = row.DiaryId,
                        ReactionType = reactionType,
                        Count = 0,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                _context.SaveChanges();
            }

            if (diaryStatus == "draft")
            {
                return RedirectToAction(nameof(DiaryList));
            }

            return RedirectToAction(nameof(DiaryDetail), new { id = row.DiaryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleShare(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            if (id <= 0) return RedirectToAction(nameof(DiaryList));

            var row = _context.Diaries
                .FirstOrDefault(d => d.DiaryId == id && d.UserId == userId.Value && d.Status != "deleted");

            if (row == null) return NotFound();

            row.Visibility = row.Visibility == "shared" ? "private" : "shared";
            row.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            return RedirectToAction(nameof(DiaryDetail), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteDiary(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            if (id <= 0) return RedirectToAction(nameof(DiaryList));

            var row = _context.Diaries
                .FirstOrDefault(d => d.DiaryId == id && d.UserId == userId.Value && d.Status != "deleted");

            if (row == null) return NotFound();

            row.Status = "deleted";
            row.DeletedAt = DateTime.Now;
            row.UpdatedAt = DateTime.Now;
            _context.SaveChanges();

            return RedirectToAction(nameof(DiaryList));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteDiaries(List<int> ids)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToLogin();
            }

            var validIds = (ids ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (validIds.Count == 0)
            {
                return BadRequest(new { ok = false, message = "未選擇要刪除的日記。" });
            }

            var now = DateTime.Now;
            var rows = _context.Diaries
                .Where(d => d.UserId == userId.Value && d.Status != "deleted" && validIds.Contains((int)d.DiaryId))
                .ToList();

            foreach (var row in rows)
            {
                row.Status = "deleted";
                row.DeletedAt = now;
                row.UpdatedAt = now;
            }

            _context.SaveChanges();
            return Ok(new { ok = true, deletedCount = rows.Count });
        }

        /// <summary>
        /// 套用日記列表頁與詳情頁前後篇共用的篩選條件。
        /// 只要篩選規則集中在這裡，DiaryList 和 DiaryDetail 就不容易出現排序 / 篩選不一致。
        /// </summary>
        private static IQueryable<Diary> ApplyDiaryListFilters(
            IQueryable<Diary> query,
            string? templateType,
            string? visibility,
            string? keyword,
            string? period,
            DateOnly? startDate,
            DateOnly? endDate,
            List<string> selectedTags)
        {
            // 篩選日記模板：normal = 一般日記、mood = 心情日記。
            if (!string.IsNullOrWhiteSpace(templateType))
            {
                query = query.Where(d => d.TemplateType == templateType);
            }

            // 篩選分享狀態：private = 私人、shared = 已分享。
            if (!string.IsNullOrWhiteSpace(visibility))
            {
                query = query.Where(d => d.Visibility == visibility);
            }

            // 標籤篩選：前端是「勾選任一標籤即符合」，所以後端也使用 Any。
            if (selectedTags.Count > 0)
            {
                query = query.Where(d => d.Tags.Any(t => selectedTags.Contains(t.TagName)));
            }

            // 期間篩選：讓從詳情頁返回列表、或詳情頁上一篇 / 下一篇時，都能套用同樣日期範圍。
            if (period == "week")
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var offset = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
                var weekStart = today.AddDays(-offset);
                var weekEnd = weekStart.AddDays(6);
                query = query.Where(d => d.DiaryDate >= weekStart && d.DiaryDate <= weekEnd);
            }
            else if (period == "month")
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var monthStart = new DateOnly(today.Year, today.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                query = query.Where(d => d.DiaryDate >= monthStart && d.DiaryDate <= monthEnd);
            }
            else if (period == "range" && startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(d => d.DiaryDate >= startDate.Value && d.DiaryDate <= endDate.Value);
            }

            // 關鍵字搜尋：同時搜尋一般日記標題 / 內容、心情日記三個引導欄位，以及標籤名稱。
            // 日期文字搜尋主要交給前端 JS 處理；後端這裡保留內容與標籤搜尋，避免 LINQ 轉 SQL 時出現 ToString 無法轉譯問題。
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(d =>
                    (d.PreviewText != null && d.PreviewText.Contains(keyword)) ||
                    d.Tags.Any(t => t.TagName.Contains(keyword)) ||
                    (d.DiaryNormal != null &&
                        ((d.DiaryNormal.Title != null && d.DiaryNormal.Title.Contains(keyword)) ||
                         (d.DiaryNormal.Body != null && d.DiaryNormal.Body.Contains(keyword)))) ||
                    (d.DiaryMood != null &&
                        ((d.DiaryMood.EventNote != null && d.DiaryMood.EventNote.Contains(keyword)) ||
                         (d.DiaryMood.ThoughtNote != null && d.DiaryMood.ThoughtNote.Contains(keyword)) ||
                         (d.DiaryMood.NeedNote != null && d.DiaryMood.NeedNote.Contains(keyword)))));
            }

            return query;
        }

        /// <summary>
        /// 套用列表頁排序。
        /// desc：新到舊；asc：舊到新。
        /// DiaryId 是最後排序依據，避免同日期、同時間時順序不穩定。
        /// </summary>
        private static IOrderedQueryable<Diary> ApplyDiaryListOrdering(IQueryable<Diary> query, string? sortOrder)
        {
            return sortOrder == "asc"
                ? query.OrderBy(d => d.DiaryDate)
                    .ThenBy(d => d.DiaryTime)
                    .ThenBy(d => d.DiaryId)
                : query.OrderByDescending(d => d.DiaryDate)
                    .ThenByDescending(d => d.DiaryTime)
                    .ThenByDescending(d => d.DiaryId);
        }

        /// <summary>
        /// 只允許合法的模板篩選值，避免網址亂帶參數時影響查詢。
        /// </summary>
        private static string? NormalizeTemplateType(string? templateType)
        {
            var value = (templateType ?? string.Empty).Trim().ToLowerInvariant();
            return value is "normal" or "mood" ? value : null;
        }

        /// <summary>
        /// 只允許合法的分享狀態篩選值。
        /// </summary>
        private static string? NormalizeVisibility(string? visibility)
        {
            var value = (visibility ?? string.Empty).Trim().ToLowerInvariant();
            return value is "private" or "shared" ? value : null;
        }

        /// <summary>
        /// 只允許合法排序值；沒有指定時預設由新到舊。
        /// </summary>
        private static string NormalizeSortOrder(string? sortOrder)
        {
            var value = (sortOrder ?? string.Empty).Trim().ToLowerInvariant();
            return value == "asc" ? "asc" : "desc";
        }

        /// <summary>
        /// 只允許合法期間值；沒有指定時視為全部。
        /// </summary>
        private static string NormalizePeriod(string? period)
        {
            var value = (period ?? string.Empty).Trim().ToLowerInvariant();
            return value is "week" or "month" or "range" ? value : "all";
        }

        /// <summary>
        /// 整理搜尋關鍵字：去除前後空白，空字串就當作沒有搜尋。
        /// </summary>
        private static string? NormalizeKeyword(string? keyword)
        {
            var value = (keyword ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// 整理多選標籤：網址會用逗號分隔，例如：工作,家庭。
        /// </summary>
        private static List<string> NormalizeTags(string? tags)
        {
            return (tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 將 yyyy-MM-dd 轉成 DateOnly；格式錯誤就忽略。
        /// </summary>
        private static DateOnly? NormalizeDateOnly(string? dateText)
        {
            return DateOnly.TryParse((dateText ?? string.Empty).Trim(), out var date)
                ? date
                : null;
        }

        private List<DiaryMedium> BuildPersistedMediaItems(string? mediaItemsJson, DateOnly diaryDate)
        {
            if (string.IsNullOrWhiteSpace(mediaItemsJson))
            {
                AppendMediaDebugLog("mediaItemsJson is empty.");
                return new List<DiaryMedium>();
            }

            AppendMediaDebugLog($"mediaItemsJson length={mediaItemsJson.Length}");

            List<Diary_EditMediaItem>? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<List<Diary_EditMediaItem>>(
                    mediaItemsJson,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                AppendMediaDebugLog($"parsed count={(parsed?.Count ?? 0)}");
            }
            catch (Exception ex)
            {
                parsed = null;
                AppendMediaDebugLog($"deserialize failed: {ex.GetType().Name} {ex.Message}");
            }

            if (parsed == null || parsed.Count == 0)
            {
                AppendMediaDebugLog("parsed is null or count=0.");
                return new List<DiaryMedium>();
            }

            var result = new List<DiaryMedium>();
            var now = DateTime.Now;

            foreach (var item in parsed.Take(10))
            {
                var kind = string.Equals(item.Kind, "drawing", StringComparison.OrdinalIgnoreCase) ? "drawing" : "image";
                var src = (item.Src ?? string.Empty).Trim();
                AppendMediaDebugLog($"item kind={kind}, srcPrefix={(src.Length > 30 ? src[..30] : src)}");
                if (string.IsNullOrWhiteSpace(src)) continue;

                string? fileUrl = null;
                if (TrySaveDataUrlToFile(src, kind, diaryDate, out var savedUrl))
                {
                    fileUrl = savedUrl;
                    AppendMediaDebugLog($"saved as dataUrl => {fileUrl}");
                }
                else if (IsPersistedMediaPath(src))
                {
                    fileUrl = NormalizeMediaUrl(src);
                    AppendMediaDebugLog($"kept persisted path => {fileUrl}");
                }

                if (string.IsNullOrWhiteSpace(fileUrl)) continue;

                result.Add(new DiaryMedium
                {
                    MediaId = CreateMediaId(),
                    MediaType = kind,
                    FileUrl = fileUrl,
                    CreatedAt = now
                });
            }

            return result;
        }

        private bool TrySaveDataUrlToFile(string dataUrl, string mediaKind, DateOnly diaryDate, out string relativeUrl)
        {
            relativeUrl = string.Empty;
            const string marker = ";base64,";
            var markerIndex = dataUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (!dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || markerIndex < 0) return false;

            var mime = dataUrl[5..markerIndex];
            var base64 = dataUrl[(markerIndex + marker.Length)..];

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch
            {
                return false;
            }

            var isDrawing = string.Equals(mediaKind, "drawing", StringComparison.OrdinalIgnoreCase);
            var extension = isDrawing
                ? ".png"
                : mime.ToLowerInvariant() switch
                {
                    "image/png" => ".png",
                    "image/jpeg" => ".jpg",
                    "image/jpg" => ".jpg",
                    "image/webp" => ".webp",
                    _ => ".png"
                };

            var relativeFolder = isDrawing ? "drawing" : "image";
            var absoluteFolder = Path.Combine(_env.WebRootPath, relativeFolder);
            Directory.CreateDirectory(absoluteFolder);

            var fileName = isDrawing
                ? CreateDrawingFileName(absoluteFolder, diaryDate)
                : $"media_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(absoluteFolder, fileName);
            System.IO.File.WriteAllBytes(absolutePath, bytes);
            AppendMediaDebugLog($"write bytes ok: {absolutePath}, bytes={bytes.Length}");

            relativeUrl = "/" + Path.Combine(relativeFolder, fileName).Replace("\\", "/");
            return true;
        }

        private static string CreateDrawingFileName(string absoluteFolder, DateOnly diaryDate)
        {
            var baseName = $"media_{diaryDate:yyyyMMdd}";
            var fileName = $"{baseName}.png";
            var absolutePath = Path.Combine(absoluteFolder, fileName);
            var index = 2;

            while (System.IO.File.Exists(absolutePath))
            {
                fileName = $"{baseName}_{index}.png";
                absolutePath = Path.Combine(absoluteFolder, fileName);
                index++;
            }

            return fileName;
        }

        private void AppendMediaDebugLog(string message)
        {
            try
            {
                var webRoot = _env.WebRootPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(webRoot)) return;

                var folder = Path.Combine(webRoot, "drawing");
                Directory.CreateDirectory(folder);
                var logPath = Path.Combine(folder, "_save_debug.log");
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
                System.IO.File.AppendAllText(logPath, line);
            }
            catch
            {
            }
        }

        private static bool IsPersistedMediaPath(string src)
        {
            var normalized = NormalizeMediaUrl(src);
            return normalized.StartsWith("/image/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("~/image/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("/drawing/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("~/drawing/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("/uploads/diary/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("~/uploads/diary/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMediaUrl(string? fileUrl)
        {
            var raw = (fileUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }

            if (raw.StartsWith("~/", StringComparison.Ordinal)) return raw;
            return raw.StartsWith("/", StringComparison.Ordinal) ? raw : $"/{raw}";
        }

        private static string CreateTagId()
        {
            return $"tag{Guid.NewGuid():N}"[..20];
        }

        private static string CreateMediaId()
        {
            return $"md{Guid.NewGuid():N}"[..20];
        }

        private static string ToReactionEmoji(string? reactionType)
        {
            var key = (reactionType ?? string.Empty).Trim().ToLowerInvariant();
            return key switch
            {
                "like" => "👍",
                "love" => "❤️",
                "hug" => "🤗",
                "empathy" => "🥺",
                "cheer" => "👏",
                _ => string.IsNullOrWhiteSpace(reactionType) ? "?" : reactionType!
            };
        }

        private static string ToWeatherDisplayText(string? weatherType)
        {
            return weatherType switch
            {
                "sunny" => "晴天",
                "cloudy" => "陰天",
                "rainy" => "雨天",
                _ => weatherType ?? string.Empty
            };
        }
    }
}
