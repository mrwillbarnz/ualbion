﻿using System.IO;

namespace UAlbion.Formats.MapEvents
{
    public class QueryVerbEvent : QueryEvent
    {
        public enum VerbType : byte
        {
            Examine = 1,
            Manipulate = 2,
            Speak = 3,
            UseItem = 4,
        }

        public static BranchNode Load(BinaryReader br, int id)
        {
            var e = new QueryVerbEvent
            {
                SubType = QueryType.ChosenVerb,
                Unk2 = br.ReadByte(), // 2
                Unk3 = br.ReadByte(), // 3
                Unk4 = br.ReadByte(), // 4
                Unk5 = br.ReadByte(), // 5
                Argument = br.ReadUInt16(), // 6
            };

            ushort? falseEventId = br.ReadUInt16(); // 8
            if (falseEventId == 0xffff)
                falseEventId = null;

            return new BranchNode(id, e, falseEventId);
        }
        public VerbType Verb => (VerbType) Argument;
        public override string ToString() => $"query_verb {SubType} {Verb} (method {Unk2})";
    }
}
