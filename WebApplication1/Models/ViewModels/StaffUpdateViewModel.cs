using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.ViewModels
{
    public class StaffUpdateViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Status")]
        public RequestStatus Status { get; set; }
    }
}
