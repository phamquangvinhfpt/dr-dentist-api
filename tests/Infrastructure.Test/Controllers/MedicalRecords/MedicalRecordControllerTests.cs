using Amazon.Runtime.Internal.Util;
using FluentAssertions;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.MedicalRecords;
using FSH.WebApi.Host.Controllers.MedicalRecords;
using FSH.WebApi.Infrastructure.MedicalRecords;
using FSH.WebApi.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Infrastructure.Test.Controllers.MedicalRecords;
public class MedicalRecordControllerTests
{
    private readonly Mock<IMedicalRecordService> _medicalRecordServiceMock;
    private readonly MedicalRecordController _controller;

    public MedicalRecordControllerTests()
    {
        _medicalRecordServiceMock = new Mock<IMedicalRecordService>();
        _controller = new MedicalRecordController(_medicalRecordServiceMock.Object);
    }

    [Fact]
    public async Task CreateMedicalRecord_ShouldCallService_WithCorrectParameters()
    {
        // Arrange
        var expectedResponse = "Medical record created successfully.";
        var request = new CreateMedicalRecordRequest
        {
            AppointmentId = Guid.NewGuid(),
            BasicExamination = new BasicExaminationRequest
            {
                ExaminationContent = "Examination content",
                TreatmentPlanNote = "Treatment plan note"
            },
            Diagnosis = new DiagnosisRequest
            {
                ToothNumber = 15,
                TeethConditions = new[] { "Good" }
            },
            Indication = new IndicationRequest
            {
                Description = "description",
                IndicationType = new[] { "Type1", "Type2" }
            },
            IndicationImages = new List<IndicationImageRequest>
            { new IndicationImageRequest
                {
                     ImageType = "ImageType",
                     ImageUrl = "Image.jpg"
                },
                new IndicationImageRequest
                {
                    ImageType = "ImageType2",
                    ImageUrl = "Image2.jpg"
                }
            }
        };

        // Mock ISender
        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateMedicalRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Create controller
        // var controller = new MedicalRecordController(null!);

        // Set up fake HttpContext
        var serviceProvider = new ServiceCollection()
            .AddSingleton(mediatorMock.Object)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act
        var result = await _controller.CreateMedicalRecord(request);

        // Assert
        result.Should().Be(expectedResponse);

        mediatorMock.Verify(
            m => m.Send(It.Is<CreateMedicalRecordRequest>(r => r.AppointmentId == request.AppointmentId), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateMedicalRecord_ShouldCallService_WithNotCorrectParameters()
    {
        // Arrange
        var request = new CreateMedicalRecordRequest
        {
            AppointmentId = Guid.Empty,
            BasicExamination = null,
            Diagnosis = null,
            Indication = null,
            IndicationImages = null
        };

        var mediatorMock = new Mock<ISender>();
        mediatorMock.Setup(x => x.Send(It.IsAny<CreateMedicalRecordRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ValidationException("Invalid data"));

        // var controller = new MedicalRecordController(null!);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(mediatorMock.Object)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => _controller.CreateMedicalRecord(request));

        exception.Message.Should().Be("Invalid data");

        mediatorMock.Verify(
            x => x.Send(It.Is<CreateMedicalRecordRequest>(x => x.AppointmentId == request.AppointmentId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMedicalRecordByID_ShouldReturnMedicalRecordResponse_WhenIDIsValid()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var expectedResponse = new MedicalRecordResponse
        {
            RecordId = recordId,
            PatientId = Guid.NewGuid(),
            PatientCode = "P12345",
            PatientName = "John Doe",
            DentistId = Guid.NewGuid(),
            DentistName = "Dr. Smith",
            AppointmentId = Guid.NewGuid(),
            AppointmentNotes = "Routine Checkup",
            Date = DateTime.UtcNow,
            BasicExamination = new BasicExaminationRequest
            {
                TreatmentPlanNote = "Checkup Plan",
                ExaminationContent = "Teeth Cleaning"
            },
            Diagnosis = new DiagnosisRequest
            {
                TeethConditions = new[] { "Healthy" },
                ToothNumber = 32
            },
            Indication = new IndicationRequest
            {
                IndicationType = new[] { "Type1" },
                Description = "Routine teeth cleaning"
            }
        };

        _medicalRecordServiceMock
            .Setup(x => x.GetMedicalRecordByID(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetMedicalRecordByID(recordId, CancellationToken.None);

        // Assert
        Assert.NotNull(result); // Ensure result is not null
        Assert.Equal(expectedResponse.RecordId, result.RecordId);
        Assert.Equal(expectedResponse.PatientId, result.PatientId);
        Assert.Equal(expectedResponse.PatientCode, result.PatientCode);
        Assert.Equal(expectedResponse.PatientName, result.PatientName);
        Assert.Equal(expectedResponse.DentistId, result.DentistId);
        Assert.Equal(expectedResponse.DentistName, result.DentistName);
        Assert.Equal(expectedResponse.AppointmentId, result.AppointmentId);
        Assert.Equal(expectedResponse.AppointmentNotes, result.AppointmentNotes);

        _medicalRecordServiceMock.Verify(x => x.GetMedicalRecordByID(recordId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetMedicalRecordByID_ShouldReturnNotFound_WhenIDIsInvalid()
    {
        // Arrange
        var invalidRecordId = Guid.NewGuid();

        // If ID = null throw Exception
        _medicalRecordServiceMock
            .Setup(x => x.GetMedicalRecordByID(invalidRecordId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Not found Medical Record"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
        {
            await _controller.GetMedicalRecordByID(invalidRecordId, CancellationToken.None);
        });

        Assert.Equal("Not found Medical Record", exception.Message);

        _medicalRecordServiceMock.Verify(x => x.GetMedicalRecordByID(invalidRecordId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetMedicalRecordByAppointmentID_ShouldReturnMedicalRecordResponse_WhenIDIsValid()
    {
        // Arrange
        var appointmentId = Guid.NewGuid();
        var expectedResponse = new MedicalRecordResponse
        {
            RecordId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            PatientCode = "P12345",
            PatientName = "John Doe",
            DentistId = Guid.NewGuid(),
            DentistName = "Dr. Smith",
            AppointmentId = appointmentId,
            Date = DateTime.UtcNow,
            BasicExamination = new BasicExaminationRequest
            {
                TreatmentPlanNote = "Checkup Plan",
                ExaminationContent = "Teeth Cleaning"
            },
            Diagnosis = new DiagnosisRequest
            {
                TeethConditions = new[] { "Healthy" },
                ToothNumber = 32
            },
            Indication = new IndicationRequest
            {
                IndicationType = new[] { "Type1" },
                Description = "Routine teeth cleaning"
            }
        };

        _medicalRecordServiceMock
            .Setup(x => x.GetMedicalRecordByAppointmentID(appointmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetMedicalRecordByAppointmentID(appointmentId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse.RecordId, result.RecordId);
        Assert.Equal(expectedResponse.PatientId, result.PatientId);
        Assert.Equal(expectedResponse.PatientName, result.PatientName);
        Assert.Equal(expectedResponse.DentistId, result.DentistId);
        Assert.Equal(expectedResponse.DentistName, result.DentistName);

        _medicalRecordServiceMock.Verify(x => x.GetMedicalRecordByAppointmentID(appointmentId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetMedicalRecordByAppointmentId_ShouldReturnNotFound_WhenIdIsInvalid()
    {
        // Arrange
        var invalidAppointmentId = Guid.NewGuid();

        _medicalRecordServiceMock
            .Setup(x => x.GetMedicalRecordByAppointmentID(invalidAppointmentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Not found Medical Record"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await _controller.GetMedicalRecordByAppointmentID(invalidAppointmentId, CancellationToken.None);
        });

        Assert.Equal("Not found Medical Record", exception.Message);
        _medicalRecordServiceMock.Verify(x => x.GetMedicalRecordByAppointmentID(invalidAppointmentId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetMedicalRecordsByPatientID_ShouldReturnMedicalRecordResponses_WhenIdIsValid()
    {
        // Arrange
        var patientId = "patientId";
        var expectedResponses = new List<MedicalRecordResponse>
        {
            new MedicalRecordResponse
            {
                RecordId = Guid.NewGuid(),
                PatientId = Guid.NewGuid(),
                PatientCode = "P12345",
                PatientName = "John Doe",
                DentistId = Guid.NewGuid(),
                DentistName = "Dr. Smith",
                AppointmentId = Guid.NewGuid(),
                AppointmentNotes = "AppointmentNote1",
                Date = DateTime.Now,
                BasicExamination = new BasicExaminationRequest
                {
                    TreatmentPlanNote = "Checkup Plan",
                    ExaminationContent = "Teeth Cleaning"
                },
                Diagnosis = new DiagnosisRequest
                {
                    TeethConditions = new[] { "Healthy" },
                    ToothNumber = 32
                },
                Indication = new IndicationRequest
                {
                    IndicationType = new[] { "Type1" },
                    Description = "Routine teeth cleaning"
                }
            },
            new MedicalRecordResponse
            {
                RecordId = Guid.NewGuid(),
                PatientId = Guid.NewGuid(),
                PatientCode = "A12345",
                PatientName = "Jane Doe",
                DentistId = Guid.NewGuid(),
                DentistName = "Dr. Thanh",
                AppointmentId = Guid.NewGuid(),
                AppointmentNotes = "AppointmentNote2",
                Date = DateTime.Now,
                BasicExamination = new BasicExaminationRequest
                {
                    TreatmentPlanNote = "Treatment Plane Note Test",
                    ExaminationContent = "Examination Content Test"
                },
                Diagnosis = new DiagnosisRequest
                {
                     TeethConditions = new[] { "Healthy" },
                     ToothNumber = 15
                },
                Indication = new IndicationRequest
                {
                     IndicationType = new[] { "Type2" },
                     Description = "Test Description"
                }
            }
        };

        _medicalRecordServiceMock
            .Setup(x => x.GetMedicalRecordsByPatientId(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponses);

        // Act
        var result = await _controller.GetMedicalRecordsByPatientID(patientId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(expectedResponses[0].PatientId, result[0].PatientId);
        Assert.Equal(expectedResponses[1].PatientName, result[1].PatientName);

        _medicalRecordServiceMock.Verify(x => x.GetMedicalRecordsByPatientId(patientId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task GetMedicalRecordsByPatientID_ShouldReturnNotFound_WhenIdIsInvalid()
    {
        // Arrange
        var invalidPatientId = "patientId";

        _medicalRecordServiceMock
            .Setup(x => x.GetMedicalRecordsByPatientId(invalidPatientId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Patient with ID invalid_patient_id not found"));

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await _controller.GetMedicalRecordsByPatientID(invalidPatientId, CancellationToken.None);
        });

        // Assert
        Assert.Equal("Patient with ID invalid_patient_id not found", exception.Message);
        _medicalRecordServiceMock.Verify(x => x.GetMedicalRecordsByPatientId(invalidPatientId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteMedicalRecordByID_ShouldDeletedSuccess_WhenIDIsValid()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var expectedMessage = "Delete successfully";

        _medicalRecordServiceMock
            .Setup(x => x.DeleteMedicalRecordID(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessage);

        // Act
        var result = await _controller.DeleteMedicalRecordByID(recordId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result);
        _medicalRecordServiceMock.Verify(x => x.DeleteMedicalRecordID(recordId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteMedicalRecordByID_ShouldReturnNotFound_WhenIdIsInvalid()
    {
        // Arrange
        var invalidRecordId = Guid.NewGuid();

        _medicalRecordServiceMock
            .Setup(x => x.DeleteMedicalRecordID(invalidRecordId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Not found Medical record"));

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await _controller.DeleteMedicalRecordByID(invalidRecordId, CancellationToken.None);
        });

        // Assert
        Assert.Equal("Not found Medical record", exception.Message);
        _medicalRecordServiceMock.Verify(x => x.DeleteMedicalRecordID(invalidRecordId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteMedicalRecordsByPatientID_ShouldDeletedSuccess_WhenIdIsValid()
    {
        // Arrange
        var patientId = "patientID";
        var expectedMessage = "Delete medical records success";

        _medicalRecordServiceMock
            .Setup(x => x.DeleteMedicalRecordByPatientID(patientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessage);

        // Act
        var result = await _controller.DeleteMedicalRecordsByPatientID(patientId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result);
        _medicalRecordServiceMock.Verify(x => x.DeleteMedicalRecordByPatientID(patientId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task DeleteMedicalRecordsByPatientID_ShouldReturnNotFound_WhenIdIsInvalid()
    {
        // Arrange
        var invalidPatientId = "patientId";

        _medicalRecordServiceMock
            .Setup(x => x.DeleteMedicalRecordByPatientID(invalidPatientId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Patient with ID {invalidPatientId} not found"));

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await _controller.DeleteMedicalRecordsByPatientID(invalidPatientId, CancellationToken.None);
        });

        // Assert
        Assert.Equal($"Patient with ID {invalidPatientId} not found", exception.Message);
        _medicalRecordServiceMock.Verify(x => x.DeleteMedicalRecordByPatientID(invalidPatientId, It.IsAny<CancellationToken>()), Times.Once());
    }
}
