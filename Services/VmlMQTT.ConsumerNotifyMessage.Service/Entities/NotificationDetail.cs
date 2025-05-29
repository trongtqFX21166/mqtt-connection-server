using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.ConsumerNotifyMessage.Service.Entities
{
    [Table("notification_detail")]
    public class NotificationDetail
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("notificationid")]
        public long NotificationId { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("createddate")]
        public long CreatedDate { get; set; }

        [Column("isdeleted")]
        public bool IsDeleted { get; set; }

        public virtual Notification Notification { get; set; }
    }
}
