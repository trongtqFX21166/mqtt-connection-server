using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.ConsumerNotifyMessage.Service.Entities
{
    [Table("notification")]
    public class Notification
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("userid")]
        public long? UserId { get; set; }

        [Column("type")]
        public string Type { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("icon")]
        public string Icon { get; set; }

        [Column("actionurl")]
        public string ActionURL { get; set; }

        [Column("createddate")]
        public long CreatedDate { get; set; }

        public virtual ICollection<NotificationStatus> UserNotificationStatus { get; set; }

        public virtual NotificationDetail NotificationDetail { get; set; }
    }
}
