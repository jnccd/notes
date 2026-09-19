using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ParentDeletedNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeletedNotes_DeletedNotes_ParentDeletedNoteId",
                        column: x => x.ParentDeletedNoteId,
                        principalTable: "DeletedNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeletedNotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_DeletionId",
                table: "DeletedNotes",
                column: "DeletionId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_NoteId",
                table: "DeletedNotes",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_ParentDeletedNoteId",
                table: "DeletedNotes",
                column: "ParentDeletedNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_UserId",
                table: "DeletedNotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedNotes");
        }
    }
}
