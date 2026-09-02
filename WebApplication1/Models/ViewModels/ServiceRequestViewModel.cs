using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.ViewModels
{
    public class ServiceRequestViewModel
    {
        [Required]
        [Display(Name = "Request Type")]
        public RequestType RequestType { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Description must be between {2} and {1} characters.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;
    }
}
