using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Infrastructure.Persistence.Initialization;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace FSH.WebApi.Infrastructure.Identity;
public class UserSeeder : ICustomSeeder
{

    private readonly ISerializerService _serializerService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UserSeeder> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FSHTenantInfo _currentTenant;
    public UserSeeder(ISerializerService serializerService,
        ApplicationDbContext db,
        ILogger<UserSeeder> logger,
        UserManager<ApplicationUser> userManager,
        FSHTenantInfo currentTenant
        )
    {
        _serializerService = serializerService;
        _db = db;
        _logger = logger;
        _userManager = userManager;
        _currentTenant = currentTenant;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string userDataPath = Path.Combine(path!, "Identity", "UserData.json");
        string patientDataPath = Path.Combine(path!, "Identity", "PatientProfileData.json");
        string doctorDataPath = Path.Combine(path!, "Identity", "DoctorProfileData.json");
        if (_db.Users.Count() <= 5)
        {
            _logger.LogInformation("Started to Seed Users.");
            string userData = await File.ReadAllTextAsync(userDataPath, cancellationToken);
            string patientData = await File.ReadAllTextAsync(patientDataPath, cancellationToken);
            string doctorData = await File.ReadAllTextAsync(doctorDataPath, cancellationToken);
            var users = _serializerService.Deserialize<List<ApplicationUser>>(userData);
            var patients = _serializerService.Deserialize<List<PatientProfile>>(patientData);
            var doctors = _serializerService.Deserialize<List<DoctorProfile>>(doctorData);
            List<ApplicationUser> doctor = new List<ApplicationUser>();
            List<ApplicationUser> staff = new List<ApplicationUser>();
            List<ApplicationUser> patient = new List<ApplicationUser>();
            int flash = 0;
            int d_profile_index = 0;
            int p_profile_index = 0;
            foreach (var user in users)
            {
                var entry = _db.Users.Add(user).Entity;
                if (flash < 5)
                {
                    doctors[d_profile_index].DoctorId = entry.Id;
                    doctor.Add(entry);
                    flash++;
                    d_profile_index++;
                }
                else if (flash >= 5 && flash < 10)
                {
                    staff.Add(entry);
                    flash++;
                }
                else
                {
                    patients[p_profile_index].UserId = entry.Id;
                    patient.Add(entry);
                    p_profile_index++;
                }
            }
            await _db.DoctorProfiles.AddRangeAsync(doctors);
            await _db.PatientProfiles.AddRangeAsync(patients);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var user in doctor)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Dentist))
                {
                    _logger.LogInformation("Assigning Dentist Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Dentist);
                }
                List<WorkingCalendar> calendars = CreateWorkingCalendar(user.Id, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
                await _db.WorkingCalendars.AddRangeAsync(calendars);
            }
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var user in staff)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Staff))
                {
                    _logger.LogInformation("Assigning Staff Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Staff);
                }
            }
            foreach (var user in patient)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Patient))
                {
                    _logger.LogInformation("Assigning Patient Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Patient);
                }
            }

            _logger.LogInformation("Seeded Users.");
        }
    }
    public List<WorkingCalendar> CreateWorkingCalendar(string doctorId, TimeSpan startTime, TimeSpan endTime, string? note = null)
    {
        var result = new List<WorkingCalendar>();

        var currentDate = DateOnly.FromDateTime(DateTime.Now);

        var lastDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, DateTime.DaysInMonth(currentDate.Year, currentDate.Month));
        var lastDate = DateOnly.FromDateTime(lastDayOfMonth);

        var date = currentDate;
        while (date <= lastDate)
        {
            var workingCalendar = new WorkingCalendar
            {
                DoctorId = doctorId,
                Date = date,
                StartTime = startTime,
                EndTime = endTime,
                Status = "Available",
                Note = note,
                CreatedOn = DateTime.Now,
                //CreatedBy = doctorId // Giả sử người tạo là chính bác sĩ đó
            };

            result.Add(workingCalendar);
            date = date.AddDays(1);
        }

        return result;
    }
}