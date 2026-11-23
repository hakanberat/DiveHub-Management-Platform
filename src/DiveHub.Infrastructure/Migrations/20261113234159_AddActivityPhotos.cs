using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiveHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityId",
                table: "MediaAssets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ActivityId",
                table: "MediaAssets",
                column: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_Activities_ActivityId",
                table: "MediaAssets",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_Activities_ActivityId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_ActivityId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "MediaAssets");
        }
    }
}
