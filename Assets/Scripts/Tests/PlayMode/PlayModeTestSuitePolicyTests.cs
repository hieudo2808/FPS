using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FPS.PlayModeTests
{
    public class PlayModeTestSuitePolicyTests
    {
        [Test]
        public void LongSoakTests_AreExplicitSoDefaultPlayModeRunStaysFast()
        {
            string[] offenders = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method.GetCustomAttributes<CategoryAttribute>(true)
                    .Any(attribute => attribute.Name == "LongSoak"))
                .Where(method => !method.GetCustomAttributes<ExplicitAttribute>(true).Any())
                .Select(method => $"{method.DeclaringType.FullName}.{method.Name}")
                .OrderBy(name => name)
                .ToArray();

            Assert.IsEmpty(offenders,
                "LongSoak tests must be Explicit so default PlayMode runs stay short.");
        }
    }
}
