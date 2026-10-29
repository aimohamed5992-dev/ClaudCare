using ClaudCare.Api.Configuration;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ClaudCare.Api.Tests.Auth;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("0551234567", "0551234567")]
    [InlineData("551234567", "0551234567")]
    [InlineData("+966551234567", "0551234567")]
    [InlineData("00966551234567", "0551234567")]
    [InlineData("966 55 123 4567", "0551234567")]
    [InlineData("٠٥٥١٢٣٤٥٦٧", "0551234567")]
    [InlineData("055-123-4567", "0551234567")]
    public void Normalizes_saudi_mobile_numbers(string input, string expected) => Assert.Equal(expected, PhoneNormalizer.TryNormalize(input));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0412345678")]
    [InlineData("05512345")]
    [InlineData("abc")]
    [InlineData("+971551234567")]
    public void Rejects_everything_else(string? input) => Assert.Null(PhoneNormalizer.TryNormalize(input));

    [Theory]
    [InlineData("1098765432", true)]
    [InlineData("2098765432", true)]
    [InlineData("١٠٩٨٧٦٥٤٣٢", true)]
    [InlineData("3098765432", false)]
    [InlineData("109876543", false)]
    public void National_id_is_ten_digits_starting_with_1_or_2(string input, bool ok) => Assert.Equal(ok, PhoneNormalizer.TryNationalId(input) is not null);

    [Fact]
    public void Masks_hide_most_of_the_contact_detail()
    {
        Assert.Equal("05•• ••• 67", PhoneNormalizer.MaskPhone("0551234567"));
        Assert.Equal("m•••@hospital.com", PhoneNormalizer.MaskEmail("mona@hospital.com"));
    }
}

public class PasswordPolicyTests
{
    private readonly PasswordPolicy _p = new(Options.Create(new AuthOptions()));

    [Theory]
    [InlineData("abc12345")]
    [InlineData("كلمةسر123")]
    [InlineData("Reem@12345")]
    public void Accepts_letters_and_digits_of_8_or_more(string pw) => _p.Validate(pw);

    [Theory]
    [InlineData("", "8")]
    [InlineData("a1b2c3", "8")]
    [InlineData("abcdefgh", "حروف وأرقام")]
    [InlineData("12345678", "حروف وأرقام")]
    [InlineData("password123", "شائعة")]
    public void Rejects_weak_passwords_with_a_reason(string pw, string reasonContains) => Assert.Contains(reasonContains, _p.Validate(pw));

    [Fact]
    public void Valid_passwords_return_null()
    {
        Assert.Null(_p.Validate("Reem@12345"));
        Assert.Null(_p.Validate("كلمةسر123"));
    }
}

public class TokenServiceTests
{
    private static (TokenService svc, FakeTimeProvider clock) Make()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));
        return (new TokenService(Options.Create(new JwtOptions { SigningKey = new string('s', 40) }), clock), clock);
    }

    [Fact]
    public void Challenge_tokens_round_trip_and_expire()
    {
        var (svc, clock) = Make();
        var t = svc.CreateChallengeToken(7, OtpPurpose.Login2Fa, 99);
        Assert.Equal(new ChallengePayload(7, OtpPurpose.Login2Fa, 99), svc.ReadChallengeToken(t));
        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Null(svc.ReadChallengeToken(t));
    }

    [Fact]
    public void Each_token_kind_only_validates_for_its_own_purpose()
    {
        var (svc, _) = Make();
        var user = new User { Id = 5, FullName = "x", PasswordHash = "hash", Role = UserRole.Patient };
        var access = svc.CreateAccessToken(user).Token;
        var challenge = svc.CreateChallengeToken(5, OtpPurpose.Register, 1);
        var reset = svc.CreateResetToken(user).Token;

        Assert.Null(svc.ReadChallengeToken(access));
        Assert.Null(svc.ReadChallengeToken(reset));
        Assert.Null(svc.ReadResetToken(access));
        Assert.Null(svc.ReadResetToken(challenge));
        Assert.NotNull(svc.ReadResetToken(reset));
    }

    [Fact]
    public void Reset_token_is_bound_to_the_current_password_hash()
    {
        var (svc, _) = Make();
        var user = new User { Id = 5, FullName = "x", PasswordHash = "hash-1", Role = UserRole.Patient };
        var read = svc.ReadResetToken(svc.CreateResetToken(user).Token)!.Value;
        Assert.Equal(svc.PasswordStamp("hash-1"), read.Stamp);
        Assert.NotEqual(svc.PasswordStamp("hash-2"), read.Stamp);
    }

    [Fact]
    public void Refresh_tokens_are_random_and_hashed_deterministically()
    {
        var (svc, _) = Make();
        var a = svc.NewRefreshToken();
        Assert.NotEqual(a, svc.NewRefreshToken());
        Assert.True(a.Length >= 43);
        Assert.Equal(svc.Hash(a), svc.Hash(a));
        Assert.NotEqual(a, svc.Hash(a));
    }
}
