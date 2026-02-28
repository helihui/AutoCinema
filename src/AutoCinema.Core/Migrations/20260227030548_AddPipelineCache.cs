using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCinema.Pro.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineCaches",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    JsonData = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineCaches", x => x.CacheKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineCaches");
        }
    }
}
