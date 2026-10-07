using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "identity",
                table: "Users");
        }
    }
}
