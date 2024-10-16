using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Profile;
using System.Security.Claims;

namespace FSH.WebApi.Application.Identity.Users;

public interface IUserService : ITransientService
{
    Task<PaginationResponse<UserDetailsDto>> SearchAsync(UserListFilter filter, CancellationToken cancellationToken);

    Task<bool> ExistsWithNameAsync(string name);
    Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null);
    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null);
    Task<bool> VerifyCurrentPassword(string userId, string password);
    Task<string> GetFullName(Guid userId);
    Task<List<UserDetailsDto>> GetListAsync(CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<UserDetailsDto> GetAsync(string userId, CancellationToken cancellationToken);

    Task<List<UserRoleDto>> GetRolesAsync(string userId, CancellationToken cancellationToken);
    Task<string> AssignRolesAsync(string userId, UserRolesRequest request, CancellationToken cancellationToken);

    Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);
    Task InvalidatePermissionCacheAsync(string userId, CancellationToken cancellationToken);

    Task ToggleStatusAsync(ToggleUserStatusRequest request, CancellationToken cancellationToken);

    Task<string> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal);
    Task<string> CreateAsync(CreateUserRequest request, string origin);
    Task UpdateAsync(UpdateUserRequest request);
    Task<string> UpdateEmailAsync(UpdateEmailRequest request);
    Task UpdatePhoneNumberAsync(UpdatePhoneNumberRequest request);
    Task UpdateAvatarAsync(UpdateAvatarRequest request, CancellationToken cancellationToken);
    Task<string> ConfirmEmailAsync(string userId, string code, string tenant, CancellationToken cancellationToken);
    Task<string> ConfirmPhoneNumberAsync(string userId, string code);
    Task<string> ResendPhoneNumberCodeConfirm(string userId);
    Task<string> ResendEmailCodeConfirm(string userId, string origin);
    Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, string origin);
    Task<string> ResetPasswordAsync(ResetPasswordRequest request);
    Task ChangePasswordAsync(ChangePasswordRequest request, string userId);
    Task<UserDetailsDto> GetUserDetailByEmailAsync(string email, CancellationToken cancellationToken);
    Task<UserDetailsDto> GetUserDetailByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
    Task GetUserByIdAsync(Guid userId, CancellationToken cancellationToken);
}