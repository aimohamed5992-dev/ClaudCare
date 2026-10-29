using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudCare.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotificationSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Notifications",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Notifications");
        }
    }
}
