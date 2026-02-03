using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministrativeServices.Models
{
    public class IdentityVerificationRequest
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        [Display(Name = "Số CCCD/CMND")]
        public string CCCD { get; set; } = string.Empty;

        [Display(Name = "Ảnh mặt trước")]
        public string FrontImage { get; set; } = string.Empty; // Legacy
        [Display(Name = "Ảnh mặt sau")]
        public string BackImage { get; set; } = string.Empty; // Legacy
        [Display(Name = "Ảnh chân dung")]
        public string PortraitImage { get; set; } = string.Empty; // Legacy

        public ICollection<IdentityAttachment> Attachments { get; set; } = new List<IdentityAttachment>();

        public IdentityVerificationStatus Status { get; set; } = IdentityVerificationStatus.Pending;

        public string? RejectReason { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedDate { get; set; }
        
        public string? ProcessedByUserId { get; set; }
        [ForeignKey("ProcessedByUserId")]
        public ApplicationUser? ProcessedBy { get; set; }
    }
}
