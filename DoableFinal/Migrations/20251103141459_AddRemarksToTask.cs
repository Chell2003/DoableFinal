using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoableFinal.Migrations
{
    /// <inheritdoc />
    public partial class AddRemarksToTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisapprovalRemarks",
                table: "Tasks");

            migrationBuilder.RenameColumn(
                name: "SubmissionRemarks",
                table: "Tasks",
                newName: "Remarks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "Tasks",
                newName: "SubmissionRemarks");

            migrationBuilder.AddColumn<string>(
                name: "DisapprovalRemarks",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
