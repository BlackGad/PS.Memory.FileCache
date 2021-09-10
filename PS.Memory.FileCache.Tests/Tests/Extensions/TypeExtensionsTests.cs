using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using PS.Runtime.Caching.Extensions;

namespace PS.Runtime.Caching.Tests.Extensions
{
    [TestFixture]
    public class TypeExtensionsTests
    {
        #region Members

        [TestCase(typeof(int))]
        [TestCase(typeof(string))]
        [TestCase(typeof(Dictionary<string, string>))]
        [TestCase(typeof(TypeExtensionsTests))]
        [TestCase(typeof(List<Dictionary<IList<int>, string>>))]
        [TestCase(typeof(DateTime))]
        [TestCase(typeof(CallingConvention))]
        public void GetAssemblyQualifiedNameTest(Type expectedType)
        {
            var qualifiedName = expectedType.GetAssemblyQualifiedName();
            var actualType = Type.GetType(qualifiedName);
            Assert.AreEqual(expectedType, actualType);
        }

        #endregion
    }
}