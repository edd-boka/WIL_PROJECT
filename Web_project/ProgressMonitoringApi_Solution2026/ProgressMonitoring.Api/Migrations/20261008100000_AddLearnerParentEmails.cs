using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProgressMonitoring.Api.Data;

#nullable disable

namespace ProgressMonitoring.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261008100000_AddLearnerParentEmails")]
    public partial class AddLearnerParentEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learner_parent_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learner_parent_emails", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_learner_parent_emails_email",
                table: "learner_parent_emails",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_learner_parent_emails_learner_id_email",
                table: "learner_parent_emails",
                columns: new[] { "learner_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "learner_parent_emails");
        }
    }
}
