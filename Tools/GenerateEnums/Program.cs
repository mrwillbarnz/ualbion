﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using UAlbion.Formats;

namespace GenerateEnums
{
    public class EnumEntry
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public override string ToString() => $"{Name} = {Value}";
    }

    public class EnumData
    {
        public string Name { get; set; }
        public IList<EnumEntry> Entries { get; } = new List<EnumEntry>();
    }

    class Program
    {
        static void Main()
        {
            var baseDir = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).Parent.Parent.Parent.Parent.FullName;
            Config config = Config.Load(baseDir);
            var outpathPath = Path.Combine(baseDir, @"UAlbion.Game\AssetIds");
            var xldPattern = new Regex(@"([0-9]+).XLD$");

            var enums = new Dictionary<string, EnumData>();
            foreach (var xld in config.Xlds)
            {
                if (string.IsNullOrEmpty(xld.Value.EnumName))
                    continue;

                int offset = 0;
                var match = xldPattern.Match(xld.Key);
                if (match.Success)
                    offset = 100 * int.Parse(match.Groups[1].Value);

                if (!enums.ContainsKey(xld.Value.EnumName))
                    enums[xld.Value.EnumName] = new EnumData { Name = xld.Value.EnumName };
                var e = enums[xld.Value.EnumName];

                foreach (var o in xld.Value.Objects)
                {
                    var id = offset + o.Key;
                    e.Entries.Add(string.IsNullOrEmpty(o.Value.Name)
                        ? new EnumEntry { Name = $"Unknown{id}", Value = id }
                        : new EnumEntry { Name = o.Value.Name.Replace(" ", ""), Value= id});
                }
            }

            foreach (var e in enums.Values)
            {
                var duplicateNames = e.Entries.GroupBy(x => x.Name).Where(x => x.Count() > 1).ToList();
                var counters = duplicateNames.ToDictionary(x => x.Key, x => 1);

                foreach (var o in e.Entries)
                {
                    if (!counters.ContainsKey(o.Name))
                        continue;
                    var name = o.Name;

                    o.Name = name +  counters[name];
                    counters[name]++;
                }
            }

            foreach (var e in enums.Values)
            {
                File.WriteAllText(Path.Combine(outpathPath, e.Name + ".cs"), $@"namespace UAlbion.Game.AssetIds
{{
    public enum {e.Name}
    {{
" +
                string.Join(Environment.NewLine, e.Entries.Select(x => $"        {x.Name} = {x.Value},"))
                + @"
    }
}");
            }
        }
    }
}