using ClinicQ.Web.Security;

namespace ClinicQ.Tests.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_round_trips()
    {
        var hash = PasswordHasher.Hash("Passw0rd!");

        Assert.True(PasswordHasher.Verify("Passw0rd!", hash));
        Assert.False(PasswordHasher.Verify("passw0rd!", hash));
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hashes_are_salted_so_the_same_password_differs_every_time()
    {
        Assert.NotEqual(PasswordHasher.Hash("Passw0rd!"), PasswordHasher.Hash("Passw0rd!"));
    }

    [Fact]
    public void Hash_uses_the_documented_pbkdf2_format()
    {
        var parts = PasswordHasher.Hash("Passw0rd!").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.Equal("100000", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void Verifies_a_hash_seeded_by_the_sql_script()
    {
        // The literal stored for the 'admin' user in db/002_seed.sql.
        const string seeded = "PBKDF2-SHA256$100000$hz2QjIvmF5FW+HoDgnPojQ==$QnnKhvoppxfweDKqU+kcD0sqgxS4Ul05OsgiHBgX0JU=";

        Assert.True(PasswordHasher.Verify("Passw0rd!", seeded));
        Assert.False(PasswordHasher.Verify("Passw0rd", seeded));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA256$100000$only-three-parts")]
    [InlineData("BCRYPT$100000$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$abc$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$100000$!not-base64!$aGFzaA==")]
    public void Malformed_stored_hashes_fail_closed(string stored)
        => Assert.False(PasswordHasher.Verify("Passw0rd!", stored));

    [Fact]
    public void An_empty_password_never_verifies()
        => Assert.False(PasswordHasher.Verify("", PasswordHasher.Hash("Passw0rd!")));
}
