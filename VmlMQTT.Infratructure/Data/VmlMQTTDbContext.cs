using Microsoft.EntityFrameworkCore;
using Npgsql;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Infratructure.Data
{
    public class VmlMQTTDbContext : DbContext
    {
        public VmlMQTTDbContext(DbContextOptions<VmlMQTTDbContext> options) : base(options)
        {
            NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserDeviceId> UserDeviceIds { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<EmqxBrokerHost> EmqxBrokerHosts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.VMLUserId);
                entity.Property(e => e.VMLUserId).ValueGeneratedNever();
                entity.Property(e => e.Phone).IsRequired();

                entity.HasMany(e => e.UserDeviceIds)
                    .WithOne(e => e.User)
                    .HasForeignKey(e => e.UserId)
                    .HasPrincipalKey(e => e.VMLUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.UserSessions)
                    .WithOne(e => e.User)
                    .HasForeignKey(e => e.UserId)
                    .HasPrincipalKey(e => e.VMLUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserDeviceId entity configuration
            modelBuilder.Entity<UserDeviceId>(entity =>
            {
                entity.HasKey(e => e.UniqueId);
                entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.DeviceId).IsRequired();
            });

            // UserSession entity configuration
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.UniqueId);
                entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.Host).IsRequired();
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Password).IsRequired();
                entity.Property(e => e.RefreshToken).IsRequired();
                entity.Property(e => e.TimestampUnix).IsRequired();

                // Store SubTopics and PubTopics as JSON arrays
                entity.Property(e => e.SubTopics).HasColumnType("jsonb");
                entity.Property(e => e.PubTopics).HasColumnType("jsonb");

            });

            // EmqxBrokerHost entity configuration
            modelBuilder.Entity<EmqxBrokerHost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Ip).IsRequired();
                entity.Property(e => e.UserName).IsRequired();
                entity.Property(e => e.Password).IsRequired();
                entity.Property(e => e.PublicPort);
                entity.Property(e => e.PublicIp);
                entity.Property(e => e.Port);
            });

        }
    }
}
