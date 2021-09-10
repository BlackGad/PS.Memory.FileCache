using System;
using System.Linq;

namespace PS.Runtime.Caching.Extensions
{
    public static class TypeExtensions
    {
        #region Static members

        public static string GetAssemblyQualifiedName(this Type type)
        {
            return GetFullTypeStringRecursive(type).Trim('[', ']');
        }

        private static string GetFullTypeStringRecursive(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            string result;
            if (type.IsGenericType)
            {
                result = $"{type.GetGenericTypeDefinition().FullName}[{string.Join(",", type.GetGenericArguments().Select(GetFullTypeStringRecursive))}]";
            }
            else
            {
                result = type.FullName?.Replace("[]", "@");
            }

            return $"[{result}, {type.Assembly.GetName().Name}]";
        }

        #endregion
    }
}