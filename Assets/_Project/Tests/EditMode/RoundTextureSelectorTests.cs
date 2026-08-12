using AddressablesSample.Game.Core;
using NUnit.Framework;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class RoundTextureSelectorTests
    {
        [Test]
        public void Next_FollowsOrderAndWraps()
        {
            var selector = new RoundTextureSelector(new object[] { "a", "b", "c" });

            Assert.That(selector.Next(), Is.EqualTo("a"));
            Assert.That(selector.Next(), Is.EqualTo("b"));
            Assert.That(selector.Next(), Is.EqualTo("c"));
            Assert.That(selector.Next(), Is.EqualTo("a"));
            Assert.That(selector.NextIndex, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Constructor_RejectsInsufficientKeys(int count)
        {
            var keys = count == 0 ? new object[0] : new object[] { "only" };
            Assert.Throws<System.ArgumentException>(() => new RoundTextureSelector(keys));
        }
    }
}
