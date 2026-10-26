using System.ComponentModel.DataAnnotations;

namespace KampusImece.Models.ViewModels;

public class LoginViewModel
{
    private string _email = string.Empty;

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Lütfen geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "Öğrenci E-posta")]
    public string Email 
    { 
        get => _email; 
        set => _email = value?.Trim() ?? string.Empty; 
    }

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni Hatırla")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
