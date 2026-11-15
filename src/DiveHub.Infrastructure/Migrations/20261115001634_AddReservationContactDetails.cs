using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiveHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContactByEmail",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ContactByPhone",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ContactByWhatsApp",
                table: "Reservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Reservations",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Reservations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactWhatsApp",
                table: "Reservations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerFullName",
                table: "Reservations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactByEmail",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactByPhone",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactByWhatsApp",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactWhatsApp",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CustomerFullName",
                table: "Reservations");
        }
    }
}
