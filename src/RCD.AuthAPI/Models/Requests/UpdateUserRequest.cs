namespace RCD.AuthAPI.Models.Requests;

public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}