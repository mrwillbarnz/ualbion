﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UAlbion.Formats
{
    public class ConfigObjectConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Config.ConfigObject).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var item = JObject.Load(reader);
            switch (item.Value<string>("Type"))
            {
                case "texture": return item.ToObject<Config.Texture>();
                case "interlaced_bitmap": return item.ToObject<Config.Texture>();
                case "palette": return item.ToObject<Config.Palette>();
                case "unknown": return item.ToObject<Config.ConfigObject>();
                default: throw new NotImplementedException();
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var o = JObject.FromObject(value);
            /*
            switch (value)
            {
                case Config.Texture _: o.AddFirst(new JProperty("Type", new JValue("texture"))); break;
                case Config.ConfigObject _: o.AddFirst(new JProperty("Type", new JValue("unknown"))); break;
                default: throw new NotImplementedException();
            }
            */
            o.WriteTo(writer);
        }
    }

    public class Config
    {
        public class ConfigObject
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        public class Texture : ConfigObject
        {
            public int Width { get; set; }
            public int Offset { get; set; }
        }

        public class Palette : ConfigObject { }

        public class Xld
        {
            [JsonIgnore]
            public string Name;
            public IDictionary<int, ConfigObject> Objects { get; } = new Dictionary<int, ConfigObject>();
        }

        [JsonIgnore]
        public string BasePath { get; set; }
        public string XldPath { get; set; }
        public string ExportedXldPath { get; set; }

        public IDictionary<string, Xld> Xlds { get; } = new Dictionary<string, Xld>();

        public static Config Load(string basePath)
        {
            var configPath = Path.Combine(basePath, "data", "config.json");
            Config config;
            if (File.Exists(configPath))
            {
                var configText = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<Config>(configText, new ConfigObjectConverter());
            }
            else
            {
                config = new Config
                {
                    XldPath = @"albion_sr\CD\XLDLIBS",
                    ExportedXldPath = @"exported"
                };
            }
            config.BasePath = basePath;
            return config;
        }

        public void Save()
        {
            var configPath = Path.Combine(BasePath, "data", "config.json");
            var serializerSettings = new JsonSerializerSettings();
            serializerSettings.Converters.Add(new ConfigObjectConverter());
            serializerSettings.Formatting = Formatting.Indented;
            var json = JsonConvert.SerializeObject(this, serializerSettings);
            File.WriteAllText(configPath, json);
        }
    }
}