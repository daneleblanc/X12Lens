using Xunit;

namespace X12Lens.Tests;

public sealed class CatalogTests
{
    [Theory]
    [InlineData("850", "Purchase Order")]
    [InlineData("810", "Invoice")]
    [InlineData("856", "Ship Notice / Manifest")]
    [InlineData("997", "Functional Acknowledgment")]
    [InlineData("999", "Implementation Acknowledgment")]
    public void DescribeTransactionSet_NamesTheCodesThisParserIsMostLikelyToMeet(string code, string expected)
    {
        Assert.Equal(expected, X12Catalog.DescribeTransactionSet(code));
    }

    [Theory]
    [InlineData("PO", "Purchase Order")]
    [InlineData("IN", "Invoice")]
    [InlineData("FA", "Functional Acknowledgment")]
    [InlineData("SH", "Ship Notice / Manifest")]
    public void DescribeFunctionalGroup_NamesTheGs01Codes(string code, string expected)
    {
        Assert.Equal(expected, X12Catalog.DescribeFunctionalGroup(code));
    }

    [Fact]
    public void Describe_ReturnsNull_ForACodeItDoesNotKnow()
    {
        // The catalog is deliberately partial. An unknown code is shown raw rather than
        // guessed at, because a parser inventing a document type is worse than a blank.
        Assert.Null(X12Catalog.DescribeTransactionSet("123"));
        Assert.Null(X12Catalog.DescribeFunctionalGroup("QQ"));
        Assert.Null(X12Catalog.DescribeTransactionSet(null));
    }
}
