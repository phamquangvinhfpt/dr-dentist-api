namespace FSH.WebApi.Application.Chat;

public class ListMessageDto : IDto
{
    public Guid Id { get; set; }
    public string? SenderId { get; set; }
    public string? LatestMessage { get; set; }
    public bool IsRead { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedOn { get; set; }
}