using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoableFinal.Migrations
{
    /// <inheritdoc />
    public partial class BackfillReferenceNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill reference numbers for tasks that don't have one yet.
            // Uses the first letter of each word in the project name (max 5 letters).
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.ReferenceNumber = (
                    SELECT
                        UPPER(
                            CASE
                                WHEN p.Name NOT LIKE '% %' THEN
                                    LEFT(p.Name, CASE WHEN LEN(p.Name) >= 3 THEN 3 ELSE LEN(p.Name) END)
                                ELSE
                                    LEFT(p.Name, 1)
                                    + COALESCE(SUBSTRING(p.Name, NULLIF(CHARINDEX(' ', p.Name), 0) + 1, 1), '')
                                    + COALESCE(SUBSTRING(p.Name, NULLIF(CHARINDEX(' ', p.Name, CHARINDEX(' ', p.Name) + 1), 0) + 1, 1), '')
                                    + COALESCE(SUBSTRING(p.Name, NULLIF(CHARINDEX(' ', p.Name, CHARINDEX(' ', p.Name, CHARINDEX(' ', p.Name) + 1) + 1), 0) + 1, 1), '')
                                    + COALESCE(SUBSTRING(p.Name, NULLIF(CHARINDEX(' ', p.Name, CHARINDEX(' ', p.Name, CHARINDEX(' ', p.Name, CHARINDEX(' ', p.Name) + 1) + 1) + 1), 0) + 1, 1), '')
                            END
                        )
                        + '-' + CAST(YEAR(t.CreatedAt) AS NVARCHAR(4))
                        + '-' + RIGHT('00000' + CAST(t.Id AS NVARCHAR(5)), 5)
                    FROM Projects p
                    WHERE p.Id = t.ProjectId
                )
                FROM Tasks t
                WHERE t.ReferenceNumber IS NULL OR t.ReferenceNumber = ''
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Tasks SET ReferenceNumber = NULL WHERE ReferenceNumber LIKE '%-%-[0-9]%'");
        }
    }
}
