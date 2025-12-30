using System;
using System.Collections.Generic;

namespace AdministrativeServices.Models
{
    /// <summary>
    /// Master citizen record - represents a person in the national database
    /// </summary>
    public class Citizen
    {
        public int Id { get; set; }
        public string CCCD { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty; // Nam/Nữ
        public string PlaceOfBirth { get; set; } = string.Empty;
        public string Ethnicity { get; set; } = "Kinh";
        public string Nationality { get; set; } = "Việt Nam";
        
        // Marital status: Chưa kết hôn, Đã kết hôn, Ly hôn, Góa
        public string MaritalStatus { get; set; } = "Chưa kết hôn";
        
        // Current residence
        public int? CurrentHouseholdId { get; set; }
        public HouseholdRegistry? CurrentHousehold { get; set; }
        
        // Parent references (nullable for unknown parents)
        public int? FatherId { get; set; }
        public Citizen? Father { get; set; }
        public int? MotherId { get; set; }
        public Citizen? Mother { get; set; }
        
        // Navigation
        public ICollection<BirthRecord> BirthRecordsAsChild { get; set; } = new List<BirthRecord>();
        public ICollection<MarriageRecord> MarriagesAsSpouse1 { get; set; } = new List<MarriageRecord>();
        public ICollection<MarriageRecord> MarriagesAsSpouse2 { get; set; } = new List<MarriageRecord>();
    }

    /// <summary>
    /// Marriage record linking two citizens
    /// </summary>
    public class MarriageRecord
    {
        public int Id { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        
        public int Spouse1Id { get; set; }
        public Citizen? Spouse1 { get; set; }
        
        public int Spouse2Id { get; set; }
        public Citizen? Spouse2 { get; set; }
        
        public DateTime MarriageDate { get; set; }
        public string RegistrationPlace { get; set; } = string.Empty;
        
        // Status: Active, Divorced
        public string Status { get; set; } = "Active";
        public DateTime? DivorceDate { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Birth record for a child
    /// </summary>
    public class BirthRecord
    {
        public int Id { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        public string GeneratedCitizenId { get; set; } = string.Empty; // Auto-generated ID for child
        
        // Child info
        public string ChildFullName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string PlaceOfBirth { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        
        // Link to citizen record if created
        public int? ChildCitizenId { get; set; }
        public Citizen? ChildCitizen { get; set; }
        
        // Parents
        public int? FatherId { get; set; }
        public Citizen? Father { get; set; }
        public int? MotherId { get; set; }
        public Citizen? Mother { get; set; }
        
        // Or parent CCCDs if not in system
        public string? FatherCCCD { get; set; }
        public string? FatherName { get; set; }
        public string? MotherCCCD { get; set; }
        public string? MotherName { get; set; }
        
        // Marriage verification
        public bool ParentsMarriageVerified { get; set; } = false;
        public int? ParentMarriageRecordId { get; set; }
        
        // Registration info
        public string RegistrationPlace { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public string? SignedByOfficialId { get; set; }
        public string? SignedByChairmanId { get; set; }
        public DateTime? SignedDate { get; set; }
    }

    /// <summary>
    /// Household registry (Sổ hộ khẩu điện tử)
    /// </summary>
    public class HouseholdRegistry
    {
        public int Id { get; set; }
        public string HouseholdNumber { get; set; } = string.Empty; // Số sổ hộ khẩu
        
        // Owner
        public int OwnerId { get; set; }
        public Citizen? Owner { get; set; }
        
        // Address
        public string Address { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty; // Phường/Xã
        public string District { get; set; } = string.Empty; // Quận/Huyện
        public string Province { get; set; } = string.Empty; // Tỉnh/Thành phố
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        
        // Members
        public ICollection<HouseholdMember> Members { get; set; } = new List<HouseholdMember>();
    }

    /// <summary>
    /// Household member linking citizens to households
    /// </summary>
    public class HouseholdMember
    {
        public int Id { get; set; }
        
        public int HouseholdId { get; set; }
        public HouseholdRegistry? Household { get; set; }
        
        public int CitizenId { get; set; }
        public Citizen? Citizen { get; set; }
        
        // Relationship to owner: Chủ hộ, Vợ/Chồng, Con, Bố/Mẹ, Anh/Chị/Em, Khác
        public string RelationshipToOwner { get; set; } = string.Empty;
        
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public DateTime? LeaveDate { get; set; }
        public bool IsCurrentMember { get; set; } = true;
    }

    /// <summary>
    /// Temporary Residence Registration (Đăng ký tạm trú)
    /// </summary>
    public class TemporaryResidence
    {
        public int Id { get; set; }
        public string RegistrationNumber { get; set; } = string.Empty;
        
        // Citizen info
        public string CitizenCCCD { get; set; } = string.Empty;
        public string CitizenName { get; set; } = string.Empty;
        public string CitizenPhone { get; set; } = string.Empty;
        public int? CitizenId { get; set; }
        public Citizen? Citizen { get; set; }
        
        // Address
        public string Address { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        
        // Duration
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; } // Max 2 years
        
        // Owner/Landlord info
        public string OwnerCCCD { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public bool OwnerConfirmed { get; set; } = false;
        public DateTime? OwnerConfirmedDate { get; set; }
        
        // Registration type: New, Extend, Update
        public string RegistrationType { get; set; } = "New";
        
        // Status: Active, Expired, Cancelled
        public string Status { get; set; } = "Active";
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string? SignedByOfficialId { get; set; }
        public DateTime? SignedDate { get; set; }
        
        // For expiration notification
        public bool ExpirationNotified { get; set; } = false;
    }
}
