using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesServer.Migrations
{
    /// <inheritdoc />
    public partial class DropDeletedNoteDeletionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeletedNotes_DeletionId",
                table: "DeletedNotes");

            migrationBuilder.DropColumn(
                name: "DeletionId",
                table: "DeletedNotes");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_UserId_ParentDeletedNoteId",
                table: "DeletedNotes",
                columns: new[] { "UserId", "ParentDeletedNoteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeletedNotes_UserId_ParentDeletedNoteId",
                table: "DeletedNotes");

            migrationBuilder.AddColumn<Guid>(
                name: "DeletionId",
                table: "DeletedNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_DeletedNotes_DeletionId",
                table: "DeletedNotes",
                column: "DeletionId");
        }
    }
}
