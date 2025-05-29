namespace VmlMQTT.Core.Abstraction
{
    public abstract class BaseEntity
    {
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public string CreatedBy { get; set; } = string.Empty;

        public long? LastModified { get; set; }

        public string? LastModifiedBy { get; set; }
    }
}
