using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Identity;

internal class MedicalHistoryService : IMedicalHistoryService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUser _currentUser;

    public MedicalHistoryService(
        ApplicationDbContext db,
        IStringLocalizer<MedicalHistoryService> t,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUser currentUser)
    {
        _db = db;
        _t = t;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
    }

    public async Task CreateAndUpdateMedicalHistory(CreateAndUpdateMedicalHistoryRequest request, CancellationToken cancellationToken)
    {
        var mdch = await _db.MedicalHistorys.Where(p => p.PatientId ==  request.PatientId).FirstOrDefaultAsync();
        if (mdch != null)
        {
            mdch.MedicalName = request.MedicalName;
            mdch.Note = request.Note;
            mdch.LastModifiedBy = _currentUser.GetUserId();
            mdch.LastModifiedOn = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else {
            var n = new MedicalHistory
            {
                PatientId = request.PatientId,
                CreatedBy = _currentUser.GetUserId(),
                CreatedOn = DateTime.Now,
                MedicalName = request.MedicalName,
                Note = request.Note
            };
            _db.MedicalHistorys.Add(n);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string> DeleteMedicalHistory(string patientID, CancellationToken cancellationToken)
    {
        //var currentuser = _currentUser.GetUserId().ToString();
        //if(_currentUser.GetRole() != FSHRoles.Patient)
        //{
        //    throw new BadRequestException("Only patient can delete their medical history");
        //}
        //if(patientID != currentuser)
        //{
        //    throw new BadRequestException("Only delete own medical history");
        //}
        var user = _userManager.FindByIdAsync(patientID) ?? throw new NotFoundException($"User is invalid");
        var roles = _userManager.GetRolesAsync(user.Result).Result;
        var flag = false;
        foreach (var role in roles)
        {
            if (role == FSHRoles.Patient)
            {
                flag = true; break;
            }
        }
        if (!flag) {
            throw new BadRequestException("User is not patient");
        }
        var existingMedical = await _db.MedicalHistorys.Where(p => p.PatientId == patientID).FirstOrDefaultAsync();
        if (existingMedical != null)
        {
            _db.MedicalHistorys.Remove(existingMedical);
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Delete Successfully"];
        }
        throw new BadRequestException("You do not have any medical history");
    }

    public Task<MedicalHistory> GetMedicalHistoryByPatientID(string patientID, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
