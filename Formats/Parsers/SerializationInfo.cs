﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace UAlbion.Formats.Parsers
{
    public class SerializationInfo
    {
        static readonly object serializerLock = new object();
        static readonly IDictionary<(Type, string), SerializationInfo> serializers = new Dictionary<(Type, string), SerializationInfo>();

        public string Name { get; }
        public int Size { get; }
        public Type Type { get; }
        protected SerializationInfo(string name, int size, Type type)
        {
            Name = name;
            Size = size;
            Type = type;
        }

        public static SerializationInfo<TTarget> Get<TTarget>(string propertyName)
        {
            lock (serializerLock)
            {
                SerializationInfo<TTarget> info;
                var key = (typeof(TTarget), propertyName);
                if (serializers.ContainsKey(key))
                {
                    info = (SerializationInfo<TTarget>)serializers[key];
                }
                else
                {
                    info = Create<TTarget>(propertyName);
                    serializers[key] = info;
                }
                return info;
            }
        }
        static SerializationInfo<TTarget> Create<TTarget>(string propertyName)
        {
            var property = typeof(TTarget).GetProperty(propertyName);
            if(property == null)
                throw new InvalidOperationException();

            var type = property.PropertyType;
            return 0 switch {
                _ when type == typeof(byte)   => new SerializationInfo<TTarget, byte>(property, sizeof(byte)),
                _ when type == typeof(sbyte)  => new SerializationInfo<TTarget, sbyte>(property, sizeof(sbyte)),
                _ when type == typeof(ushort) => new SerializationInfo<TTarget, ushort>(property, sizeof(ushort)),
                _ when type == typeof(short)  => new SerializationInfo<TTarget, short>(property, sizeof(short)),
                _ when type == typeof(uint)   => new SerializationInfo<TTarget, uint>(property, sizeof(uint)),
                _ when type == typeof(int)    => new SerializationInfo<TTarget, int>(property, sizeof(int)),
                _ when type == typeof(ulong)  => new SerializationInfo<TTarget, ulong>(property, sizeof(ulong)),
                _ when type == typeof(long)   => new SerializationInfo<TTarget, long>(property, sizeof(long)),
                _ => null
            };
        }
    }

    public class SerializationInfo<TTarget> : SerializationInfo
    {
        protected SerializationInfo(string name, int size, Type type) : base(name, size, type) { }
    }

    public class SerializationInfo<TTarget, TValue> : SerializationInfo<TTarget>
    {
        public SerializationInfo(PropertyInfo property, int size) : base(property.Name, size, property.PropertyType)
        {
            Getter = (Func<TTarget, TValue>)property.GetMethod.CreateDelegate(typeof(Func<TTarget, TValue>));
            Setter = (Action<TTarget, TValue>) property.SetMethod.CreateDelegate(typeof(Action<TTarget, TValue>));
        }

        public Func<TTarget, TValue> Getter { get; }
        public Action<TTarget, TValue> Setter { get; }
    }
}