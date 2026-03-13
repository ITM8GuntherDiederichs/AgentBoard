using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentBoard.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationConnectedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IntegrationConnectedAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrationConnectedAt",
                table: "Projects");
        }
    }
}
