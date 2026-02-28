using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoCinema.Pro.Migrations
{
    /// <inheritdoc />
    public partial class AddClonedVoiceRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClonedVoices",
                columns: table => new
                {
                    VoiceId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClonedVoices", x => x.VoiceId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClonedVoices");
        }
    }
}
