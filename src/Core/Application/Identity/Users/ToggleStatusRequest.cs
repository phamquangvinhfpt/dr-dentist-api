namespace FSH.WebApi.Application.Identity.Users;

public class ToggleStatusRequest
{
    public bool Activate { get; set; }
    public string? Id { get; set; }
}
