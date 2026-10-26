using System.ComponentModel.DataAnnotations;

namespace KampusImece.Models.ViewModels;

public class RegisterViewModel
{
    private string _fullName = string.Empty;
    private string _studentNumber = string.Empty;
    private string _email = string.Empty;
    private string? _phoneNumber;

    [Required(ErrorMessage = "Ad ve Soyad zorunludur.")]
    [MaxLength(100, ErrorMessage = "Ad Soyad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad Soyad")]
    public string FullName 
    { 
        get => _fullName; 
        set => _fullName = value?.Trim() ?? string.Empty; 
    }

    [Required(ErrorMessage = "Öğrenci numarası zorunludur.")]
    [MaxLength(20, ErrorMessage = "Öğrenci numarası en fazla 20 karakter olabilir.")]
    [Display(Name = "Öğrenci Numarası")]
    public string StudentNumber 
    { 
        get => _studentNumber; 
        set => _studentNumber = value?.Trim() ?? string.Empty; 
    }

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir üniversite e-posta adresi giriniz.")]
    [MaxLength(150)]
    [Display(Name = "Öğrenci E-posta (@ogr.selcuk.edu.tr)")]
    public string Email 
    { 
        get => _email; 
        set => _email = value?.Trim() ?? string.Empty; 
    }

    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    [MaxLength(20)]
    [Display(Name = "Telefon Numarası")]
    public string? PhoneNumber 
    { 
        get => _phoneNumber; 
        set => _phoneNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); 
    }

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifreniz en az 6 karakter uzunluğunda olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Girilen şifreler birbiriyle eşleşmiyor.")]
    [Display(Name = "Şifre Tekrar")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// 6698 Sayılı KVKK Kapsamında Kullanıcının Açık Rıza ve Aydınlatma Onayı
    /// </summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Kayıt olabilmek için KVKK Aydınlatma Metni ve Açık Rıza Beyanını onaylamanız zorunludur.")]
    [Display(Name = "KVKK Açık Rıza Onayı")]
    public bool AcceptKvkk { get; set; }
}
