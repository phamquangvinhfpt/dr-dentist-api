﻿using FSH.WebApi.Application.MedicalRecords;

namespace FSH.WebApi.Host.Controllers.MedicalRecords;

public class MedicalRecordController : VersionNeutralApiController
{
    private readonly IMedicalRecordService _mediicalRecordService;

    public MedicalRecordController(IMedicalRecordService mediicalRecordService)
    {
        _mediicalRecordService = mediicalRecordService;
    }

    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.MedicalRecord)]
    [OpenApiOperation("Create Medical record.", "")]
    public Task<string> CreateMedicalRecord(CreateMedicalRecordRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpGet("get/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.MedicalRecord)]
    [OpenApiOperation("Get Medical record by ID", "")]
    public Task<MedicalRecordResponse> GetMedicalRecordByID(Guid id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetMedicalRecordByID(id, cancellationToken);
    }

    [HttpGet("get-by-appointment/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.MedicalRecord)]
    [OpenApiOperation("Get Medical record by appointment ID", "")]
    public Task<MedicalRecordResponse> GetMedicalRecordByAppointmentID(Guid id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetMedicalRecordByAppointmentID(id, cancellationToken);
    }

    [HttpGet("patient/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.MedicalRecord)]
    [OpenApiOperation("Get Medical records by patient ID", "")]
    public Task<List<MedicalRecordResponse>> GetMedicalRecordsByPatientID(string id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetMedicalRecordsByPatientId(id, cancellationToken);
    }

    [HttpPut("update")]
    [MustHavePermission(FSHAction.Update, FSHResource.MedicalRecord)]
    [OpenApiOperation("Update Medical record.", "")]
    public Task<string> UpdateMedicalRecord(UpdateMedicalRecordRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpDelete("delete/{id}")]
    [OpenApiOperation("Delete Medical record by ID", "")]
    [MustHavePermission(FSHAction.Delete, FSHResource.MedicalRecord)]
    public Task<string> DeleteMedicalRecordByID(Guid id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.DeleteMedicalRecordID(id, cancellationToken);
    }

    [HttpDelete("delete-by-patient/{id}")]
    [OpenApiOperation("Delete Medical records by patient ID", "")]
    [MustHavePermission(FSHAction.Delete, FSHResource.MedicalRecord)]
    public Task<string> DeleteMedicalRecordsByPatientID(string id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.DeleteMedicalRecordByPatientID(id, cancellationToken);
    }
}