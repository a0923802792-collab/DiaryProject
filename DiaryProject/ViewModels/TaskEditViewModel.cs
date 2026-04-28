using System.ComponentModel.DataAnnotations;

namespace DiaryProject.ViewModels
{
    public class TaskEditViewModel
    {
        [Required]
        public int TaskId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "任務名稱")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "執行節奏")]
        public string RhythmType { get; set; } = string.Empty;

        [Display(Name = "每週目標次數")]
        public int? WeeklyTargetCount { get; set; }
    }
}