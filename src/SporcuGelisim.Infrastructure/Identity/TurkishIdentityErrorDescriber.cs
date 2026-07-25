using Microsoft.AspNetCore.Identity;

namespace SporcuGelisim.Infrastructure.Identity;

public sealed class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error("Beklenmeyen bir hata oluştu.");
    public override IdentityError ConcurrencyFailure() => Error("Kayıt başka bir işlem tarafından güncellendi. Lütfen tekrar deneyin.");
    public override IdentityError PasswordMismatch() => Error("Şifre hatalı.");
    public override IdentityError InvalidToken() => Error("Geçersiz doğrulama kodu.");
    public override IdentityError LoginAlreadyAssociated() => Error("Bu giriş yöntemi başka bir hesapla ilişkilendirilmiş.");
    public override IdentityError InvalidUserName(string? userName) => Error($"'{userName}' geçerli bir kullanıcı adı değildir.");
    public override IdentityError InvalidEmail(string? email) => Error($"'{email}' geçerli bir e-posta adresi değildir.");
    public override IdentityError DuplicateUserName(string userName) => Error($"'{userName}' kullanıcı adı zaten kullanılıyor.");
    public override IdentityError DuplicateEmail(string email) => Error($"'{email}' e-posta adresi zaten kullanılıyor.");
    public override IdentityError InvalidRoleName(string? role) => Error($"'{role}' geçerli bir rol adı değildir.");
    public override IdentityError DuplicateRoleName(string role) => Error($"'{role}' rolü zaten var.");
    public override IdentityError UserAlreadyHasPassword() => Error("Kullanıcının zaten şifresi var.");
    public override IdentityError UserLockoutNotEnabled() => Error("Bu kullanıcı için hesap kilitleme etkin değildir.");
    public override IdentityError UserAlreadyInRole(string role) => Error($"Kullanıcı zaten '{role}' rolünde.");
    public override IdentityError UserNotInRole(string role) => Error($"Kullanıcı '{role}' rolünde değil.");
    public override IdentityError PasswordTooShort(int length) => Error($"Şifre en az {length} karakter olmalıdır.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error("Şifre en az bir harf veya rakam dışı karakter içermelidir.");
    public override IdentityError PasswordRequiresDigit() => Error("Şifre en az bir rakam içermelidir.");
    public override IdentityError PasswordRequiresLower() => Error("Şifre en az bir küçük harf içermelidir.");
    public override IdentityError PasswordRequiresUpper() => Error("Şifre en az bir büyük harf içermelidir.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error($"Şifre en az {uniqueChars} farklı karakter içermelidir.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error("Kurtarma kodu kullanılamadı.");

    private static IdentityError Error(string description) => new() { Description = description };
}
