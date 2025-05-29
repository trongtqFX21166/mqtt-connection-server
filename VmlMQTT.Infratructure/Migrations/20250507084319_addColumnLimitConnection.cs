using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmlMQTT.Infratructure.Migrations
{
    /// <inheritdoc />
    public partial class addColumnLimitConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LimitConnections",
                table: "EmqxBrokerHosts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimitConnections",
                table: "EmqxBrokerHosts");
        }
    }
}
