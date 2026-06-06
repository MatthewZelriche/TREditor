namespace TREditor2026.Tests;

public class EditorObjectIdTests
{
    [Fact]
    public void New_GeneratesUniqueValues()
    {
        EditorObjectId first = EditorObjectId.New();
        EditorObjectId second = EditorObjectId.New();

        Assert.NotEqual(first, second);
        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(Guid.Empty, second.Value);
    }

    [Fact]
    public void ToString_ReturnsGuidText()
    {
        Guid guid = Guid.Parse("12345678-1234-5678-1234-567812345678");
        EditorObjectId id = new(guid);

        Assert.Equal(guid.ToString(), id.ToString());
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Guid guid = Guid.NewGuid();
        EditorObjectId left = new(guid);
        EditorObjectId right = new(guid);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}
