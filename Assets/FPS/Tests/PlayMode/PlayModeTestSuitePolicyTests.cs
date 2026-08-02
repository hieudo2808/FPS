using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FPS.PlayModeTests
{
    public class PlayModeTestSuitePolicyTests
    {
        [Test]
        public void LongSoakTests_AreIncludedInA1Gate()
        {
            string[] offenders = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => method.GetCustomAttributes<CategoryAttribute>(true)
                    .Any(attribute => attribute.Name == "LongSoak"))
                .Where(method => method.GetCustomAttributes<ExplicitAttribute>(true).Any())
                .Select(method => $"{method.DeclaringType.FullName}.{method.Name}")
                .OrderBy(name => name)
                .ToArray();

            // A1 deliberately runs the soak/performance gates in the default
            // PlayMode invocation.  Keep this policy test as a guard against
            // accidentally reintroducing [Explicit], which would make the
            // reported suite green while silently skipping the A1 gate.
            Assert.IsEmpty(offenders,
                "A1 LongSoak tests must remain discoverable in the default PlayMode gate.");
        }
    }
}
