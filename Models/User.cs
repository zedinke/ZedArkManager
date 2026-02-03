namespace ZedASAManager.Models;

public class User
{
    public string Username { get; set; } = string.Empty;
    public string? EncryptedPassword { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public bool AcceptedTerms { get; set; }
    public DateTime TermsAcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastLoginAt { get; set; }
}
