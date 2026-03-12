using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBoard.Migrations
{
    /// <inheritdoc />
    public partial class AddDeployFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntegrationType",
                table: "Teams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RepoUrl",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationToken",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationRepoUrl",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalProjectId",
                table: "Projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrationType",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RepoUrl",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "IntegrationToken",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IntegrationRepoUrl",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ExternalProjectId",
                table: "Projects");
        }
    }
}
