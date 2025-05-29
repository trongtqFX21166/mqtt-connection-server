using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.ConsumerNotifyMessage.Service.Entities
{
    public class NotificationStatus
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("userid")]
        public long UserId { get; set; }

        [Column("isread")]
        public bool IsRead { get; set; }

        [Column("notificationid")]
        public long NotificationId { get; set; }

        [Column("createddate")]
        public long CreatedDate { get; set; }

        [Column("lastmodified")]
        public long LastModified { get; set; }

        [Column("isdeleted")]
        public bool IsDeleted { get; set; }

        public virtual Notification UserNotification { get; set; }
    }
}
