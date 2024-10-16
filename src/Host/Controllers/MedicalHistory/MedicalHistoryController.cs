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
    [OpenApiOperation("Update Doctor Profile", "")]
    public Task<string> CreateOrUpdateMedicalHistory(CreateAndUpdateMedicalHistoryRequest request)
    {
        return Mediator.Send(request);
    }
    [HttpDelete("delete")]
    [MustHavePermission(FSHAction.Delete, FSHResource.MedicalHistory)]
    [OpenApiOperation("Update Doctor Profile", "")]
    public async Task<string> DeleteMedicalHistory(string patientID, CancellationToken cancellationToken)
    {
        return await _medicalHistoryService.DeleteMedicalHistory(patientID, cancellationToken);
    }
}
