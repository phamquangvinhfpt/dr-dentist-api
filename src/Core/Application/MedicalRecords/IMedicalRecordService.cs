﻿using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Application.MedicalRecords;
public interface IMedicalRecordService : ITransientService
{
    Task<List<MedicalRecordResponse>> GetMedicalRecordsByPatientId(string id, DateOnly sDate, DateOnly eDate, CancellationToken cancellationToken);
    Task<MedicalRecordResponse> GetMedicalRecordByID(Guid id, CancellationToken cancellationToken);
    Task<MedicalRecordResponse> GetMedicalRecordByAppointmentID(Guid id, CancellationToken cancellationToken);
    Task CreateMedicalRecord(CreateMedicalRecordRequest request, CancellationToken cancellationToken);
    Task<bool> CheckToothNumberValidAsync(int i);
    Task<PaginationResponse<MedicalRecordResponse>> GetAllMedicalRecord(PaginationFilter request, CancellationToken cancellationToken);
}
