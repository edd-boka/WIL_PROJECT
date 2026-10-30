using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProgressMonitoring.Api.Data;

#nullable disable

namespace ProgressMonitoring.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261005190000_AddSubjectPhase")]
    public partial class AddSubjectPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing subjects become "Both" so nothing disappears; change them per subject afterwards.
            migrationBuilder.AddColumn<string>(
                name: "phase",
                table: "subjects",
                type: "text",
                nullable: false,
                defaultValue: "Both");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phase",
                table: "subjects");
        }
    }
}
