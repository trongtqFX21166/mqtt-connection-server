using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Infratructure.Data
{
    public class VmlMQTTDbContext : DbContext
    {
        public VmlMQTTDbContext(DbContextOptions<VmlMQTTDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserDeviceId> UserDeviceIds { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<EmqxBrokerHost> EmqxBrokerHosts { get; set; }
        public DbSet<SessionSubTopic> SessionSubTopics { get; set; }
        public DbSet<SessionPubTopic> SessionPubTopics { get; set; }

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

                entity.HasMany(e => e.SessionSubTopics)
                    .WithOne(e => e.UserSession)
                    .HasForeignKey(e => e.UserSessionId)
                    .HasPrincipalKey(e => e.UniqueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.SessionPubTopics)
                    .WithOne(e => e.UserSession)
                    .HasForeignKey(e => e.UserSessionId)
                    .HasPrincipalKey(e => e.UniqueId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // EmqxBrokerHost entity configuration
            modelBuilder.Entity<EmqxBrokerHost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Ip).IsRequired();
                entity.Property(e => e.UserName).IsRequired();
                entity.Property(e => e.Password).IsRequired();
                entity.Property(e => e.TopicClientRequestPattern).IsRequired();
                entity.Property(e => e.TopicClientResponsePattern).IsRequired();
                entity.Property(e => e.TopicNotifyPattern).IsRequired();
            });

            // SessionSubTopic entity configuration
            modelBuilder.Entity<SessionSubTopic>(entity =>
            {
                entity.HasKey(e => e.UniqueId);
                entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.TopicPattern).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
            });

            // SessionPubTopic entity configuration
            modelBuilder.Entity<SessionPubTopic>(entity =>
            {
                entity.HasKey(e => e.UniqueId);
                entity.Property(e => e.UniqueId).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.TopicPattern).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();
            });
        }


        public static void Seed(VmlMQTTDbContext context)
        {
            // Check if data already exists
            if (context.EmqxBrokerHosts.Any())
                return;

            // Add broker host
            context.EmqxBrokerHosts.Add(new EmqxBrokerHost
            {
                Id = 1,
                Ip = "192.168.8.164",
                UserName = "admin",
                Password = "Vietmap2021!@#",
                TopicClientRequestPattern = "user/{userId}/request",
                TopicClientResponsePattern = "user/{userId}/response",
                TopicNotifyPattern = "user/{userId}/notify",
                TotalAccounts = 0,
                TotalConnections = 0,
                LastModified = DateTime.UtcNow,
                LastModifiedBy = DateTime.UtcNow,
                IsActive = true
            });

            context.SaveChanges();
        }
    }
}
