using System;
using Microsoft.AspNetCore.Identity;

namespace AdministrativeServices.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? CCCD { get; set; }
        
        // Detailed Address
        public string? Street { get; set; } // Số nhà, Tên đường (Optional)
        public string? Ward { get; set; } // Phường/Xã (Required)
        public string? District { get; set; } // Quận/Huyện (Required)
        public string? City { get; set; } // Tỉnh/Thành phố (Required)
        
        // Combined Address helper
        public string FullAddress => $"{Street}, {Ward}, {District}, {City}".Trim(',', ' ');

        public string? Department { get; set; }
        public string? Position { get; set; }

        // Link to verified citizen record
        public int? CitizenProfileId { get; set; }
        public Citizen? CitizenProfile { get; set; }

        public IdentityVerificationStatus IdentityStatus { get; set; } = IdentityVerificationStatus.None;
    }

    public enum IdentityVerificationStatus
    {
        None,
        Pending,
        Verified,
        Rejected,
        SupplementRequired
    }

    public enum ApplicationStatus
    {
        Draft,
        AwaitingConfirmation,
        Submitted, // Chờ phân công
        Processing, // Đang xử lý (Đã phân công)
        PendingSignature, // Chờ ký duyệt
        SupplementRequired,
        Signed,
        Completed,
        Rejected
    }

    public class ServiceType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Fee { get; set; }
    }

    public class Application
    {
        public int Id { get; set; }
        public string CitizenId { get; set; } = string.Empty;
        public ApplicationUser? Citizen { get; set; }
        
        public int ServiceTypeId { get; set; }
        public ServiceType? ServiceType { get; set; }

        public string ContentJson { get; set; } = string.Empty;
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }

        // Assignment Info
        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }
        public DateTime? AssignedDate { get; set; }

        public string? CurrentOfficialId { get; set; } // Deprecated or alias for AssignedToUserId? Let's keep for backup or simplify.
        // Actually AssignedToUser is better. CurrentOfficialId was just a placeholder.

        public string? RejectReason { get; set; }
        public string? SupplementNote { get; set; }

        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<ApplicationHistory> History { get; set; } = new List<ApplicationHistory>();
    }

    public class Attachment
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
    }

    public class ApplicationHistory
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public DateTime ChangeDate { get; set; } = DateTime.UtcNow;
        public ApplicationStatus Status { get; set; }
        public string Note { get; set; } = string.Empty;
        public string ChangedById { get; set; } = string.Empty;
        public ApplicationUser? ChangedBy { get; set; }
    }

    /// <summary>
    /// Confirmation request for spouse (marriage) or owner (temporary residence)
    /// </summary>
    public class ConfirmationRequest
    {
        public int Id { get; set; }
        
        public int ApplicationId { get; set; }
        public Application? Application { get; set; }
        
        // User who submitted the application
        public string RequesterId { get; set; } = string.Empty;
        public ApplicationUser? Requester { get; set; }
        
        // User who needs to confirm (spouse or owner)
        public string TargetUserId { get; set; } = string.Empty;
        public ApplicationUser? TargetUser { get; set; }
        
        // Also store CCCD for lookup
        public string TargetCCCD { get; set; } = string.Empty;
        
        // Type: Marriage, TemporaryResidence
        public string RequestType { get; set; } = string.Empty;
        
        // Status: Pending, Confirmed, Rejected
        public string Status { get; set; } = "Pending";
        public string? RejectReason { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ResponseDate { get; set; }
    }
}
