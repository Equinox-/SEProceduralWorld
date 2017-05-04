using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcBuild
{
    public class MyCache<TK, TV>
    {
        private struct CacheItem
        {
            public TK key;
            public TV value;
        }

        private readonly Dictionary<TK, LinkedListNode<CacheItem>> cache;
        private readonly LinkedList<CacheItem> lruCache;
        private readonly int capacity;

        public MyCache(int capacity)
        {
            this.cache = new Dictionary<TK, LinkedListNode<CacheItem>>(capacity);
            this.lruCache = new LinkedList<CacheItem>();
            this.capacity = capacity;
        }

        public delegate TV CreateDelegate(TK key);

        public TV GetOrCreate(TK key, CreateDelegate del)
        {
            TV res;
            if (TryGet(key, out res)) return res;
            if (cache.Count >= capacity)
                while (cache.Count >= capacity / 1.5)
                {
                    cache.Remove(lruCache.First.Value.key);
                    lruCache.RemoveFirst();
                }

            var node = new LinkedListNode<CacheItem>(new CacheItem() {key = key, value = del(key)});
            lruCache.AddLast(node);
            cache.Add(key, node);
            return node.Value.value;
        }

        public bool TryGet(TK key, out TV value)
        {
            LinkedListNode<CacheItem> node;
            if (cache.TryGetValue(key, out node))
            {
                lruCache.Remove(node);
                lruCache.AddLast(node);
                value = node.Value.value;
                return true;
            }
            value = default(TV);
            return false;
        }
    }
}
