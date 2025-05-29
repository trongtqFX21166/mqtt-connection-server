using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.ConsumerNotifyMessage.Service.Entities;

namespace VmlMQTT.ConsumerNotifyMessage.Service.Repositories
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
        }


        public virtual DbSet<Notification> Notification { get; set; }

        public virtual DbSet<NotificationStatus> NotificationStatus { get; set; }

        public virtual DbSet<NotificationDetail> NotificationDetail { get; set; }
    }
}
