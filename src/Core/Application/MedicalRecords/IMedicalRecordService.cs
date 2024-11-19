using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Application.MedicalRecords;
public interface IMedicalRecordService : ITransientService
{
    Task<List<MedicalRecordResponse>> GetMedicalRecordsByPatientId(string id, CancellationToken cancellationToken);
    Task<MedicalRecordResponse> GetMedicalRecordByID(Guid id, CancellationToken cancellationToken);
    Task<MedicalRecordResponse> GetMedicalRecordByAppointmentID(Guid id, CancellationToken cancellationToken);
    Task CreateMedicalRecord(CreateMedicalRecordRequest request, CancellationToken cancellationToken);
    Task UpdateMedicalRecord(UpdateMedicalRecordRequest request, CancellationToken cancellationToken);
    Task<string> DeleteMedicalRecordID(Guid id, CancellationToken cancellationToken);
    Task<string> DeleteMedicalRecordByPatientID(string id, CancellationToken cancellationToken);
}
