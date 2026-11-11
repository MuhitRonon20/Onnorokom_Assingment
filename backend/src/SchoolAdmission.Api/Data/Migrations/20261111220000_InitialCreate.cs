using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolAdmission.Api.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Classes",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TotalSeats = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Classes", x => x.Id);
                table.CheckConstraint("CK_Classes_TotalSeats_Positive", "\"TotalSeats\" > 0");
            });

        migrationBuilder.CreateTable(
            name: "Holds",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ClassId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                StudentId = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Holds", x => x.Id);
                table.ForeignKey(
                    name: "FK_Holds_Classes_ClassId",
                    column: x => x.ClassId,
                    principalTable: "Classes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Classes_Name",
            table: "Classes",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Holds_ClassId_Status",
            table: "Holds",
            columns: new[] { "ClassId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Holds_Status_ExpiresAtUtc",
            table: "Holds",
            columns: new[] { "Status", "ExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Holds_Student_Class_Confirmed",
            table: "Holds",
            columns: new[] { "StudentId", "ClassId" },
            unique: true,
            filter: "\"Status\" = 'CONFIRMED'");

        migrationBuilder.CreateIndex(
            name: "IX_Holds_StudentId",
            table: "Holds",
            column: "StudentId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Holds");

        migrationBuilder.DropTable(
            name: "Classes");
    }
}

