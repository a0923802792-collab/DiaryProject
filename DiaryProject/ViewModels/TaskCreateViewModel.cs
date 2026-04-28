using System.ComponentModel.DataAnnotations;
namespace DiaryProject.ViewModels
{
    public class TaskCreateViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "任務名稱")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "任務節奏類型")]
        public string RhythmType { get; set; } = string.Empty;

        [Display(Name = "每週目標次數")]
        [Range(1,7,ErrorMessage ="每周目標次數需介於1到7之間")]
        public int? WeeklyTargetCount { get; set; }
    }
}
