using NUnit.Framework;
using System;

namespace FPS.Tests
{
    public class ObjectPoolingNamespaceTests
    {
        [Test]
        public void ObjectPooling_IsInFPSNamespace()
        {
            Type t1 = Type.GetType("ObjectPooling, Assembly-CSharp") ?? Type.GetType("ObjectPooling, FPS");
            Type t2 = Type.GetType("FPS.ObjectPooling, Assembly-CSharp") ?? Type.GetType("FPS.ObjectPooling, FPS");

            Assert.IsNull(t1, "ObjectPooling should not be in the global namespace.");
            Assert.IsNotNull(t2, "ObjectPooling must be in the FPS namespace.");
        }
    }
}
