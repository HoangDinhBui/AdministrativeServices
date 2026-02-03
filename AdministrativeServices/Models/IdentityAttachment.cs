using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministrativeServices.Models
{
    public class IdentityAttachment
    {
        public int Id { get; set; }

        public int IdentityVerificationRequestId { get; set; }
        [ForeignKey("IdentityVerificationRequestId")]
        public IdentityVerificationRequest? Request { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty; // image/jpeg, application/pdf
        
        // Type: "Front", "Back", "Portrait", "Other"
        public string DocumentType { get; set; } = "Other";

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    }
}
