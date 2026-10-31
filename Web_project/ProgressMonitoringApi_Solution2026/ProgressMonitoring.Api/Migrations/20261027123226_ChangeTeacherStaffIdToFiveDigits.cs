using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProgressMonitoring.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTeacherStaffIdToFiveDigits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT COUNT(*) FROM teachers) > 90000 THEN
                        RAISE EXCEPTION 'Cannot assign unique five-digit staff IDs to more than 90000 teachers.';
                    END IF;
                END $$;

                WITH numbered_teachers AS (
                    SELECT id, (9999 + ROW_NUMBER() OVER (ORDER BY id))::text AS new_staff_id
                    FROM teachers
                )
                UPDATE teachers AS teacher
                SET staff_id = numbered_teachers.new_staff_id
                FROM numbered_teachers
                WHERE teacher.id = numbered_teachers.id;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_staff_id_five_digits",
                table: "teachers",
                sql: "\"staff_id\" ~ '^[0-9]{5}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_staff_id_five_digits",
                table: "teachers");

            migrationBuilder.Sql("""
                UPDATE teachers
                SET staff_id = 'ET-' || REPLACE(id::text, '-', '');
                """);
        }
    }
}
