using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    public class ServiceRequest
    {
        public int Id { get; set; }

        [Required]
        public string StudentId { get; set; } = string.Empty;

        [ForeignKey("StudentId")]
        public ApplicationUser? Student { get; set; }

        [Required]
        [Display(Name = "Request Type")]
        public RequestType RequestType { get; set; }

        [Required]
        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        [Display(Name = "Submitted On")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
