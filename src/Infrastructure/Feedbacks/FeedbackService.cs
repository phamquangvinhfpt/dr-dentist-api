using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Infrastructure.CustomerInformations;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Feedbacks;
internal class FeedbackService : IFeedbackService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<FeedbackService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(ApplicationDbContext db, IStringLocalizer<FeedbackService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<FeedbackService> logger)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }
}
