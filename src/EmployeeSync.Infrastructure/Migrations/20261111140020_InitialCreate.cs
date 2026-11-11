using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderEmployeeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TrackTikEmployeeId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SyncStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    ExistsInTrackTik = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.CheckConstraint("CK_Employees_Status", "[Status] IN ('active', 'inactive', 'terminated')");
                    table.CheckConstraint("CK_Employees_SyncStatus", "[SyncStatus] IN ('pending', 'synced', 'failed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SyncStatus",
                table: "Employees",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "UX_Employees_Provider_ProviderEmployeeId",
                table: "Employees",
                columns: new[] { "Provider", "ProviderEmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employees");
        }
    }
}
