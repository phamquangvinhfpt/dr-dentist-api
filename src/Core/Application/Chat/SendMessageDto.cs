using Microsoft.AspNetCore.Http;

namespace FSH.WebApi.Application.Chat;

public class SendMessageDto : IDto
{
    public string? Message { get; set; } = string.Empty;
    public string? ReceiverId { get; set; } = string.Empty;
    [AllowedExtensions(FileType.Image)]
    public IFormFile[]? Images { get; set; }
}