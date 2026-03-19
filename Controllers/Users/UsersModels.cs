namespace Api_BuildTech.Controllers.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public Guid IdEntreprise { get; set; }
        public string? NomEntreprise { get; set; }
        public Guid? IdRole { get; set; }
        public string? RoleName { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid? IdRole { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? Email { get; set; }
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public Guid? IdRole { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UserListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<UserDto> Users { get; set; } = new();
    }
}