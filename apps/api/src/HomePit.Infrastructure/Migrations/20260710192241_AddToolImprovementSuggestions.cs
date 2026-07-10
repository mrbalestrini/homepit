using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolImprovementSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tool_improvement_suggestions",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuggestionText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InternalComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_improvement_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_improvement_suggestions_users_LastReviewedByUserId",
                        column: x => x.LastReviewedByUserId,
                        principalSchema: "homepit",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tool_improvement_suggestions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "homepit",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_LastReviewedByUserId",
                schema: "homepit",
                table: "tool_improvement_suggestions",
                column: "LastReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_Priority",
                schema: "homepit",
                table: "tool_improvement_suggestions",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_Status",
                schema: "homepit",
                table: "tool_improvement_suggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_SubmittedAt",
                schema: "homepit",
                table: "tool_improvement_suggestions",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_UserId",
                schema: "homepit",
                table: "tool_improvement_suggestions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_improvement_suggestions",
                schema: "homepit");
        }
    }
}
