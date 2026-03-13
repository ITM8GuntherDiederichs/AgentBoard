using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBoard.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIssueNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalIssueNumber",
                table: "Todos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSystem",
                table: "Todos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalIssueNumber",
                table: "FeatureRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSystem",
                table: "FeatureRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalIssueNumber",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "ExternalSystem",
                table: "Todos");

            migrationBuilder.DropColumn(
                name: "ExternalIssueNumber",
                table: "FeatureRequests");

            migrationBuilder.DropColumn(
                name: "ExternalSystem",
                table: "FeatureRequests");
        }
    }
}
