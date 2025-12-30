using System;
using Microsoft.AspNetCore.Identity;

namespace AdministrativeServices.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? CCCD { get; set; }
        public string? Address { get; set; }
        // For Officials
        public string? Department { get; set; }
        public string? Position { get; set; }
    }

    public enum ApplicationStatus
    {
        Draft,
        AwaitingConfirmation, // Waiting for User B (spouse/owner) to confirm
        Submitted,
        InReview,
        SupplementRequired,
        PendingApproval,
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

        public string ContentJson { get; set; } = string.Empty; // Store form data as JSON
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedDate { get; set; }

        public string? CurrentOfficialId { get; set; }
        public ApplicationUser? CurrentOfficial { get; set; }

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
        public string DocumentType { get; set; } = string.Empty; // e.g., "CT01", "CCCD_Scan"
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
