using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;

#nullable disable

namespace ProgressMonitoring.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261027120000_AddTeacherStaffDirectory")]
public partial class AddTeacherStaffDirectory : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        AppDbContextModelSnapshot.BuildModelForMigration(modelBuilder);
    }

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "user_id",
            table: "teachers",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<string>(
            name: "staff_id",
            table: "teachers",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "email",
            table: "teachers",
            type: "text",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE teachers
            SET staff_id = 'ET-' || REPLACE(id::text, '-', '')
            WHERE staff_id IS NULL;

            UPDATE teachers AS teacher
            SET email = app_user.email
            FROM users AS app_user
            WHERE teacher.user_id = app_user.id
              AND teacher.email IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "staff_id",
            table: "teachers",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_teachers_staff_id",
            table: "teachers",
            column: "staff_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_teachers_staff_id",
            table: "teachers");

        migrationBuilder.DropColumn(
            name: "email",
            table: "teachers");

        migrationBuilder.DropColumn(
            name: "staff_id",
            table: "teachers");

        migrationBuilder.AlterColumn<Guid>(
            name: "user_id",
            table: "teachers",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}

partial class AppDbContextModelSnapshot
{
    internal static void BuildModelForMigration(ModelBuilder modelBuilder)
    {
        new AppDbContextModelSnapshot().BuildModel(modelBuilder);
    }
}
