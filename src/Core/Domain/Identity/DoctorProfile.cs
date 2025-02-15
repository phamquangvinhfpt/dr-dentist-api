﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Treatment;

namespace FSH.WebApi.Domain.Identity;
public class DoctorProfile : AuditableEntity, IAggregateRoot
{
    public string? DoctorId { get; set; }
    public Guid TypeServiceID { get; set; }
    public string? Education { get; set; }
    public string? College { get; set; }
    public string? Certification { get; set; }
    public string[]? CertificationImage { get; set; }
    public string? YearOfExp { get; set; }
    public string? SeftDescription { get; set; }
    public WorkingType WorkingType { get; set; }
    public bool IsActive { get; set; } = false;

    // Navigation property
    [JsonIgnore]
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    [JsonIgnore]
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    [JsonIgnore]
    public ICollection<TreatmentPlanProcedures>? TreatmentPlanProcedures { get; set; }
    [JsonIgnore]
    public ICollection<Prescription>? Prescriptions { get; set; }
}