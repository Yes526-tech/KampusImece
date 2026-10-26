using System.ComponentModel.DataAnnotations;

namespace KampusImece.Models.ViewModels;

public class FutureDateAttribute : ValidationAttribute
{
    private readonly int _minimumMinutesAhead;

    public FutureDateAttribute(int minimumMinutesAhead = 5)
    {
        _minimumMinutesAhead = minimumMinutesAhead;
        ErrorMessage = "Etkinlik tarihi ve saati gelecekte bir zaman olmalıdır.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateTime dateTime)
        {
            if (dateTime <= DateTime.Now.AddMinutes(_minimumMinutesAhead))
            {
                return new ValidationResult(ErrorMessage);
            }
        }
        else if (value != null)
        {
            return new ValidationResult("Geçerli bir tarih ve saat seçiniz.");
        }

        return ValidationResult.Success;
    }
}
