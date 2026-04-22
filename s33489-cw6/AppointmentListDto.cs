namespace s33489_cw6;

public class AppointmentListDto
{
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PatientFullName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
}

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

