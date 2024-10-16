using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users
{
    public class ListUserDTO
    {
        public string Id { get; set; }
        public string? ImageUrl { get; set; }
        public string? UserName { get; set; }
        public bool? Gender { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public UserRoleDto? Role { get; set; }
    }
}
