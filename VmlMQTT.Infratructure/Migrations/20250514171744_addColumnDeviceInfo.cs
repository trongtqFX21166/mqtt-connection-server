using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmlMQTT.Infratructure.Migrations
{
    /// <inheritdoc />
    public partial class addColumnDeviceInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceInfo",
                table: "UserDeviceIds",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceInfo",
                table: "UserDeviceIds");
        }
    }
}
