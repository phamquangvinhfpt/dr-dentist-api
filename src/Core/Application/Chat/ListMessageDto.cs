namespace FSH.WebApi.Application.Chat;

public class ListMessageDto : IDto
{
    public Guid Id { get; set; }
    public string? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? Message { get; set; }
    public string? ImageUrl { get; set; }
    public string[]? ImagesUrl { get; set; }
    public DateTime CreatedOn { get; set; }
}