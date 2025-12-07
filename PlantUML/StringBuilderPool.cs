using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace PlantUML
{
    internal static class StringBuilderPool
    {
        private static readonly ObjectPool<StringBuilder> _pool;

        static StringBuilderPool()
        {
            var provider = new DefaultObjectPoolProvider();
            _pool = provider.Create(new StringBuilderPooledObjectPolicy());
        }

        public static StringBuilder Rent(int capacity = 16)
        {
            var sb = _pool.Get();
            if (sb.Capacity < capacity)
                sb.EnsureCapacity(capacity);
            return sb;
        }

        public static void Return(StringBuilder sb)
        {
            if (sb == null)
                return;

            // Reset the builder before returning
            sb.Clear();
            _pool.Return(sb);
        }
    }
}
