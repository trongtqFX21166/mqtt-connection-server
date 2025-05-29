using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VmlMQTT.Infratructure.Migrations
{
    /// <inheritdoc />
    public partial class add_base_entity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "UserSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserSessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "LastModified",
                table: "UserSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "UserSessions",
                type: "text",
                nullable: true);

            // Sử dụng Sql trực tiếp với mệnh đề USING để chuyển đổi kiểu dữ liệu
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                ALTER COLUMN ""LastModifiedBy"" TYPE text 
                USING ""LastModifiedBy""::text,
                ALTER COLUMN ""LastModifiedBy"" DROP NOT NULL;
            ");

            // Sử dụng CASE để xử lý giá trị 'infinity' và '-infinity'
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                ALTER COLUMN ""LastModified"" TYPE bigint 
                USING CASE 
                    WHEN ""LastModified"" = 'infinity'::timestamp with time zone THEN 9223372036854775807
                    WHEN ""LastModified"" = '-infinity'::timestamp with time zone THEN -9223372036854775808
                    ELSE EXTRACT(EPOCH FROM ""LastModified"")::bigint * 1000
                END,
                ALTER COLUMN ""LastModified"" DROP NOT NULL;
            ");

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "UserDeviceIds",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserDeviceIds",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "LastModified",
                table: "UserDeviceIds",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "UserDeviceIds",
                type: "text",
                nullable: true);

            // Sử dụng Sql trực tiếp với mệnh đề USING để chuyển đổi kiểu dữ liệu
            migrationBuilder.Sql(@"
                ALTER TABLE ""EmqxBrokerHosts"" 
                ALTER COLUMN ""LastModifiedBy"" TYPE text 
                USING ""LastModifiedBy""::text,
                ALTER COLUMN ""LastModifiedBy"" DROP NOT NULL;
            ");

            // Sử dụng CASE để xử lý giá trị 'infinity' và '-infinity'
            migrationBuilder.Sql(@"
                ALTER TABLE ""EmqxBrokerHosts"" 
                ALTER COLUMN ""LastModified"" TYPE bigint 
                USING CASE 
                    WHEN ""LastModified"" = 'infinity'::timestamp with time zone THEN 9223372036854775807
                    WHEN ""LastModified"" = '-infinity'::timestamp with time zone THEN -9223372036854775808
                    ELSE EXTRACT(EPOCH FROM ""LastModified"")::bigint * 1000
                END,
                ALTER COLUMN ""LastModified"" DROP NOT NULL;
            ");

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "EmqxBrokerHosts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EmqxBrokerHosts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserDeviceIds");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserDeviceIds");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "UserDeviceIds");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "UserDeviceIds");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EmqxBrokerHosts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EmqxBrokerHosts");

            // Chuyển đổi ngược lại từ text sang timestamp with time zone
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                ALTER COLUMN ""LastModifiedBy"" TYPE timestamp with time zone 
                USING '1970-01-01 00:00:00 UTC'::timestamp with time zone,
                ALTER COLUMN ""LastModifiedBy"" SET NOT NULL;
            ");

            // Xử lý chuyển đổi từ bigint sang timestamp bằng CASE
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                ALTER COLUMN ""LastModified"" TYPE timestamp with time zone 
                USING CASE 
                    WHEN ""LastModified"" = 9223372036854775807 THEN 'infinity'::timestamp with time zone
                    WHEN ""LastModified"" = -9223372036854775808 THEN '-infinity'::timestamp with time zone
                    ELSE to_timestamp(""LastModified""/1000) AT TIME ZONE 'UTC'
                END,
                ALTER COLUMN ""LastModified"" SET NOT NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""EmqxBrokerHosts"" 
                ALTER COLUMN ""LastModifiedBy"" TYPE timestamp with time zone 
                USING '1970-01-01 00:00:00 UTC'::timestamp with time zone,
                ALTER COLUMN ""LastModifiedBy"" SET NOT NULL;
            ");

            // Xử lý chuyển đổi từ bigint sang timestamp bằng CASE
            migrationBuilder.Sql(@"
                ALTER TABLE ""EmqxBrokerHosts"" 
                ALTER COLUMN ""LastModified"" TYPE timestamp with time zone 
                USING CASE 
                    WHEN ""LastModified"" = 9223372036854775807 THEN 'infinity'::timestamp with time zone
                    WHEN ""LastModified"" = -9223372036854775808 THEN '-infinity'::timestamp with time zone
                    ELSE to_timestamp(""LastModified""/1000) AT TIME ZONE 'UTC'
                END,
                ALTER COLUMN ""LastModified"" SET NOT NULL;
            ");
        }
    }
}