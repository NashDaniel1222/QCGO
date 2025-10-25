using System.ComponentModel.DataAnnotations;

namespace QCGO.Models
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

    [Display(Name = "Birthday")]
    [DataType(DataType.Date)]
    public DateTime? Birthday { get; set; }

    [Display(Name = "Gender")]
    public string Gender { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
