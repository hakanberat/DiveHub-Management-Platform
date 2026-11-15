using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiveHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StaffMemberId",
                table: "MediaAssets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_StaffMemberId",
                table: "MediaAssets",
                column: "StaffMemberId",
                unique: true,
                filter: "[StaffMemberId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_StaffMembers_StaffMemberId",
                table: "MediaAssets",
                column: "StaffMemberId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_StaffMembers_StaffMemberId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_StaffMemberId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "StaffMemberId",
                table: "MediaAssets");
        }
    }
}
