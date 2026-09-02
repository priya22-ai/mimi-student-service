using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.ViewModels
{
    public class ServiceRequestDetailViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Request Type")]
        public RequestType RequestType { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public RequestStatus Status { get; set; }

        [Display(Name = "Submitted On")]
        public DateTime CreatedDate { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime UpdatedDate { get; set; }

        [Display(Name = "Student Name")]
        public string StudentName { get; set; } = string.Empty;

        [Display(Name = "Student Email")]
        public string StudentEmail { get; set; } = string.Empty;
    }
}
