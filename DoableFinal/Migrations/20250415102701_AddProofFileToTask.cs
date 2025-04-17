using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoableFinal.Migrations
{
    /// <inheritdoc />
    public partial class AddProofFileToTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProofFilePath",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProofFilePath",
                table: "Tasks");
        }
    }
}
