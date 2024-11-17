using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Treatment;

namespace FSH.WebApi.Application.MedicalRecords;
public class MedicalRecordResponse
{
    public Guid RecordId { get; set; }
    public Guid? PatientId { get; set; }
    public string? PatientCode { get; set; }
    public string? PatientName { get; set; }
    public Guid? DentistId { get; set; }
    public string? DentistName { get; set; }
    public Guid? AppointmentId { get; set; }
    public string? AppointmentNotes { get; set; }
    public DateTime Date { get; set; }
    public BasicExaminationRequest? BasicExamination { get; set; }
    public DiagnosisRequest? Diagnosis { get; set; }
    public IndicationRequest? Indication { get; set; }
}
