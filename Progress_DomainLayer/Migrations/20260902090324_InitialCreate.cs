using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progress_DomainLayer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Marks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    Test1 = table.Column<string>(type: "TEXT", nullable: false),
                    Test2 = table.Column<string>(type: "TEXT", nullable: false),
                    Test3 = table.Column<string>(type: "TEXT", nullable: false),
                    Test4 = table.Column<string>(type: "TEXT", nullable: false),
                    Test5 = table.Column<string>(type: "TEXT", nullable: false),
                    Test6 = table.Column<string>(type: "TEXT", nullable: false),
                    Test7 = table.Column<string>(type: "TEXT", nullable: false),
                    Test8 = table.Column<string>(type: "TEXT", nullable: false),
                    Average = table.Column<string>(type: "TEXT", nullable: false),
                    Checked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Marks");
        }
    }
}
