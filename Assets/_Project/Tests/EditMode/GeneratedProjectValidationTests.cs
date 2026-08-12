using AddressablesSample.Game.Editor;
using NUnit.Framework;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class GeneratedProjectValidationTests
    {
        [Test]
        public void GeneratedProject_PassesStructuralValidation()
        {
            Assert.DoesNotThrow(ProjectValidation.ValidateOrThrow);
        }
    }
}
