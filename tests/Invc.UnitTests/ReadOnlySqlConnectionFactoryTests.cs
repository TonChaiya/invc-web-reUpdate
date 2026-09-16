using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;
using Microsoft.Data.SqlClient;

namespace Invc.UnitTests;

public class ReadOnlySqlConnectionFactoryTests
{
    [Fact]
    public void Forces_read_only_intent_and_application_name()
    {
        var cs = ReadOnlySqlConnectionFactory.BuildReadOnlyConnectionString(
            "Server=.;Database=INV;Integrated Security=true;Encrypt=false");
        var b = new SqlConnectionStringBuilder(cs);

        Assert.Equal(ApplicationIntent.ReadOnly, b.ApplicationIntent);
        Assert.False(b.MultipleActiveResultSets);
        Assert.Equal("INVC Web (read-only)", b.ApplicationName);
        Assert.Equal("INV", b.InitialCatalog);
    }

    [Fact]
    public void Refuses_sa_login()
        => Assert.Throws<InvalidOperationException>(() =>
            ReadOnlySqlConnectionFactory.BuildReadOnlyConnectionString("Server=.;Database=INV;User ID=sa;Password=x"));

    [Fact]
    public void Requires_database_name()
        => Assert.Throws<InvalidOperationException>(() =>
            ReadOnlySqlConnectionFactory.BuildReadOnlyConnectionString("Server=.;Integrated Security=true"));

    [Theory]
    [InlineData("para", "para")]
    [InlineData("50%", "50[%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[x]", "[[]x]")]
    public void EscapeLike_neutralises_wildcards(string input, string expected)
        => Assert.Equal(expected, InventoryRepository.EscapeLike(input));
}
