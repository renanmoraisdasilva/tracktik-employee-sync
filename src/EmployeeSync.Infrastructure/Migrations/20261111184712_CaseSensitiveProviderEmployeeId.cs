using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CaseSensitiveProviderEmployeeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite is already case-sensitive and has no SQL Server collations.
            if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer")
            {
                return;
            }

            migrationBuilder.AlterColumn<string>(
                name: "ProviderEmployeeId",
                table: "Employees",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer")
            {
                return;
            }

            migrationBuilder.AlterColumn<string>(
                name: "ProviderEmployeeId",
                table: "Employees",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldCollation: "Latin1_General_100_BIN2");
        }
    }
}
