using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FPS.Tests
{
    public class RuntimeDebugSurfaceTests
    {
        [Test]
        public void EnemyAI_DoesNotExposeDebugPropertiesAsProductionApi()
        {
            string[] publicDebugProperties = typeof(EnemyAI)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.Name.StartsWith("Debug"))
                .Select(property => property.Name)
                .ToArray();

            Assert.IsEmpty(publicDebugProperties,
                "EnemyAI runtime API should expose gameplay behavior only; tests should use CaptureTestSnapshot instead of public Debug* properties.");
        }

        [Test]
        public void RubberBandingSystem_DoesNotExposeDebugPropertiesAsProductionApi()
        {
            string[] publicDebugProperties = typeof(RubberBandingSystem)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property => property.Name.StartsWith("Debug"))
                .Select(property => property.Name)
                .ToArray();

            Assert.IsEmpty(publicDebugProperties,
                "RubberBandingSystem runtime API should not expose debug metrics directly; tests should use CaptureTestSnapshot.");
        }
    }
}
