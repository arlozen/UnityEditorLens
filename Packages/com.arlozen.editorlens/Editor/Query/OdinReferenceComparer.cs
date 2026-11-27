using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EditorLens
{
    internal sealed class OdinReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly OdinReferenceComparer Instance = new OdinReferenceComparer();
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}
