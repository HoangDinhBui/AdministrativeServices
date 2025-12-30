using AdministrativeServices.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AdministrativeServices.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing
        public DbSet<Application> Applications { get; set; }
        public DbSet<ServiceType> ServiceTypes { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<ApplicationHistory> ApplicationHistories { get; set; }

        // New: Citizen records
        public DbSet<Citizen> Citizens { get; set; }
        public DbSet<MarriageRecord> MarriageRecords { get; set; }
        public DbSet<BirthRecord> BirthRecords { get; set; }
        public DbSet<HouseholdRegistry> HouseholdRegistries { get; set; }
        public DbSet<HouseholdMember> HouseholdMembers { get; set; }
        public DbSet<TemporaryResidence> TemporaryResidences { get; set; }
        public DbSet<ConfirmationRequest> ConfirmationRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Application relationships
            builder.Entity<Application>()
                .HasOne(a => a.Citizen)
                .WithMany()
                .HasForeignKey(a => a.CitizenId);

            builder.Entity<Application>()
                .HasOne(a => a.CurrentOfficial)
                .WithMany()
                .HasForeignKey(a => a.CurrentOfficialId);

            // Citizen unique index
            builder.Entity<Citizen>()
                .HasIndex(c => c.CCCD)
                .IsUnique();

            // Marriage relationships
            builder.Entity<MarriageRecord>()
                .HasOne(m => m.Spouse1)
                .WithMany(c => c.MarriagesAsSpouse1)
                .HasForeignKey(m => m.Spouse1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MarriageRecord>()
                .HasOne(m => m.Spouse2)
                .WithMany(c => c.MarriagesAsSpouse2)
                .HasForeignKey(m => m.Spouse2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Birth record relationships
            builder.Entity<BirthRecord>()
                .HasOne(b => b.Father)
                .WithMany()
                .HasForeignKey(b => b.FatherId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BirthRecord>()
                .HasOne(b => b.Mother)
                .WithMany()
                .HasForeignKey(b => b.MotherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Household relationships
            builder.Entity<HouseholdRegistry>()
                .HasOne(h => h.Owner)
                .WithMany()
                .HasForeignKey(h => h.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<HouseholdMember>()
                .HasOne(hm => hm.Household)
                .WithMany(h => h.Members)
                .HasForeignKey(hm => hm.HouseholdId);

            builder.Entity<HouseholdMember>()
                .HasOne(hm => hm.Citizen)
                .WithMany()
                .HasForeignKey(hm => hm.CitizenId)
                .OnDelete(DeleteBehavior.Restrict);

            // Citizen parent relationships
            builder.Entity<Citizen>()
                .HasOne(c => c.Father)
                .WithMany()
                .HasForeignKey(c => c.FatherId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Citizen>()
                .HasOne(c => c.Mother)
                .WithMany()
                .HasForeignKey(c => c.MotherId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
