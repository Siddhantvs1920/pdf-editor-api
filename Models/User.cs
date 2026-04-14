using System.ComponentModel.DataAnnotations.Schema;

namespace PdfEditorApi.Models;

public sealed class User
{
    [Column("UserId")]
    public int Id { get; set; }

    [Column("Username")]
    public string UserName { get; set; } = string.Empty;

    [NotMapped]
    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
