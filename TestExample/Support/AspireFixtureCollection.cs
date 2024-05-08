namespace TestExample.Support;

[CollectionDefinition(nameof(AspireFixtureCollection))]
public class AspireFixtureCollection : ICollectionFixture<AspireAppHostFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}