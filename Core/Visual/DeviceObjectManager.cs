﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UAlbion.Core.Events;

namespace UAlbion.Core.Visual
{
    public class DeviceObjectManager : Component, IDeviceObjectManager
    {
        readonly IDictionary<(object, object), CacheEntry> _cache = new Dictionary<(object, object), CacheEntry>();
        readonly object _syncRoot = new object();
        readonly HashSet<(object, object)> _staleOwners = new HashSet<(object, object)>();
        long _frame;

        static readonly HandlerSet Handlers = new HandlerSet(
            H<DeviceObjectManager, BeginFrameEvent>((x,_) => x.BeginFrame())
        );

        class CacheEntry : IDisposable
        {
            public CacheEntry(IDisposable resource)
            {
                Resource = resource;
            }

            public IDisposable Resource { get; }
            public long LastAccessed { get; set; }
            public void Dispose() => Resource?.Dispose();
        }

        public DeviceObjectManager() : base(Handlers) { }

        void BeginFrame()
        {
            lock (_syncRoot)
            {
                foreach (var kvp in _cache)
                    if (kvp.Value.LastAccessed < _frame)
                        _staleOwners.Add(kvp.Key);

                foreach (var owner in _staleOwners)
                    _cache.Remove(owner);

                _staleOwners.Clear();
                _frame++;
            }
        }

        public T Get<T>((object, object) owner)
        {
            lock(_syncRoot)
            {
                if (_cache.TryGetValue(owner, out var entry))
                    return (T)entry.Resource;

                throw new KeyNotFoundException();
            }
        }

        public T Prepare<T>((object, object) owner, Func<T> createFunc, Func<T, bool> dirtyFunc) where T : IDisposable
        {
            lock (_syncRoot)
            {
                if (_cache.TryGetValue(owner, out var entry)
                    && !dirtyFunc((T)entry.Resource))
                {
                    entry.LastAccessed = _frame;
                    return (T)entry.Resource;
                }

                entry?.Dispose();
                var newResource = createFunc();
                _cache[owner] = new CacheEntry(newResource) { LastAccessed = _frame };
                return newResource;
            }
        }

        public void DestroyDeviceObjects()
        {
            lock(_syncRoot)
            {
                foreach (var resource in _cache.Values)
                    resource.Dispose();
                _cache.Clear();
            }
        }

        DateTime _lastUpdate;
        string _cachedStats;
        public string Stats()
        {
            if ((DateTime.Now - _lastUpdate).TotalMilliseconds <= 500)
                return _cachedStats;

            var sb = new StringBuilder();
            sb.AppendLine("DeviceObject Statistics:");
            lock (_syncRoot)
            {
                long totalCount = 0;
                foreach (var entry in _cache.GroupBy(x => x.Value.Resource.GetType()).OrderBy(x => x.Key.Name))
                {
                    long count = entry.Count();
                    totalCount += count;
                    sb.AppendLine($"    {entry.Key.Name}: {count}");
                }
                sb.AppendLine($"    Total: {_cache.Count:N0} entries");
            }

            _cachedStats = sb.ToString();
            _lastUpdate = DateTime.Now;
            return _cachedStats;
        }
    }
}