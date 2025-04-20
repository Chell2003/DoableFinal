using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoableFinal.Migrations
{
    /// <inheritdoc />
    public partial class comments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskComment_AspNetUsers_CreatedById",
                table: "TaskComment");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComment_Tasks_ProjectTaskId",
                table: "TaskComment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskComment",
                table: "TaskComment");

            migrationBuilder.RenameTable(
                name: "TaskComment",
                newName: "TaskComments");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComment_ProjectTaskId",
                table: "TaskComments",
                newName: "IX_TaskComments_ProjectTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComment_CreatedById",
                table: "TaskComments",
                newName: "IX_TaskComments_CreatedById");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskComments",
                table: "TaskComments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_AspNetUsers_CreatedById",
                table: "TaskComments",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_Tasks_ProjectTaskId",
                table: "TaskComments",
                column: "ProjectTaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_AspNetUsers_CreatedById",
                table: "TaskComments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_Tasks_ProjectTaskId",
                table: "TaskComments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskComments",
                table: "TaskComments");

            migrationBuilder.RenameTable(
                name: "TaskComments",
                newName: "TaskComment");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComments_ProjectTaskId",
                table: "TaskComment",
                newName: "IX_TaskComment_ProjectTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComments_CreatedById",
                table: "TaskComment",
                newName: "IX_TaskComment_CreatedById");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskComment",
                table: "TaskComment",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComment_AspNetUsers_CreatedById",
                table: "TaskComment",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComment_Tasks_ProjectTaskId",
                table: "TaskComment",
                column: "ProjectTaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
