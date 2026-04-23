using System.Data;
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

    [HttpGet]
    public async Task<IActionResult> GetAppointment([FromQuery] string? status, [FromQuery] string? patientLastName) {
        using var connection = new SqlConnection(connectionString);
        var appointments = new List<AppointmentListDto>();

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
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }
        return Ok(appointments);
    }
    
    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetDetails(int idAppointment)
    {
        await using var connection = new SqlConnection(connectionString);
        await using var command = new SqlCommand("""
                                                 SELECT a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, 
                                                        p.Email, d.LicenseNumber, a.InternalNotes, a.CreatedAt
                                                 FROM dbo.Appointments a
                                                 JOIN dbo.Patients p ON a.IdPatient = p.IdPatient
                                                 JOIN dbo.Doctors d ON a.IdDoctor = d.IdDoctor
                                                 WHERE a.IdAppointment = @IdAppointment;
                                                 """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return Ok(new AppointmentDetailsDto{
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientEmail = reader.GetString(reader.GetOrdinal("Email")),
                InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) ? "" : reader.GetString(reader.GetOrdinal("InternalNotes")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }
        return NotFound();
    }
    
    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentRequestDto dto)
    {
        if (dto.AppointmentDate < DateTime.Now) return BadRequest("Data nie może być w przeszłości.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Sprawdzenie konfliktu
        await using var checkCmd = new SqlCommand("""
                                                  SELECT COUNT(*) FROM dbo.Appointments 
                                                  WHERE IdDoctor = @IdDoctor AND AppointmentDate = @Date AND Status = 'Scheduled';
                                                  """, connection);
        checkCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        checkCmd.Parameters.Add("@Date", SqlDbType.DateTime).Value = dto.AppointmentDate;

        var conflict = (int)await checkCmd.ExecuteScalarAsync()!;
        if (conflict > 0) return Conflict("Lekarz ma już wizytę w tym terminie.");

        await using var insertCmd = new SqlCommand("""
                                                   INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason, CreatedAt)
                                                   VALUES (@IdPatient, @IdDoctor, @Date, 'Scheduled', @Reason, GETDATE());
                                                   """, connection);
        
        insertCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        insertCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        insertCmd.Parameters.Add("@Date", SqlDbType.DateTime).Value = dto.AppointmentDate;
        insertCmd.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = dto.Reason;

        await insertCmd.ExecuteNonQueryAsync();
        return Created("", null);
    }
    
    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> Update(int idAppointment, UpdateAppointmentRequestDto dto)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var updateCmd = new SqlCommand("""
                                                   UPDATE dbo.Appointments 
                                                   SET IdPatient = @IdP, IdDoctor = @IdD, AppointmentDate = @Date, 
                                                       Status = @Status, Reason = @Reason, InternalNotes = @Notes
                                                   WHERE IdAppointment = @Id;
                                                   """, connection);

        updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        updateCmd.Parameters.Add("@IdP", SqlDbType.Int).Value = dto.IdPatient;
        updateCmd.Parameters.Add("@IdD", SqlDbType.Int).Value = dto.IdDoctor;
        updateCmd.Parameters.Add("@Date", SqlDbType.DateTime).Value = dto.AppointmentDate;
        updateCmd.Parameters.Add("@Status", SqlDbType.NVarChar).Value = dto.Status;
        updateCmd.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = dto.Reason;
        updateCmd.Parameters.Add("@Notes", SqlDbType.NVarChar).Value = (object?)dto.InternalNotes ?? DBNull.Value;

        var rows = await updateCmd.ExecuteNonQueryAsync();
        return rows > 0 ? Ok() : NotFound();
    }
    
    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> Delete(int idAppointment)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var deleteCmd = new SqlCommand("""
                                                   DELETE FROM dbo.Appointments WHERE IdAppointment = @Id AND Status <> 'Completed';
                                                   """, connection);
        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        var rows = await deleteCmd.ExecuteNonQueryAsync();
        return rows > 0 ? NoContent() : BadRequest("Nie znaleziono wizyty lub jest już ukończona (Completed).");
    }
}