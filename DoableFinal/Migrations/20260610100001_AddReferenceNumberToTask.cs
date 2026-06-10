using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoableFinal.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceNumberToTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Tasks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "Tasks");
        }
    }
}
