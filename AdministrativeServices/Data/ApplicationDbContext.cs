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

        public DbSet<Application> Applications { get; set; }
        public DbSet<ServiceType> ServiceTypes { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<ApplicationHistory> ApplicationHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships if needed
            builder.Entity<Application>()
                .HasOne(a => a.Citizen)
                .WithMany()
                .HasForeignKey(a => a.CitizenId);

            builder.Entity<Application>()
                .HasOne(a => a.CurrentOfficial)
                .WithMany()
                .HasForeignKey(a => a.CurrentOfficialId);
        }
    }
}
