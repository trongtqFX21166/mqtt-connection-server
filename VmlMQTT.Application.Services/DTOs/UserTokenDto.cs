namespace VmlMQTT.Application.DTOs
{
    public class UserTokenDto
    {
        public List<DeviceTokenDto>? DevicesOfVietMap { get; set; }
        public DeviceTokenDto? OtherDevice { get; set; }
        public long RowVersion { get; set; }

        public List<DeviceTokenDto> GetAll => [.. (DevicesOfVietMap ?? []).Union([OtherDevice ?? new()])];
    }
}