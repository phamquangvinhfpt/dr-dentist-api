using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.MedicalHistory;
public class MedicalHistoryController : VersionNeutralApiController
{
    private readonly ICurrentUser _currentUserService;
    private readonly IMedicalHistoryService _medicalHistoryService;

    public MedicalHistoryController(ICurrentUser currentUserService, IMedicalHistoryService medicalHistoryService)
    {
        _currentUserService = currentUserService;
        _medicalHistoryService = medicalHistoryService;
    }
    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.MedicalHistory)]
    [OpenApiOperation("Create Medical history", "")]
    public Task<string> CreateMedicalHistory(CreateAndUpdateMedicalHistoryRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpPost("update")]
    [MustHavePermission(FSHAction.Update, FSHResource.MedicalHistory)]
    [OpenApiOperation("Update Medical History", "")]
    public Task<string> UpdateMedicalHistory(CreateAndUpdateMedicalHistoryRequest request)
    {
        return Mediator.Send(request);
    }
    [HttpDelete("delete")]
    [MustHavePermission(FSHAction.Delete, FSHResource.MedicalHistory)]
    [OpenApiOperation("Delete Doctor Profile", "")]
    public async Task<string> DeleteMedicalHistory(string patientID, CancellationToken cancellationToken)
    {
        return await _medicalHistoryService.DeleteMedicalHistory(patientID, cancellationToken);
    }
}
