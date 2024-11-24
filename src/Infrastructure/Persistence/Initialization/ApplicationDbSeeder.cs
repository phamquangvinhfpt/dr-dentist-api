using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Threading;

namespace FSH.WebApi.Infrastructure.Persistence.Initialization;

internal class ApplicationDbSeeder
{
    private readonly FSHTenantInfo _currentTenant;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CustomSeederRunner _seederRunner;
    private readonly ILogger<ApplicationDbSeeder> _logger;
    private readonly ApplicationDbContext _db;
    private readonly ISerializerService _serializerService;

    public ApplicationDbSeeder(ISerializerService serializerService, ApplicationDbContext db, FSHTenantInfo currentTenant, RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager, CustomSeederRunner seederRunner, ILogger<ApplicationDbSeeder> logger)
    {
        _currentTenant = currentTenant;
        _roleManager = roleManager;
        _userManager = userManager;
        _seederRunner = seederRunner;
        _logger = logger;
        _db = db;
        _serializerService = serializerService;
    }

    public async Task SeedDatabaseAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(dbContext);
        await SeedAdminUserAsync();
        await SeedAdmin2UserAsync();
        await SeedStaffUserAsync();
        await SeedUserAsync();
        await SeedServiceAsync();
        await SeedAppointmentAsync();
        await SeedAppointmentInforAsync();
        await _seederRunner.RunSeedersAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(ApplicationDbContext dbContext)
    {
        foreach (string roleName in FSHRoles.DefaultRoles)
        {
            if (await _roleManager.Roles.SingleOrDefaultAsync(r => r.Name == roleName)
                is not ApplicationRole role)
            {
                // Create the role
                _logger.LogInformation("Seeding {role} Role for '{tenantId}' Tenant.", roleName, _currentTenant.Id);
                role = new ApplicationRole(roleName, $"{roleName} Role for {_currentTenant.Id} Tenant");
                await _roleManager.CreateAsync(role);
            }

            // Assign permissions
            if (roleName == FSHRoles.Dentist)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Dentist, role);
            }
            else if (roleName == FSHRoles.Admin)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Admin, role);

                if (_currentTenant.Id == MultitenancyConstants.Root.Id)
                {
                   await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Root, role);
                }
            }
            else if (roleName == FSHRoles.Staff)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Staff, role);
            }
            else if (roleName == FSHRoles.Patient)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Patient, role);
            }
            else if (roleName == FSHRoles.Guest)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Guest, role);
            }
        }
    }

    private async Task AssignPermissionsToRoleAsync(ApplicationDbContext dbContext, IReadOnlyList<FSHPermission> permissions, ApplicationRole role)
    {
        var currentClaims = await _roleManager.GetClaimsAsync(role);
        foreach (var permission in permissions)
        {
            if (!currentClaims.Any(c => c.Type == FSHClaims.Permission && c.Value == permission.Name))
            {
                _logger.LogInformation("Seeding {role} Permission '{permission}' for '{tenantId}' Tenant.", role.Name, permission.Name, _currentTenant.Id);
                dbContext.RoleClaims.Add(new ApplicationRoleClaim
                {
                    RoleId = role.Id,
                    ClaimType = FSHClaims.Permission,
                    ClaimValue = permission.Name,
                    CreatedBy = "ApplicationDbSeeder"
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }

    private async Task SeedAdminUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace(_currentTenant.AdminEmail))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == _currentTenant.AdminEmail)
            is not ApplicationUser adminUser)
        {
            string adminUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Admin}".ToLowerInvariant();
            adminUser = new ApplicationUser
            {
                Id = "f56b04ea-d95d-4fab-be50-2fd2ca1561ff",
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Admin,
                Email = _currentTenant.AdminEmail,
                UserName = adminUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = _currentTenant.AdminEmail?.ToUpperInvariant(),
                NormalizedUserName = adminUserName.ToUpperInvariant(),
                IsActive = true
            };

            _logger.LogInformation("Seeding Default Admin User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            adminUser.PasswordHash = password.HashPassword(adminUser, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(adminUser);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(adminUser, FSHRoles.Admin))
        {
            _logger.LogInformation("Assigning Admin Role to Admin User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(adminUser, FSHRoles.Admin);
        }
    }

    private async Task SeedAdmin2UserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace("admin2@root.com"))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "admin2@root.com")
                       is not ApplicationUser admin2User)
        {
            admin2User = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Admin,
                Email = "admin2@root.com",
                UserName = "admin2.root",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "admin2@root.com"?.ToUpperInvariant(),
                NormalizedUserName = "admin2.root".ToUpperInvariant(),
                IsActive = true
            };

            _logger.LogInformation("Seeding Default Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            admin2User.PasswordHash = password.HashPassword(admin2User, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(admin2User);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(admin2User, FSHRoles.Admin))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(admin2User, FSHRoles.Admin);
        }
    }

    private async Task SeedStaffUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace("staff@root.com"))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "staff@root.com")
                          is not ApplicationUser staffUser)
        {
            string staffUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Staff}".ToLowerInvariant();
            staffUser = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Staff,
                Email = "staff@root.com",
                UserName = staffUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "staff@root.com"?.ToUpperInvariant(),
                NormalizedUserName = staffUserName.ToUpperInvariant(),
                IsActive = true
            };
            _logger.LogInformation("Seeding Default Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            staffUser.PasswordHash = password.HashPassword(staffUser, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(staffUser);
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "staff@root.com")
                          is not ApplicationUser staffUser2)
        {
            string staffUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Staff}".ToLowerInvariant();
            
            staffUser2 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = $"{FSHRoles.Staff} 2",
                Email = "staff2@root.com",
                UserName = "Staff 2",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "staff2@root.com"?.ToUpperInvariant(),
                NormalizedUserName = "Staff 2".ToUpperInvariant(),
                IsActive = true
            };
            _logger.LogInformation("Seeding Default Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            staffUser2.PasswordHash = password.HashPassword(staffUser2, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(staffUser2);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(staffUser, FSHRoles.Staff))
        {
            _logger.LogInformation("Assigning Staff Role to Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(staffUser, FSHRoles.Staff);
        }
        if (!await _userManager.IsInRoleAsync(staffUser2, FSHRoles.Staff))
        {
            _logger.LogInformation("Assigning Staff Role to Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(staffUser2, FSHRoles.Staff);
        }
    }
    private async Task SeedServiceAsync()
    {
        _logger.LogInformation("Seeding Service.");
        if (_db.Services.Count() < 1)
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dataPath = Path.Combine(path!, "Services", "ProcedureData.json");
            _logger.LogInformation("Started to Seed Service.");
            string proceData = await File.ReadAllTextAsync(dataPath);
            var procedures = _serializerService.Deserialize<List<Procedure>>(proceData);

            await _db.Procedures.AddRangeAsync(procedures);
            await _db.SaveChangesAsync();

            dataPath = Path.Combine(path!, "Services", "ServiceData.json");
            string serviceData = await File.ReadAllTextAsync(dataPath);
            var services = _serializerService.Deserialize<List<Service>>(serviceData);
            var totalPrice = GetToltalPrice(procedures);
            foreach (var service in services)
            {
                service.TotalPrice = totalPrice;
                service.CreatedOn = DateTime.Now;
            }
            await _db.Services.AddRangeAsync(services);
            await _db.SaveChangesAsync();

            foreach (var service in services)
            {
                for (int i = 0; i < procedures.Count(); i++)
                {
                    _db.ServiceProcedures.Add(new ServiceProcedures
                    {
                        ServiceId = service.Id,
                        ProcedureId = procedures[i].Id,
                        StepOrder = i + 1,
                        CreatedOn = DateTime.Now,
                        CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                    });
                }
            }
            await _db.SaveChangesAsync();
            var pros = await _db.Procedures.ToListAsync();
            foreach (var pro in pros)
            {
                pro.CreatedOn = DateTime.Now;
                pro.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            var sers = await _db.Services.ToListAsync();
            foreach (var i in sers)
            {
                i.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            var sps = await _db.ServiceProcedures.ToListAsync();
            foreach (var i in sps)
            {
                i.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Seeded Services.");
        }
    }

    private async Task SeedAppointmentAsync()
    {
        _logger.LogInformation("Seeded Appointment.");
        try
        {
            if (!_db.Appointments.Any())
            {
                var serviceProcedures = _db.ServiceProcedures
                .GroupBy(p => p.ServiceId)
                .Select(p => new
                {
                    ServiceID = p.Key,
                    ProcedureIDs = p.Select(p => p.ProcedureId).ToList(),
                })
                .ToList();

                var patients = _db.PatientProfiles.Select(p => new { PatientID = p.Id }).ToList();

                var doctor = _db.DoctorProfiles.Select(p => new { DoctorId = p.Id }).ToList();

                var appointments = new List<Appointment>();
                var random = new Random();

                var currentDate = DateOnly.FromDateTime(DateTime.Now);

                for (int dayOffset = -270; dayOffset <= 30; dayOffset++)
                {
                    var appointmentDate = currentDate.AddDays(dayOffset);

                    if (appointmentDate.DayOfWeek == DayOfWeek.Saturday ||
                        appointmentDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        continue;
                    }

                    var appointmentsPerDay = random.Next(4, 7);
                    //for(int i = 0; i < appointmentsPerDay; i++)
                    //{
                        var startHour = random.Next(8, 17);
                        var startTime = new TimeSpan(startHour, 0, 0);

                        var duration = TimeSpan.FromMinutes(30);

                        var patientId = patients[random.Next(patients.Count)];
                        var doctorId = doctor[random.Next(doctor.Count)];
                        var service = serviceProcedures[random.Next(serviceProcedures.Count)];

                        AppointmentStatus status;
                        bool canProvideFeeback = false;

                        // Xác định status dựa vào ngày
                        if (dayOffset < 0)
                        {
                            status = AppointmentStatus.Success;
                            canProvideFeeback = true;
                        }
                        else if (dayOffset == 0)
                        {
                            var currentTime = DateTime.Now.TimeOfDay;
                            if (startTime < currentTime)
                            {
                                status = AppointmentStatus.Success;
                                canProvideFeeback = true;
                            }
                            else
                            {
                                status = AppointmentStatus.Confirmed;
                            }
                        }
                        else
                        {
                            status = AppointmentStatus.Confirmed;
                        }

                        var appointment = new Appointment
                        {
                            PatientId = patientId.PatientID,
                            DentistId = doctorId.DoctorId,
                            ServiceId = service.ServiceID.Value,
                            AppointmentDate = appointmentDate,
                            StartTime = startTime,
                            Duration = duration,
                            Status = status,
                            SpamCount = 0,
                            canFeedback = canProvideFeeback,
                            CreatedOn = DateTime.Now.AddDays(dayOffset - random.Next(0, 3)),
                            CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                        };

                        var entry = _db.Appointments.Add(appointment).Entity;

                        await _db.SaveChangesAsync();

                        _logger.LogInformation($"Successfully seeded {appointments.Count} appointments.");
                    //}
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    private async Task SeedNonDoctorAppointmentAsync()
    {
        _logger.LogInformation("Seeded Appointment.");
        try
        {
            var serviceProcedures = _db.ServiceProcedures
                .GroupBy(p => p.ServiceId)
                .Select(p => new
                {
                    ServiceID = p.Key,
                    ProcedureIDs = p.Select(p => p.ProcedureId).ToList(),
                })
                .ToList();

            var patients = _db.PatientProfiles.Select(p => new { PatientID = p.Id }).ToList();

            var appointments = new List<Appointment>();
            var random = new Random();

            var currentDate = DateOnly.FromDateTime(DateTime.Now);

            for (int dayOffset = 5; dayOffset <= 20; dayOffset++)
            {
                var appointmentDate = currentDate.AddDays(dayOffset);

                if (appointmentDate.DayOfWeek == DayOfWeek.Saturday ||
                    appointmentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                var appointmentsPerDay = random.Next(5, 11);
                var startHour = random.Next(8, 17);
                var startTime = new TimeSpan(startHour, 0, 0);

                var duration = TimeSpan.FromMinutes(30);

                var patientId = patients[random.Next(patients.Count)];
                var service = serviceProcedures[random.Next(serviceProcedures.Count)];

                var appointment = _db.Appointments.Add(new Appointment
                {
                    PatientId = patientId.PatientID,
                    ServiceId = service.ServiceID.Value,
                    AppointmentDate = appointmentDate,
                    StartTime = startTime,
                    Duration = duration,
                    Status = AppointmentStatus.Confirmed,
                    SpamCount = 0,
                    canFeedback = false,
                    CreatedOn = DateTime.Now.AddDays(dayOffset - random.Next(0, 3)),
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                }).Entity;

                var s = _db.Services.FirstOrDefault(p => p.Id == appointment.ServiceId);
                var payment = _db.Payments.Add(new Domain.Payments.Payment
                {
                    PatientProfileId = appointment.PatientId,
                    AppointmentId = appointment.Id,
                    ServiceId = appointment.ServiceId,
                    DepositAmount = 0,
                    Amount = s.TotalPrice,
                    RemainingAmount = 0,
                    Method = Domain.Payments.PaymentMethod.None,
                    Status = Domain.Payments.PaymentStatus.Incomplete,
                }).Entity;
                var payDetail = new List<Domain.Payments.PaymentDetail>();
                var sps = _db.ServiceProcedures.Where(p => p.ServiceId == s.Id)
                    .OrderBy(p => p.StepOrder)
                    .Select(s => new
                    {
                        SP = s,
                        Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                    })
                    .ToList();
                var val = _db.WorkingCalendars.Add(new WorkingCalendar
                {
                    PatientId = patientId.PatientID,
                    AppointmentId = appointment.Id,
                    Date = appointment.AppointmentDate,
                    StartTime = appointment.StartTime,
                    EndTime = appointment.StartTime.Add(appointment.Duration),
                    Status = Domain.Identity.CalendarStatus.Booked,
                    Type = AppointmentType.Appointment,
                });
                foreach (var item in sps)
                {
                    payDetail.Add(new Domain.Payments.PaymentDetail
                    {
                        PaymentID = payment.Id,
                        ProcedureID = item.Procedure.Id,
                        PaymentAmount = item.Procedure.Price,
                        PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                    });
                }
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Successfully seeded {appointments.Count} appointments.");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    private async Task SeedAppointmentInforAsync()
    {
        try
        {
            _logger.LogInformation($"Seeding Payment");
            if (!_db.Payments.Any())
            {
                var random = new Random();
                var currentDate = DateOnly.FromDateTime(DateTime.Now);
                var appointments = _db.Appointments.ToList();

                foreach (var appointment in appointments)
                {
                    if (appointment.Status == AppointmentStatus.Success)
                    {
                        var s = _db.Services.FirstOrDefault(p => p.Id == appointment.ServiceId);
                        var payment = _db.Payments.Add(new Domain.Payments.Payment
                        {
                            PatientProfileId = appointment.PatientId,
                            AppointmentId = appointment.Id,
                            ServiceId = appointment.ServiceId,
                            DepositAmount = s.TotalPrice * 0.3,
                            DepositDate = DateOnly.FromDateTime(appointment.CreatedOn),
                            Amount = s.TotalPrice,
                            RemainingAmount = 0,
                            Method = Domain.Payments.PaymentMethod.BankTransfer,
                            Status = Domain.Payments.PaymentStatus.Completed,
                            FinalPaymentDate = appointment.AppointmentDate,
                        }).Entity;
                        var payDetail = new List<Domain.Payments.PaymentDetail>();
                        var sps = _db.ServiceProcedures.Where(p => p.ServiceId == s.Id)
                            .OrderBy(p => p.StepOrder)
                            .Select(s => new
                            {
                                SP = s,
                                Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                            })
                            .ToList();
                        var next = 4;
                        var d = appointment.AppointmentDate;
                        foreach (var item in sps)
                        {
                            payDetail.Add(new Domain.Payments.PaymentDetail
                            {
                                PaymentID = payment.Id,
                                ProcedureID = item.Procedure.Id,
                                PaymentAmount = item.Procedure.Price - item.Procedure.Price * 0.3,
                                PaymentStatus = Domain.Payments.PaymentStatus.Completed,
                            });
                            var date = d.AddDays(next++);
                            // seed treatment plan
                            bool c = date > currentDate;
                            var t = _db.TreatmentPlanProcedures.Add(new Domain.Treatment.TreatmentPlanProcedures
                            {
                                ServiceProcedureId = item.SP.Id,
                                AppointmentID = appointment.Id,
                                DoctorID = appointment.DentistId,
                                Status = c ? Domain.Treatment.TreatmentPlanStatus.Active : Domain.Treatment.TreatmentPlanStatus.Completed,
                                Price = item.Procedure.Price,
                                StartTime = appointment.StartTime,
                                DiscountAmount = 0.3,
                                TotalCost = item.Procedure.Price - item.Procedure.Price * 0.3,
                                StartDate = date,
                            }).Entity;
                            var w = _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
                            {
                                DoctorId = appointment.DentistId,
                                PatientId = appointment.PatientId,
                                AppointmentId = appointment.Id,
                                PlanID = t.Id,
                                Date = t.StartDate,
                                StartTime = t.StartTime,
                                EndTime = t.StartTime.Value.Add(TimeSpan.FromMinutes(30)),
                                Status = c ? Domain.Identity.CalendarStatus.Booked : CalendarStatus.Completed,
                                Type = item.SP.StepOrder == 1 ? AppointmentType.Appointment : AppointmentType.FollowUp,
                            });

                            // seed prescription

                            var pre = _db.Prescriptions.Add(new Domain.Treatment.Prescription
                            {
                                TreatmentID = t.Id,
                                DoctorID = appointment.DentistId,
                                PatientID = appointment.PatientId,
                                Notes = "Use every day"
                            }).Entity;

                            var preItem = _db.PrescriptionItems.Add(new Domain.Treatment.PrescriptionItem
                            {
                                PrescriptionId = pre.Id,
                                MedicineName = "Chống viêm",
                                Dosage = "1 viên 1 lần",
                                Frequency = "1 lần 1 ngày",
                            });
                            if (!c)
                            {
                                appointment.Status = AppointmentStatus.Done;
                            }
                            else
                            {
                                appointment.Status = AppointmentStatus.Success;
                            }
                        }

                        // Seed medical record

                        var medical = _db.MedicalRecords.Add(new Domain.Examination.MedicalRecord
                        {
                            DoctorProfileId = appointment.DentistId,
                            PatientProfileId = appointment.PatientId,
                            Date = DateTime.Parse(appointment.AppointmentDate.ToString()),
                            AppointmentId = appointment.Id,
                        }).Entity;

                        _db.BasicExaminations.Add(new Domain.Examination.BasicExamination
                        {
                            RecordId = medical.Id,
                            ExaminationContent = "Good",
                            TreatmentPlanNote = "Ok"
                        });

                        _db.Diagnoses.Add(new Domain.Examination.Diagnosis
                        {
                            RecordId = medical.Id,
                            ToothNumber = 5,
                            TeethConditions = new[] { "Sâu Răng" }
                        });

                        _db.Indications.Add(new Domain.Examination.Indication
                        {
                            RecordId = medical.Id,
                            IndicationType = new[] { "Khác" },
                            Description = "Sâu Răng"
                        });

                        if(appointment.Status == AppointmentStatus.Done)
                        {
                            _db.Feedbacks.Add(new Feedback
                            {
                                PatientProfileId = appointment.PatientId,
                                DoctorProfileId = appointment.DentistId,
                                ServiceId = s.Id,
                                AppointmentId = appointment.Id,
                                Rating = random.Next(3, 5),
                                Message = $"Feedback for appointment on {appointment.AppointmentDate}",
                                CreatedBy = appointment.PatientId
                            });
                        }

                    }
                    else
                    {
                        var s = _db.Services.FirstOrDefault(p => p.Id == appointment.ServiceId);
                        var dAmount = 0.3;
                        var payment = _db.Payments.Add(new Domain.Payments.Payment
                        {
                            PatientProfileId = appointment.PatientId,
                            AppointmentId = appointment.Id,
                            ServiceId = appointment.ServiceId,
                            DepositAmount = 0.3,
                            DepositDate = DateOnly.FromDateTime(appointment.CreatedOn),
                            Amount = s.TotalPrice,
                            RemainingAmount = s.TotalPrice - (s.TotalPrice * dAmount),
                            Method = Domain.Payments.PaymentMethod.None,
                            Status = Domain.Payments.PaymentStatus.Incomplete,
                        }).Entity;
                        var payDetail = new List<Domain.Payments.PaymentDetail>();
                        var sps = _db.ServiceProcedures.Where(s => s.ServiceId == s.Id)
                            .OrderBy(p => p.StepOrder)
                            .Select(s => new
                            {
                                SP = s,
                                Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                            })
                            .ToList();
                        var next = 1;
                        foreach (var item in sps)
                        {
                            payDetail.Add(new Domain.Payments.PaymentDetail
                            {
                                PaymentID = payment.Id,
                                ProcedureID = item.Procedure.Id,
                                PaymentAmount = item.Procedure.Price - (item.Procedure.Price * dAmount),
                                PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                            });

                            // seed treatment plan
                            var t = new Domain.Treatment.TreatmentPlanProcedures
                            {
                                ServiceProcedureId = item.SP.Id,
                                AppointmentID = appointment.Id,
                                DoctorID = appointment.DentistId,
                                Status = item.SP.StepOrder == 1 ? Domain.Treatment.TreatmentPlanStatus.Active : Domain.Treatment.TreatmentPlanStatus.Pending,
                                Price = item.Procedure.Price,
                                StartTime = appointment.StartTime,
                                DiscountAmount = dAmount,
                                TotalCost = item.Procedure.Price - (item.Procedure.Price * dAmount),
                            };
                            if (item.SP.StepOrder == 1)
                            {
                                t.StartDate = appointment.AppointmentDate.AddDays(+next++);
                            }
                            t = _db.TreatmentPlanProcedures.Add(t).Entity;
                        }
                        _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
                        {
                            PatientId = appointment.PatientId,
                            DoctorId = appointment.DentistId,
                            AppointmentId = appointment.Id,
                            Date = appointment.AppointmentDate,
                            StartTime = appointment.StartTime,
                            EndTime = appointment.StartTime.Add(TimeSpan.FromMinutes(30)),
                            Status = Domain.Identity.CalendarStatus.Booked,
                            Type = AppointmentType.Appointment,
                        });
                    }
                }
                _db.SaveChanges();
                await SeedNonDoctorAppointmentAsync();
                await SeedPatientDemoAsync();
            }
            _logger.LogInformation($"Seeded Payment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }



    private async Task SeedUserAsync()
    {
        _logger.LogInformation("Seeding Users.");
        if (_db.Users.Count() <= 5)
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string userDataPath = Path.Combine(path!, "Identity", "UserData.json");
            string patientDataPath = Path.Combine(path!, "Identity", "PatientProfileData.json");
            string doctorDataPath = Path.Combine(path!, "Identity", "DoctorProfileData.json");
            _logger.LogInformation("Started to Seed Users.");
            string userData = await File.ReadAllTextAsync(userDataPath);
            string patientData = await File.ReadAllTextAsync(patientDataPath);
            string doctorData = await File.ReadAllTextAsync(doctorDataPath);
            var users = _serializerService.Deserialize<List<ApplicationUser>>(userData);
            var patients = _serializerService.Deserialize<List<PatientProfile>>(patientData);
            var doctors = _serializerService.Deserialize<List<DoctorProfile>>(doctorData);
            List<ApplicationUser> doctor = new List<ApplicationUser>();
            List<ApplicationUser> patient = new List<ApplicationUser>();
            int flash = 0;
            int d_profile_index = 0;
            int p_profile_index = 0;
            foreach (var user in users)
            {
                var entry = _db.Users.Add(user).Entity;
                if (flash < 10)
                {
                    doctors[d_profile_index].DoctorId = entry.Id;
                    doctor.Add(entry);
                    flash++;
                    d_profile_index++;
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
            await _db.SaveChangesAsync();
            foreach (var user in doctor)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Dentist))
                {
                    _logger.LogInformation("Assigning Dentist Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Dentist);
                }
                var profile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == user.Id);
                profile.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            foreach (var user in patient)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Patient))
                {
                    _logger.LogInformation("Assigning Patient Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Patient);
                }
            }
            foreach (var user in doctor)
            {
                var profile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == user.Id);
                profile.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            foreach (var user in patient)
            {
                var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                profile.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Seeded Users.");
        }
    }

    private async Task SeedPatientDemoAsync()
    {
        //patient demo 1
        var patientDemo1 = new ApplicationUser
        {
            FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
            LastName = FSHRoles.Admin,
            Email = "patient1@demo.com",
            UserName = "patient1.demo",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            NormalizedEmail = "patient1@demo.com"?.ToUpperInvariant(),
            NormalizedUserName = "patient1.demo".ToUpperInvariant(),
            IsActive = true
        };

        _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
        var password = new PasswordHasher<ApplicationUser>();
        patientDemo1.PasswordHash = password.HashPassword(patientDemo1, MultitenancyConstants.DefaultPassword);
        await _userManager.CreateAsync(patientDemo1);

        //patient demo 2
        var patientDemo2 = new ApplicationUser
        {
            FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
            LastName = FSHRoles.Admin,
            Email = "patient2@demo.com",
            UserName = "patient2.demo",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            NormalizedEmail = "patient2@demo.com"?.ToUpperInvariant(),
            NormalizedUserName = "patient2.demo".ToUpperInvariant(),
            IsActive = true
        };

        _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
        patientDemo2.PasswordHash = password.HashPassword(patientDemo2, MultitenancyConstants.DefaultPassword);
        await _userManager.CreateAsync(patientDemo2);

        //patient demo 3
        var patientDemo3 = new ApplicationUser
        {
            FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
            LastName = FSHRoles.Admin,
            Email = "patient3@demo.com",
            UserName = "patient3.demo",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            NormalizedEmail = "patient3@demo.com"?.ToUpperInvariant(),
            NormalizedUserName = "patient3.demo".ToUpperInvariant(),
            IsActive = true
        };

        _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
        patientDemo3.PasswordHash = password.HashPassword(patientDemo3, MultitenancyConstants.DefaultPassword);
        await _userManager.CreateAsync(patientDemo3);

        //patient demo 4
        var patientDemo4 = new ApplicationUser
        {
            FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
            LastName = FSHRoles.Admin,
            Email = "patient4@demo.com",
            UserName = "patient4.demo",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            NormalizedEmail = "patient4@demo.com"?.ToUpperInvariant(),
            NormalizedUserName = "patient4.demo".ToUpperInvariant(),
            IsActive = true
        };

        _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
        patientDemo4.PasswordHash = password.HashPassword(patientDemo4, MultitenancyConstants.DefaultPassword);
        await _userManager.CreateAsync(patientDemo4);

        //patient demo 5
        var patientDemo5 = new ApplicationUser
        {
            FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
            LastName = FSHRoles.Admin,
            Email = "patient5@demo.com",
            UserName = "patient5.demo",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            NormalizedEmail = "patient5@demo.com"?.ToUpperInvariant(),
            NormalizedUserName = "patient5.demo".ToUpperInvariant(),
            IsActive = true
        };

        _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
        patientDemo5.PasswordHash = password.HashPassword(patientDemo5, MultitenancyConstants.DefaultPassword);
        await _userManager.CreateAsync(patientDemo5);
        // Assign role to user
        if (!await _userManager.IsInRoleAsync(patientDemo1, FSHRoles.Patient))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(patientDemo1, FSHRoles.Patient);
            var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
            string code = "BN";
            if (amountP.Count() < 10)
            {
                code += $"00{amountP.Count()}";
            }
            else if (amountP.Count() < 100)
            {
                code += $"0{amountP.Count()}";
            }
            else
            {
                code += $"{amountP.Count()}";
            }
            var pa = await _userManager.FindByEmailAsync(patientDemo1.Email);
            await _db.PatientProfiles.AddAsync(new PatientProfile
            {
                UserId = pa.Id,
                PatientCode = code,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
            });
            _db.SaveChanges();
        }
        if (!await _userManager.IsInRoleAsync(patientDemo2, FSHRoles.Patient))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(patientDemo2, FSHRoles.Patient);
            var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
            string code = "BN";
            if (amountP.Count() < 10)
            {
                code += $"00{amountP.Count()}";
            }
            else if (amountP.Count() < 100)
            {
                code += $"0{amountP.Count()}";
            }
            else
            {
                code += $"{amountP.Count()}";
            }
            var pa = await _userManager.FindByEmailAsync(patientDemo2.Email);
            await _db.PatientProfiles.AddAsync(new PatientProfile
            {
                UserId = pa.Id,
                PatientCode = code,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
            });
            _db.SaveChanges();
        }
        if (!await _userManager.IsInRoleAsync(patientDemo3, FSHRoles.Patient))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(patientDemo3, FSHRoles.Patient);
            var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
            string code = "BN";
            if (amountP.Count() < 10)
            {
                code += $"00{amountP.Count()}";
            }
            else if (amountP.Count() < 100)
            {
                code += $"0{amountP.Count()}";
            }
            else
            {
                code += $"{amountP.Count()}";
            }
            var pa = await _userManager.FindByEmailAsync(patientDemo3.Email);
            await _db.PatientProfiles.AddAsync(new PatientProfile
            {
                UserId = pa.Id,
                PatientCode = code,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
            });
            _db.SaveChanges();
        }
        if (!await _userManager.IsInRoleAsync(patientDemo4, FSHRoles.Patient))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(patientDemo4, FSHRoles.Patient);
            var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
            string code = "BN";
            if (amountP.Count() < 10)
            {
                code += $"00{amountP.Count()}";
            }
            else if (amountP.Count() < 100)
            {
                code += $"0{amountP.Count()}";
            }
            else
            {
                code += $"{amountP.Count()}";
            }
            var pa = await _userManager.FindByEmailAsync(patientDemo4.Email);
            await _db.PatientProfiles.AddAsync(new PatientProfile
            {
                UserId = pa.Id,
                PatientCode = code,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
            });
            _db.SaveChanges();
        }
        if (!await _userManager.IsInRoleAsync(patientDemo5, FSHRoles.Patient))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(patientDemo5, FSHRoles.Patient);
            var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
            string code = "BN";
            if (amountP.Count() < 10)
            {
                code += $"00{amountP.Count()}";
            }
            else if (amountP.Count() < 100)
            {
                code += $"0{amountP.Count()}";
            }
            else
            {
                code += $"{amountP.Count()}";
            }
            var pa = await _userManager.FindByEmailAsync(patientDemo5.Email);
            await _db.PatientProfiles.AddAsync(new PatientProfile
            {
                UserId = pa.Id,
                PatientCode = code,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
            });
            _db.SaveChanges();
        }
    }
    private double GetToltalPrice(List<Procedure> procedures)
    {
        double result = 0;
        foreach (Procedure procedure in procedures)
        {
            result += procedure.Price;
        }
        return result;
    }
}