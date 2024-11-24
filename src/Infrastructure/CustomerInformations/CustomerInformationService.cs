using Ardalis.Specification.EntityFrameworkCore;
using ClosedXML.Excel;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.CustomerServices;
using FSH.WebApi.Domain.CustomerServices;
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

    public CustomerInformationService(ApplicationDbContext db, IStringLocalizer<CustomerInformationService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<CustomerInformationService> logger)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
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
                    StaffId = contact.StaffId,
                    ContactId = contact.Id,
                    Content = contact.Content,
                    CreateDate = contact.CreatedOn,
                    Email = contact.Email,
                    Phone = contact.Phone,
                    Title = contact.Title,
                    StaffName = null
                };

                if (!string.IsNullOrEmpty(contact.StaffId))
                {
                    var user = await _userManager.FindByIdAsync(contact.StaffId);
                    if (user != null)
                    {
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
                    StaffName = $"{user.FirstName} {user.LastName}"
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
}
