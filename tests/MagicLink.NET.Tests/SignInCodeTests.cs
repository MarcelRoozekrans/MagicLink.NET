namespace MagicLink.Tests;

public sealed class SignInCodeTests
{
    [Fact]
    public void A_code_is_two_groups_of_four_characters_that_are_hard_to_mistake()
    {
        for (var i = 0; i < 1_000; i++)
        {
            var code = SignInRequest.NewCode();

            Assert.Matches("^[ABCDEFGHJKMNPQRTWXY3-9]{4}-[ABCDEFGHJKMNPQRTWXY3-9]{4}$", code);
        }
    }

    [Theory]
    [InlineData("ABCD-EFGH", "ABCDEFGH")]
    [InlineData(" abcd efgh ", "ABCDEFGH")]
    [InlineData("ab-cd-ef-gh", "ABCDEFGH")]
    public void A_typed_code_ignores_case_spaces_and_dashes(string typed, string normalized)
    {
        Assert.Equal(normalized, SignInRequest.NormalizeCode(typed));
    }
}
