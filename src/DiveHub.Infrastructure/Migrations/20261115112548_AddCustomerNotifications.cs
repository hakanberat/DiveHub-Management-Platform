using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiveHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNotifications_AspNetUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerNotifications_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotifications_CustomerUserId_ReadAtUtc_CreatedAtUtc",
                table: "CustomerNotifications",
                columns: new[] { "CustomerUserId", "ReadAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNotifications_ReservationId_Kind",
                table: "CustomerNotifications",
                columns: new[] { "ReservationId", "Kind" },
                unique: true,
                filter: "[ReservationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerNotifications");
        }
    }
}
