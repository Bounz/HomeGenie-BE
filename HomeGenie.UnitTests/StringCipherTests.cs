using HomeGenie.Service;
using NUnit.Framework;

namespace HomeGenieTests
{
    [TestFixture]
    public class StringCipherTests
    {
        private const string ValueWithCorrectPadding = "KSCxOe4DvexMneGuelZbxw==";
        private const string ValueWithWrongPadding = "ghEz91fogQMWcVgWkN5zHw==";

        [Test]
        public void DecryptValue_WithCorrectPadding_Succeeds()
        {
            var decryptedValue = StringCipher.Decrypt(ValueWithCorrectPadding, "homegenie");

            Assert.That(decryptedValue, Is.EqualTo("test@test.com"));
        }

        [Test]
        public void DecryptValue_WithWrongPadding_Succeeds()
        {
            var decryptedValue = StringCipher.Decrypt(ValueWithWrongPadding, "homegenie");

            Assert.That(decryptedValue, Is.EqualTo("test2@test.com"));
        }
    }
}
