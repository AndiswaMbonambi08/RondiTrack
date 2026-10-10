using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddStokvelMemberRoleAndRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JoinedAt",
                table: "StokvelMembers",
                newName: "JoinedAtUtc");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "StokvelMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Payouts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ContributionCycleId",
                table: "Payouts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutDate",
                table: "Payouts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientUserId",
                table: "Payouts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StokvelId",
                table: "Payouts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Contributions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ContributionCycleId",
                table: "Contributions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedAt",
                table: "Contributions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "StokvelId",
                table: "Contributions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Contributions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_UserId",
                table: "StokvelMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_UserId",
                table: "Contributions",
                columns: new[] { "StokvelId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_ContributionCycles_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId",
                principalTable: "ContributionCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_StokvelMembers_StokvelId_UserId",
                table: "Contributions",
                columns: new[] { "StokvelId", "UserId" },
                principalTable: "StokvelMembers",
                principalColumns: new[] { "StokvelId", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Stokvels_StokvelId",
                table: "StokvelMembers",
                column: "StokvelId",
                principalTable: "Stokvels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_ContributionCycles_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_StokvelMembers_StokvelId_UserId",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Stokvels_StokvelId",
                table: "StokvelMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_StokvelMembers_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_StokvelId_UserId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StokvelMembers");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ContributionCycleId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "PayoutDate",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "StokvelId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "ContributionCycleId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "RecordedAt",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "StokvelId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Contributions");

            migrationBuilder.RenameColumn(
                name: "JoinedAtUtc",
                table: "StokvelMembers",
                newName: "JoinedAt");
        }
    }
}
