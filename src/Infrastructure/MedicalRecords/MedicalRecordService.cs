using ClosedXML;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.FileStorage;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.MedicalRecords;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Treatment;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace FSH.WebApi.Infrastructure.MedicalRecords;
public class MedicalRecordService : IMedicalRecordService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<MedicalRecord> _t;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MedicalRecordService> _logger;
    private readonly IFileStorageService _fileStorageService;

    public MedicalRecordService(ApplicationDbContext db, IStringLocalizer<MedicalRecord> t, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ICurrentUser currentUser, ILogger<MedicalRecordService> logger, IFileStorageService fileStorageService)
    {
        _db = db;
        _t = t;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
        _logger = logger;
        _fileStorageService = fileStorageService;
    }

    public async Task CreateMedicalRecord(CreateMedicalRecordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(x => x.Id == request.AppointmentId
                && (
                    x.Status == Domain.Appointments.AppointmentStatus.Confirmed
                     || x.Status == Domain.Appointments.AppointmentStatus.Come))
                    ?? throw new BadRequestException("Appointment not found");

            var app = new MedicalRecord
            {
                PatientProfileId = appointment.PatientId,
                DoctorProfileId = appointment.DentistId,
                AppointmentId = appointment.Id,
                Date = DateTime.Now,
            };
            var medical = _db.MedicalRecords.Add(app).Entity;

            if (request.BasicExamination != null)
            {
                var basic = new BasicExamination
                {
                    RecordId = medical.Id,
                    ExaminationContent = request.BasicExamination.ExaminationContent,
                    TreatmentPlanNote = request.BasicExamination.TreatmentPlanNote
                };
                var examination = _db.BasicExaminations.Add(basic).Entity;
            }

            if (request.Diagnosis != null)
            {
                var dia = new Diagnosis
                {
                    RecordId = medical.Id,
                    ToothNumber = request.Diagnosis.ToothNumber,
                    TeethConditions = request.Diagnosis.TeethConditions
                };
                var diagnosis = _db.Diagnoses.Add(dia).Entity;
            }

            if (request.Indication != null)
            {
                var indi = new Indication
                {
                    RecordId = medical.Id,
                    IndicationType = request.Indication.IndicationType,
                    Description = request.Indication.Description
                };
                var indication = _db.Indications.Add(indi).Entity;

                if (request.IndicationImages?.Any() == true)
                {
                    foreach (var image in request.IndicationImages)
                    {
                        var indiImage = new PatientImage
                        {
                            IndicationId = indication.Id,
                            ImageUrl = await _fileStorageService.SaveFileAsync(image.Images, cancellationToken),
                            ImageType = image.ImageType
                        };
                        await _db.PatientImages.AddRangeAsync(indiImage);
                    }
                }
            }

            await _db.MedicalRecords.AddAsync(medical);
            await _db.SaveChangesAsync(cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<MedicalRecordResponse> GetMedicalRecordByAppointmentID(DefaultIdType id, CancellationToken cancellationToken)
    {
        try
        {
            var medicalRecord = await _db.MedicalRecords.Where(x => x.AppointmentId == id)
                .Select(medical => new
                {
                    MedicalRecord = medical,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(x => x.Id == medical.DoctorProfileId),
                    Patient = _db.PatientProfiles.FirstOrDefault(x => x.Id == medical.PatientProfileId),
                    Appointment = _db.Appointments.IgnoreQueryFilters().FirstOrDefault(x => x.Id == medical.AppointmentId),

                    BasicExamination = _db.BasicExaminations
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new BasicExaminationRequest
                        {
                            TreatmentPlanNote = x.TreatmentPlanNote,
                            ExaminationContent = x.ExaminationContent
                        }).FirstOrDefault(),
                    Diagnosis = _db.Diagnoses
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new DiagnosisRequest
                        {
                            TeethConditions = x.TeethConditions,
                            ToothNumber = x.ToothNumber
                        }).FirstOrDefault(),
                    Indication = _db.Indications
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new IndicationRequest
                        {
                            IndicationType = x.IndicationType,
                            Description = x.Description
                        }).FirstOrDefault()

                })
                .FirstOrDefaultAsync(cancellationToken);

            if (medicalRecord == null)
            {
                throw new BadRequestException("Not found Medical Record");
            }
            var patient = await _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Patient.UserId);
            var doctor = await _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Doctor.DoctorId);
            return new MedicalRecordResponse
            {
                RecordId = medicalRecord.MedicalRecord.Id,
                PatientId = medicalRecord.MedicalRecord.PatientProfileId,
                DentistId = medicalRecord.MedicalRecord.DoctorProfileId,
                AppointmentId = medicalRecord.MedicalRecord.AppointmentId,
                Date = medicalRecord.MedicalRecord.Date,

                PatientCode = medicalRecord.Patient?.PatientCode,
                PatientName = $"{patient.FirstName} {patient.LastName}",
                DentistName = $"{doctor.FirstName} {doctor.LastName}",
                AppointmentNotes = medicalRecord.Appointment.Notes,

                BasicExamination = medicalRecord.BasicExamination,
                Diagnosis = medicalRecord.Diagnosis,
                Indication = medicalRecord?.Indication
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<MedicalRecordResponse> GetMedicalRecordByID(DefaultIdType id, CancellationToken cancellationToken)
    {
        try
        {
            var medicalRecord = await _db.MedicalRecords.Where(x => x.Id == id)
                .Select(medical => new
                {
                    MedicalRecord = medical,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(x => x.Id == medical.DoctorProfileId),
                    Patient = _db.PatientProfiles.FirstOrDefault(x => x.Id == medical.PatientProfileId),
                    Appointment = _db.Appointments.IgnoreQueryFilters().FirstOrDefault(x => x.Id == medical.AppointmentId),

                    BasicExamination = _db.BasicExaminations
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new BasicExaminationRequest
                        {
                            TreatmentPlanNote = x.TreatmentPlanNote,
                            ExaminationContent = x.ExaminationContent
                        }).FirstOrDefault(),
                    Diagnosis = _db.Diagnoses
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new DiagnosisRequest
                        {
                            TeethConditions = x.TeethConditions,
                            ToothNumber = x.ToothNumber
                        }).FirstOrDefault(),
                    Indication = _db.Indications
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new IndicationRequest
                        {
                            IndicationType = x.IndicationType,
                            Description = x.Description
                        }).FirstOrDefault()

                })
                .FirstOrDefaultAsync(cancellationToken);

            if (medicalRecord == null)
            {
                throw new BadRequestException("Not found Medical Record");
            }
            var patient = await _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Patient.UserId);
            var doctor = await _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Doctor.DoctorId);
            return new MedicalRecordResponse
            {
                RecordId = medicalRecord.MedicalRecord.Id,
                PatientId = medicalRecord.MedicalRecord.PatientProfileId,
                DentistId = medicalRecord.MedicalRecord.DoctorProfileId,
                AppointmentId = medicalRecord.MedicalRecord.AppointmentId,
                Date = medicalRecord.MedicalRecord.Date,

                PatientCode = medicalRecord.Patient?.PatientCode,
                PatientName = $"{patient.FirstName} {patient.LastName}",
                DentistName = $"{doctor.FirstName} {doctor.LastName}",
                AppointmentNotes = _db.Appointments.FirstOrDefaultAsync(x => x.Id == medicalRecord.Appointment.Id).Result.Notes,

                BasicExamination = medicalRecord.BasicExamination,
                Diagnosis = medicalRecord.Diagnosis,
                Indication = medicalRecord?.Indication
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<MedicalRecordResponse>> GetMedicalRecordsByPatientId(string id, DateOnly sDate, DateOnly eDate, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == id)
                ?? throw new NotFoundException($"Patient with ID {id} not found");

            var medicalRecords = _db.MedicalRecords.Where(x => x.PatientProfileId == patient.Id);

            if(sDate != default)
            {
                medicalRecords = medicalRecords.Where(p => p.Date >= DateTime.Parse(sDate.ToString()));
            }
            if (sDate != default)
            {
                medicalRecords = medicalRecords.Where(p => p.Date <= DateTime.Parse(eDate.ToString()));
            }
            var result = await medicalRecords
                .OrderByDescending(x => x.Date)
                .Select(medical => new MedicalRecordResponse
                {
                    RecordId = medical.Id,
                    DentistId = medical.DoctorProfileId,
                    PatientId = medical.PatientProfileId,
                    AppointmentId = medical.AppointmentId,
                    Date = medical.Date,
                    PatientCode = _db.PatientProfiles
                        .Where(x => x.Id == medical.PatientProfileId)
                        .Select(x => x.PatientCode)
                        .FirstOrDefault(),
                    PatientName = _db.Users
                        .Where(x => x.Id == patient.UserId)
                        .Select(x => $"{x.FirstName} {x.LastName}")
                        .FirstOrDefault(),
                    DentistName = _db.Users
                        .Where(x => x.Id == medical.DoctorProfile.DoctorId)
                        .Select(x => $"{x.FirstName} {x.LastName}")
                        .FirstOrDefault(),
                    AppointmentNotes = _db.Appointments
                        .Where(x => x.Id == medical.Appointment.Id)
                        .Select(x => x.Notes)
                        .FirstOrDefault(),

                    BasicExamination = _db.BasicExaminations
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new BasicExaminationRequest
                        {
                            TreatmentPlanNote = x.TreatmentPlanNote,
                            ExaminationContent = x.ExaminationContent
                        }).FirstOrDefault(),
                    Diagnosis = _db.Diagnoses
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new DiagnosisRequest
                        {
                            TeethConditions = x.TeethConditions,
                            ToothNumber = x.ToothNumber
                        }).FirstOrDefault(),
                    Indication = _db.Indications
                        .Where(x => x.RecordId == medical.Id)
                        .Select(x => new IndicationRequest
                        {
                            IndicationType = x.IndicationType,
                            Description = x.Description
                        }).FirstOrDefault()

                })
                .ToListAsync(cancellationToken);

            return result;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }
}
