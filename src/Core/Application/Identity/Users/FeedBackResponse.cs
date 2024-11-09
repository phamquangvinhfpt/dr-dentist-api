namespace FSH.WebApi.Application.Identity.Users;

public class FeedBackResponse
{
    public Guid ServiceID { get; set; }
    public string? ServiceName { get; set; }
    public string? PatientID { get; set; }
    public string? PatientName { get; set; }
    public DateTime CreateDate { get; set; }
    public int Ratings { get; set; }
    public string? Message { get; set; }

}