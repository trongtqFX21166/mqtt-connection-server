using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmlMQTT.Infratructure.Migrations
{
    /// <inheritdoc />
    public partial class db250425 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "UserSessions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "UserSessions");
        }
    }
}
