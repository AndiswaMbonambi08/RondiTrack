using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelAfterSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Payouts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ux_contributions_cycle_user",
                table: "Contributions",
                columns: new[] { "ContributionCycleId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_contributions_cycle_user",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Payouts");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId");
        }
    }
}
