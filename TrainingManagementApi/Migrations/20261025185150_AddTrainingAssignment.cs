using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingManagementApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingAssignment",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingAssignment", x => x.AssignmentId);
                    table.CheckConstraint("CK_TrainingAssignment_EndDate_OnOrAfterStartDate", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_TrainingAssignment_Status_Valid", "[Status] IN ('Assigned', 'In Progress', 'Completed')");
                    table.ForeignKey(
                        name: "FK_TrainingAssignment_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "CourseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingAssignment_Employee_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employee",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingAssignment_CourseId",
                table: "TrainingAssignment",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "UX_TrainingAssignment_Employee_Course",
                table: "TrainingAssignment",
                columns: new[] { "EmployeeId", "CourseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingAssignment");
        }
    }
}
