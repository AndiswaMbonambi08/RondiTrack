using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleScheduleArrearsAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "ContributionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "ContributionCycles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "ContributionCycles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Arrears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortfallCents = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Arrears_ContributionCycles_ContributionCycleId",
                        column: x => x.ContributionCycleId,
                        principalTable: "ContributionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Arrears_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyEntries",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyEntries", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires",
                table: "RefreshTokens",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_cycles_closed_enddate",
                table: "ContributionCycles",
                columns: new[] { "ClosedAtUtc", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "ux_cycles_stokvel_startdate",
                table: "ContributionCycles",
                columns: new[] { "StokvelId", "StartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Arrears_UserId",
                table: "Arrears",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ux_arrears_cycle_user",
                table: "Arrears",
                columns: new[] { "ContributionCycleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_created",
                table: "IdempotencyEntries",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arrears");

            migrationBuilder.DropTable(
                name: "IdempotencyEntries");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_expires",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "ix_cycles_closed_enddate",
                table: "ContributionCycles");

            migrationBuilder.DropIndex(
                name: "ux_cycles_stokvel_startdate",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ContributionCycles");
        }
    }
}
