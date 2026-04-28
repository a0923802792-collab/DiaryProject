using System;
using System.ComponentModel.DataAnnotations;
namespace DiaryProject.ViewModels
{
    public class TaskCheckinViewModel
    {

        [Required]
        public int TaskId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "打卡日期")]
        public DateTime CheckinDate { get; set; }

        [Required]
        [Display(Name = "打卡類型")]
        public string CheckinType { get; set; } = "Complete";
    }
}
