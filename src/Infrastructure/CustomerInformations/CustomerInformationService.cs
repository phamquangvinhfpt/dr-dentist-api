using Ardalis.Specification.EntityFrameworkCore;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.FileStorage;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Mailing;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.CustomerServices;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static QRCoder.PayloadGenerator.SwissQrCode;

namespace FSH.WebApi.Infrastructure.CustomerInformations;
internal class CustomerInformationService : ICustomerInformationService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<CustomerInformationService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomerInformationService> _logger;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;

    public CustomerInformationService(ApplicationDbContext db, IStringLocalizer<CustomerInformationService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<CustomerInformationService> logger, IFileStorageService fileStorageService, IEmailTemplateService templateService, IMailService mailService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
        _fileStorageService = fileStorageService;
        _templateService = templateService;
        _mailService = mailService;
    }

    public async Task AddCustomerInformation(ContactInformationRequest request)
    {
        try
        {
            await _db.ContactInfor.AddAsync(new Domain.CustomerServices.ContactInfor
            {
                Title = request.Title,
                Content = request.Content,
                Phone = request.Phone,
                Email = request.Email,
                Status = ContactStatus.Pending,

            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task AddStaffForContact(AddStaffForContactRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(_currentUserService.GetUserId().ToString());
            var role = await _userManager.GetRolesAsync(user);
            if (role[0] == FSHRoles.Staff)
            {
                if (!request.StaffId.Equals(_currentUserService.GetUserId()))
                {
                    throw new InvalidOperationException("Staff can not handle for other staff");
                }
            }
            var contact = await _db.ContactInfor.FirstOrDefaultAsync(p => p.Id == request.ContactID);
            if (contact.StaffId != null)
            {
                if (role[0] != FSHRoles.Admin)
                {
                    throw new InvalidOperationException($"the Staff {user.UserName} contacted with guest {contact.Email}");
                }
            }
            contact.StaffId = request.StaffId;
            contact.Status = ContactStatus.Waiting;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task<bool> CheckContactExist(Guid id)
    {
        return await _db.ContactInfor.AnyAsync(x => x.Id == id);
    }

    public async Task<PaginationResponse<ContactResponse>> GetAllContactRequest(PaginationFilter request, CancellationToken cancellationToken)
    {
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<ContactInfor>(request);
            var contacts = await _db.ContactInfor
                .AsNoTracking()
                .Where(p => p.StaffId != null)
                .WithSpecification(spec)
                .OrderByDescending(p => p.CreatedOn)
                .ToListAsync(cancellationToken);

            var list = new List<ContactResponse>();
            foreach (var contact in contacts)
            {
                var contactResponse = new ContactResponse
                {
                    ContactId = contact.Id,
                    Content = contact.Content,
                    CreateDate = contact.CreatedOn,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Title = contact.Title,
                    EmailContext = contact.EmailContext,
                    ImageUrl = contact.ImageUrl,
                    Status = contact.Status,
                };

                if (!string.IsNullOrEmpty(contact.StaffId))
                {
                    var user = await _userManager.FindByIdAsync(contact.StaffId);
                    if (user != null)
                    {
                        contactResponse.StaffId = user.Id;
                        contactResponse.StaffName = $"{user.FirstName} {user.LastName}";
                    }
                }

                list.Add(contactResponse);
            }

            int count = await _db.ContactInfor.Where(p => p.StaffId != null)
                .CountAsync(cancellationToken);
            return new PaginationResponse<ContactResponse>(list, count, request.PageNumber, request.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<ContactResponse>> GetAllContactRequestForStaff(PaginationFilter request, CancellationToken cancellationToken)
    {
        try
        {
            var staffID = _currentUserService.GetUserId().ToString();
            var user = await _userManager.FindByIdAsync(staffID) ?? throw new Exception("Warning: Error when find staff.");
            var spec = new EntitiesByPaginationFilterSpec<ContactInfor>(request);
            var contacts = await _db.ContactInfor
                .AsNoTracking()
                .Where(p => p.StaffId == staffID)
                .WithSpecification(spec)
                .OrderByDescending(p => p.CreatedOn)
                .ToListAsync(cancellationToken);

            var list = new List<ContactResponse>();
            foreach (var contact in contacts)
            {
                var contactResponse = new ContactResponse
                {
                    StaffId = contact.StaffId,
                    ContactId = contact.Id,
                    Content = contact.Content,
                    CreateDate = contact.CreatedOn,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Title = contact.Title,
                    StaffName = $"{user.FirstName} {user.LastName}",
                    EmailContext = contact.EmailContext,
                    ImageUrl = contact.ImageUrl,
                    Status = contact.Status,
                };

                list.Add(contactResponse);
            }

            int count = await _db.ContactInfor.Where(p => p.StaffId == staffID)
                .CountAsync(cancellationToken);
            return new PaginationResponse<ContactResponse>(list, count, request.PageNumber, request.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<ContactResponse>> GetAllContactRequestNonStaff(PaginationFilter request, CancellationToken cancellationToken)
    {
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<ContactInfor>(request);
            var contacts = await _db.ContactInfor
                .AsNoTracking()
                .Where(p => p.StaffId == null)
                .WithSpecification(spec)
                .OrderByDescending(p => p.CreatedOn)
                .ToListAsync(cancellationToken);

            var list = new List<ContactResponse>();
            foreach (var contact in contacts)
            {
                var contactResponse = new ContactResponse
                {
                    ContactId = contact.Id,
                    Content = contact.Content,
                    CreateDate = contact.CreatedOn,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Title = contact.Title,
                };

                list.Add(contactResponse);
            }

            int count = await _db.ContactInfor.Where(p => p.StaffId == null)
                .CountAsync(cancellationToken);
            return new PaginationResponse<ContactResponse>(list, count, request.PageNumber, request.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> StaffEmailContextContactAsync(UpdateEmailContextRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var contact = await _db.ContactInfor.FirstOrDefaultAsync(p => p.Id == request.ContactID);

            if (contact == null)
            {
                throw new Exception("Warning: Error when found contact");
            }
            if (request.EmailContext == null)
            {
                throw new Exception("Warning: Can not found email context");
            }

            ContactFormEmail eMailModel = new ContactFormEmail()
            {
                ClinicAddress = "Tp HCM",
                ClinicPhone = "0987654321",
                ConsultContent = request.EmailContext,
                Phone = contact.Phone,
            };
            var mailRequest = new MailRequest(
                        new List<string> { contact.Email },
                        "🦷 TƯ VẤN NHA KHOA 🦷",
                        _templateService.GenerateEmailTemplate("contact-infor", eMailModel));
            await _mailService.SendAsync(mailRequest, CancellationToken.None);

            contact.StaffId = _currentUserService.GetUserId().ToString();

            contact.LastModifiedBy = _currentUserService.GetUserId();

            contact.EmailContext = request.EmailContext;

            contact.Status = ContactStatus.Done;

            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<string> StaffUpdatePhoneCallImageContactAsync(UpdatePhoneCallImageRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var contact = await _db.ContactInfor.FirstOrDefaultAsync(p => p.Id == request.ContactID);

            if (contact == null) {
                throw new Exception("Warning: Error when found contact");
            }
            if (request.Images == null) {
                throw new Exception("Warning: Can not found image");
            }
            contact.ImageUrl = await _fileStorageService.SaveFilesAsync(request.Images, cancellationToken);
            contact.StaffId = _currentUserService.GetUserId().ToString();
            contact.LastModifiedBy = _currentUserService.GetUserId();
            contact.Status = ContactStatus.Done;
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }
}
