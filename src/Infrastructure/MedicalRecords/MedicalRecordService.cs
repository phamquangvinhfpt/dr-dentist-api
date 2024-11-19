using ClosedXML;
using DocumentFormat.OpenXml.Office2010.Excel;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.MedicalRecords;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Treatment;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.MedicalRecords;
public class MedicalRecordService : IMedicalRecordService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<MedicalRecord> _t;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MedicalRecordService> _logger;

    public MedicalRecordService(ApplicationDbContext db, IStringLocalizer<MedicalRecord> t, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ICurrentUser currentUser, ILogger<MedicalRecordService> logger)
    {
        _db = db;
        _t = t;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task CreateMedicalRecord(CreateMedicalRecordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(x => x.Id == request.AppointmentId
                && (
                    x.Status == Domain.Appointments.AppointmentStatus.Confirmed
                     || x.Status == Domain.Appointments.AppointmentStatus.Success))
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
                            ImageUrl = image.ImageUrl,
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

    public async Task<string> DeleteMedicalRecordByPatientID(string id, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == id)
                ?? throw new NotFoundException($"Patient with ID {id} not found");

            var medicalRecords = await _db.MedicalRecords
            .Where(x => x.PatientProfile.UserId == id)
            .ToListAsync();

            if (medicalRecords == null || !medicalRecords.Any())
                throw new NotFoundException($"No medical records found for patient");

            var currentUser = _currentUser.GetUserId();

            foreach (var record in medicalRecords)
            {
                record.DeletedOn = DateTime.Now;
                record.DeletedBy = currentUser;
            }

            _db.MedicalRecords.UpdateRange(medicalRecords);
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Delete medical records success"];

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> DeleteMedicalRecordID(DefaultIdType id, CancellationToken cancellationToken)
    {
        try
        {
            var medical = _db.MedicalRecords.FirstOrDefault(x => x.Id == id);
            if (medical == null) throw new BadRequestException("Not found Medical record");

            _db.MedicalRecords.Remove(medical);
            await _db.SaveChangesAsync(cancellationToken);
            return _t["Delete successfully"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
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

            return new MedicalRecordResponse
            {
                RecordId = medicalRecord.MedicalRecord.Id,
                PatientId = medicalRecord.MedicalRecord.PatientProfileId,
                DentistId = medicalRecord.MedicalRecord.DoctorProfileId,
                AppointmentId = medicalRecord.MedicalRecord.AppointmentId,
                Date = medicalRecord.MedicalRecord.Date,

                PatientCode = medicalRecord.Patient?.PatientCode,
                PatientName = _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Patient.UserId).Result.UserName,
                DentistName = _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Doctor.DoctorId).Result.UserName,
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

            return new MedicalRecordResponse
            {
                RecordId = medicalRecord.MedicalRecord.Id,
                PatientId = medicalRecord.MedicalRecord.PatientProfileId,
                DentistId = medicalRecord.MedicalRecord.DoctorProfileId,
                AppointmentId = medicalRecord.MedicalRecord.AppointmentId,
                Date = medicalRecord.MedicalRecord.Date,

                PatientCode = medicalRecord.Patient?.PatientCode,
                PatientName = _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Patient.UserId).Result.UserName,
                DentistName = _db.Users.FirstOrDefaultAsync(x => x.Id == medicalRecord.Doctor.DoctorId).Result.UserName,
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

    public async Task<List<MedicalRecordResponse>> GetMedicalRecordsByPatientId(string id, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == id)
                ?? throw new NotFoundException($"Patient with ID {id} not found");

            //var patient = await _db.PatientProfiles
            //.Where(p => p.UserId == id)
            //.Select(p => new
            //{
            //    Profile = p,
            //    PatientName = _db.Users
            //        .Where(u => u.Id == p.UserId)
            //        .Select(u => u.UserName)
            //        .FirstOrDefault()
            //})
            //.FirstOrDefaultAsync(cancellationToken);

            //if (patient == null)
            //{
            //    throw new BadRequestException("Patient not found");
            //}

            var medicalRecords = await _db.MedicalRecords
                .Where(x => x.PatientProfileId == patient.Id)
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
                        .Select(x => x.UserName)
                        .FirstOrDefault(),
                    DentistName = _db.Users
                        .Where(x => x.Id == medical.DoctorProfile.DoctorId)
                        .Select(x => x.UserName)
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

            if (medicalRecords.Count() < 1) throw new NotFoundException("Patient don't have Medical Record");

            return medicalRecords;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task UpdateMedicalRecord(UpdateMedicalRecordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Get Medical record
            var medical = await _db.MedicalRecords.Where(x => x.Id == request.RecordID).FirstOrDefaultAsync();
            if (medical == null)
                throw new BadRequestException("Not found Medical record");

            // Get basic
            var basicExamination = await _db.BasicExaminations
                .FirstOrDefaultAsync(x => x.RecordId == request.RecordID);

            // Get Diagnosi
            var diagnosis = await _db.Diagnoses
                .FirstOrDefaultAsync(x => x.RecordId == request.RecordID);

            // Get Indication and Images
            var indication = await _db.Indications
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x => x.RecordId == request.RecordID);

            // Update Basic Examination
            if (request.BasicExamination != null)
            {
                if (basicExamination == null)
                {
                    basicExamination = new BasicExamination
                    {
                        RecordId = request.RecordID,
                        ExaminationContent = request.BasicExamination.ExaminationContent,
                        TreatmentPlanNote = request.BasicExamination.TreatmentPlanNote
                    };
                    await _db.BasicExaminations.AddAsync(basicExamination);
                }
                else
                {
                    basicExamination.ExaminationContent = request.BasicExamination.ExaminationContent;
                    basicExamination.TreatmentPlanNote = request.BasicExamination.TreatmentPlanNote;
                    _db.BasicExaminations.Update(basicExamination);
                }
            }

            // Update Diagnosis
            if (request.Diagnosis != null)
            {
                if (diagnosis == null)
                {
                    diagnosis = new Diagnosis
                    {
                        RecordId = request.RecordID,
                        ToothNumber = request.Diagnosis.ToothNumber,
                        TeethConditions = request.Diagnosis.TeethConditions
                    };
                    await _db.Diagnoses.AddAsync(diagnosis);
                }
                else
                {
                    diagnosis.ToothNumber = request.Diagnosis.ToothNumber;
                    diagnosis.TeethConditions = request.Diagnosis.TeethConditions;
                    _db.Diagnoses.Update(diagnosis);
                }
            }

            // Update Indication and Images
            if (request.Indication != null)
            {
                if (indication == null)
                {
                    indication = new Indication
                    {
                        RecordId = request.RecordID,
                        IndicationType = request.Indication.IndicationType,
                        Description = request.Indication.Description
                    };
                    await _db.Indications.AddAsync(indication);
                }
                else
                {
                    indication.IndicationType = request.Indication.IndicationType;
                    indication.Description = request.Indication.Description;
                    _db.Indications.Update(indication);
                }

                // Update Images
                if (request.IndicationImages != null && request.IndicationImages.Any())
                {
                    // Remove existing images
                    if (indication.Images != null && indication.Images.Any())
                    {
                        _db.PatientImages.RemoveRange(indication.Images);
                    }

                    // Add new images
                    var newImages = request.IndicationImages.Select(img => new PatientImage
                    {
                        IndicationId = indication.Id,
                        ImageUrl = img.ImageUrl,
                        ImageType = img.ImageType
                    }).ToList();

                    await _db.PatientImages.AddRangeAsync(newImages);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }
}
