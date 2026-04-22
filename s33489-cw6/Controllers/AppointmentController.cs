using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace s33489_cw6.Controllers;
[ApiController]
    [Route("api/[controller]")]
public class AppointmentController : ControllerBase {
    private readonly string connectionString;

    public AppointmentController(IConfiguration conf) {
        connectionString = conf.GetConnectionString("DefaultConnection");
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointment([FromQuery] string? status, [FromQuery] string? patientLastName) {
        using var connection = new SqlConnection(connectionString);

        using var cmd = new SqlCommand(@"
            SELECT a.IdAppointment, a.AppointmentDate, a.Status, a.Reason,
                   p.FirstName + ' ' + p.LastName AS PatientFullName, p.Email AS PatientEmail
            FROM Appointments a
            JOIN Patients p ON a.IdPatient = p.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
              AND (@LastName IS NULL OR p.LastName = @LastName)
            ORDER BY a.AppointmentDate", connection);
        cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastName", (object?)patientLastName ?? DBNull.Value);
        
        await connection.OpenAsync();
        return Ok();
    }
}