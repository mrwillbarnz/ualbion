﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UAlbion.Formats
{
    public enum GameLanguage
    {
        German,
        English,
        French,
    }

    public class Assets
    {
        static Assets Instance { get; } = new Assets();

        readonly Config _config;
        readonly IDictionary<AssetType, XldFile[]> _xlds = new Dictionary<AssetType, XldFile[]>();
        readonly IDictionary<AssetType, IDictionary<int, object>> _assetCache = new Dictionary<AssetType, IDictionary<int, object>>();

        // ReSharper disable StringLiteralTypo
        readonly IDictionary<AssetType, (AssetLocation, string)> _assetFiles = new Dictionary<AssetType, (AssetLocation, string)> {
            { AssetType.MapData,            (AssetLocation.Base,      "MAPDATA!.XLD") }, // Map2d
            { AssetType.IconData,           (AssetLocation.Base,      "ICONDAT!.XLD") }, // Texture
            { AssetType.IconGraphics,       (AssetLocation.Base,      "ICONGFX!.XLD") }, // Texture
            { AssetType.Palette,            (AssetLocation.Base,      "PALETTE!.XLD") }, // Palette
            { AssetType.PaletteNull,        (AssetLocation.BaseRaw,   "PALETTE.000" ) }, // Palette (supplementary)
            { AssetType.Slab,               (AssetLocation.Base,      "SLAB"        ) }, //
            { AssetType.BigPartyGraphics,   (AssetLocation.Base,      "PARTGR!.XLD" ) }, // Texture
            { AssetType.SmallPartyGraphics, (AssetLocation.Base,      "PARTKL!.XLD" ) }, // Texture
            { AssetType.LabData,            (AssetLocation.Base,      "LABDATA!.XLD") }, //
            { AssetType.Wall3D,             (AssetLocation.Base,      "3DWALLS!.XLD") }, // Texture
            { AssetType.Object3D,           (AssetLocation.Base,      "3DOBJEC!.XLD") }, // Texture
            { AssetType.Overlay3D,          (AssetLocation.Base,      "3DOVERL!.XLD") }, // Texture
            { AssetType.Floor3D,            (AssetLocation.Base,      "3DFLOOR!.XLD") }, // Texture
            { AssetType.BigNpcGraphics,     (AssetLocation.Base,      "NPCGR!.XLD"  ) }, // Texture
            { AssetType.BackgroundGraphics, (AssetLocation.Base,      "3DBCKGR!.XLD") }, // Texture
            { AssetType.Font,               (AssetLocation.Base,      "FONTS!.XLD"  ) }, // Font (raw, 8 wide. 00 = normal, 01 = bold)
            { AssetType.BlockList,          (AssetLocation.Base,      "BLKLIST!.XLD") }, // 
            { AssetType.PartyCharacterData, (AssetLocation.Initial,   "PRTCHAR!.XLD") }, // 
            { AssetType.SmallPortrait,      (AssetLocation.Base,      "SMLPORT!.XLD") }, // Texture
            { AssetType.SystemTexts,        (AssetLocation.Localised, "SYSTEXTS"    ) }, // Strings
            { AssetType.EventSet,           (AssetLocation.Base,      "EVNTSET!.XLD") }, // 
            { AssetType.EventTexts,         (AssetLocation.Localised, "EVNTTXT!.XLD") }, // Strings
            { AssetType.MapTexts,           (AssetLocation.Localised, "MAPTEXT!.XLD") }, // Strings
            { AssetType.ItemList,           (AssetLocation.Base,      "ITEMLIST.DAT") }, // 
            { AssetType.ItemNames,          (AssetLocation.Base,      "ITEMNAME.DAT") }, // Strings
            { AssetType.ItemGraphics,       (AssetLocation.BaseRaw,      "ITEMGFX"     ) }, // Texture
            { AssetType.FullBodyPicture,    (AssetLocation.Base,      "FBODPIX!.XLD") }, // Texture
            { AssetType.Automap,            (AssetLocation.Initial,   "AUTOMAP!.XLD") }, // 
            { AssetType.AutomapGraphics,    (AssetLocation.Base,      "AUTOGFX!.XLD") }, // Texture
            { AssetType.Song,               (AssetLocation.Base,      "SONGS!.XLD"  ) }, // Midi
            { AssetType.Sample,             (AssetLocation.Base,      "SAMPLES!.XLD") }, // Sample
            { AssetType.WaveLibrary,        (AssetLocation.Base,      "WAVELIB!.XLD") }, // Sample
            // { AssetType.Unnamed2,        (AssetLocation.Base,      ""            ) },
            { AssetType.ChestData,          (AssetLocation.Initial,   "CHESTDT!.XLD") }, // 
            { AssetType.MerchantData,       (AssetLocation.Initial,   "MERCHDT!.XLD") }, // 
            { AssetType.NpcCharacterData,   (AssetLocation.Initial,   "NPCCHAR!.XLD") }, // 
            { AssetType.MonsterGroup,       (AssetLocation.Base,      "MONGRP!.XLD" ) }, // 
            { AssetType.MonsterCharacter,   (AssetLocation.Base,      "MONCHAR!.XLD") }, // Texture
            { AssetType.MonsterGraphics,    (AssetLocation.Base,      "MONGFX!.XLD" ) }, // Texture
            { AssetType.CombatBackground,   (AssetLocation.Base,      "COMBACK!.XLD") }, // Texture
            { AssetType.CombatGraphics,     (AssetLocation.Base,      "COMGFX!.XLD" ) }, // Texture
            { AssetType.TacticalIcon,       (AssetLocation.Base,      "TACTICO!.XLD") }, // Texture
            { AssetType.SpellData,          (AssetLocation.Base,      "SPELLDAT.DAT") }, // Spell
            { AssetType.SmallNpcGraphics,   (AssetLocation.Base,      "NPCKL!.XLD"  ) }, // Texture
            { AssetType.Flic,               (AssetLocation.Localised, "FLICS!.XLD"  ) }, // Video
            { AssetType.Dictionary,         (AssetLocation.Localised, "WORDLIS!.XLD") }, // Dictionary
            { AssetType.Script,             (AssetLocation.Base,      "SCRIPT!.XLD" ) }, // Script
            { AssetType.Picture,            (AssetLocation.Base,      "PICTURE!.XLD") }, // Texture (ILBM)
            { AssetType.TransparencyTables, (AssetLocation.Base,      "TRANSTB!.XLD") }
        };
        // ReSharper restore StringLiteralTypo

        string GetXldPath(AssetLocation location, GameLanguage language, string baseName, int number)
        {
            Debug.Assert(number >= 0);
            Debug.Assert(number <= 9);

            var baseDir = @"C:\Depot\Main\bitbucket\ualbion\albion_sr\CD\XLDLIBS"; // TODO: Pull from config
            switch (location)
            {
                case AssetLocation.Base:
                    if(!baseName.Contains("!"))
                        return Path.Combine(baseDir, baseName);
                    return Path.Combine(baseDir, baseName.Replace("!", number.ToString()));

                case AssetLocation.Localised:
                    if (!baseName.Contains("!"))
                        return Path.Combine(baseDir, language.ToString().ToUpper(), baseName);
                    return Path.Combine(baseDir, language.ToString().ToUpper(), baseName.Replace("!", number.ToString()));

                case AssetLocation.Initial:
                    if (!baseName.Contains("!"))
                        return Path.Combine(baseDir, "INITIAL", baseName);
                    return Path.Combine(baseDir, "INITIAL", baseName.Replace("!", number.ToString()));

                case AssetLocation.Current:
                    if (!baseName.Contains("!"))
                        return Path.Combine(baseDir, "CURRENT", baseName);
                    return Path.Combine(baseDir, "CURRENT", baseName.Replace("!", number.ToString()));

                case AssetLocation.BaseRaw: return Path.Combine(baseDir, baseName);
                case AssetLocation.LocalisedRaw: return Path.Combine(baseDir,  language.ToString().ToUpper(), baseName);
                default: throw new ArgumentOutOfRangeException("Invalid asset location");
            }
        }

        readonly string[] _overrideExtensions = { "bmp", "png", "wav", "json", "mp3" };

        string GetOverridePath(AssetLocation location, GameLanguage language, string baseName, int number, int objectNumber)
        {
            string Try(string x)
            {
                foreach (var extension in _overrideExtensions)
                {
                    var path = $"{x}.{extension}";
                    if (File.Exists(path))
                        return path;
                }
                return null;
            }

            Debug.Assert(number >= 0);
            Debug.Assert(number <= 9);

            var baseDir = @"C:\Depot\Main\bitbucket\ualbion\data"; // TODO: Pull from config
            switch (location)
            {
                case AssetLocation.Base:
                    if(!baseName.Contains("!"))
                        return Try(Path.Combine(baseDir, baseName, objectNumber.ToString()));
                    return Try(Path.Combine(baseDir, baseName.Replace("!", number.ToString()), objectNumber.ToString()));

                case AssetLocation.BaseRaw:
                    return Try(Path.Combine(baseDir, baseName));

                case AssetLocation.Localised:
                    if (!baseName.Contains("!"))
                        return Path.Combine(baseDir, language.ToString().ToUpper(), baseName, objectNumber.ToString());
                    return Path.Combine(baseDir, language.ToString().ToUpper(), baseName.Replace("!", number.ToString()), objectNumber.ToString());

                case AssetLocation.LocalisedRaw:
                    return Try(Path.Combine(baseDir, language.ToString().ToUpper(), baseName));

                case AssetLocation.Initial:
                    if (!baseName.Contains("!"))
                        return Path.Combine(baseDir, "INITIAL", baseName, objectNumber.ToString());
                    return Path.Combine(baseDir, "INITIAL", baseName.Replace("!", number.ToString()), objectNumber.ToString());

                case AssetLocation.Current:
                    if (!baseName.Contains("!"))
                        return Path.Combine(baseDir, "CURRENT", baseName, objectNumber.ToString());
                    return Path.Combine(baseDir, "CURRENT", baseName.Replace("!", number.ToString()), objectNumber.ToString());

                default: throw new ArgumentOutOfRangeException("Invalid asset location");
            }
        }

        object LoadAsset(AssetType type, int id, GameLanguage language, object context)
        {
            int xldIndex = id / 1000;
            Debug.Assert(xldIndex >= 0);
            Debug.Assert(xldIndex <= 9);
            int objectIndex = id % 1000;
            var (location, baseName) = _assetFiles[type];

            var overrideFilename = GetOverridePath(location, language, baseName, xldIndex, objectIndex);
            if (overrideFilename != null || IsLocationRaw(location))
            {
                var path = overrideFilename ?? GetXldPath(location, language, baseName, id);
                using(var stream = File.OpenRead(path))
                using (var br = new BinaryReader(stream))
                {
                    var asset = AssetLoader.Load(br, type, (int)stream.Length, context);
                    if(asset == null)
                        throw new InvalidOperationException($"Object {type}:{id} could not be loaded from file {path}");

                    return asset;
                }
            }

            if (!_xlds.ContainsKey(type))
            {
                _xlds[type] = new XldFile[10];
                for (int i = 0; i < (baseName.Contains("!") ? 10 : 1); i++)
                {
                    var filename = GetXldPath(location, language, baseName, i);
                    if(File.Exists(filename))
                        _xlds[type][i] = new XldFile(filename);
                }
            }

            var xldArray = _xlds[type];
            var xld = xldArray[xldIndex];
            if (xld == null)
                throw new InvalidOperationException($"XLD not found for object: {type}:{id} in {baseName} ({location})");

            using (var br = xld.GetReaderForObject(objectIndex, out var length))
            {
                var asset = AssetLoader.Load(br, type,length, context);
                if (asset == null)
                    throw new InvalidOperationException($"Object {type}:{id} could not be loaded from XLD {xld.Filename}");

                return asset;
            }
        }

        bool IsLocationRaw(AssetLocation location)
        {
            switch (location)
            {
                case AssetLocation.BaseRaw:
                case AssetLocation.LocalisedRaw:
                    return true;
                default: return false;
            }
        }

        object LoadAssetCached(AssetType type, int id, GameLanguage language) { return LoadAssetCached(type, id, language, null); }
        object LoadAssetCached(AssetType type, int id, object context = null) { return LoadAssetCached(type, id, GameLanguage.English, context); }
        object LoadAssetCached(AssetType type, int id, GameLanguage language, object context)
        {
            if (_assetCache.TryGetValue(type, out var typeCache))
            {
                if (typeCache.TryGetValue(id, out var cachedAsset))
                    return cachedAsset;
            }
            else _assetCache[type] = new Dictionary<int, object>();

            var newAsset = LoadAsset(type, id, language, context);
            _assetCache[type][id] = newAsset;
            return newAsset;
        }

        public static Map2D LoadMap2D(int id) { return (Map2D)Instance.LoadAssetCached(AssetType.MapData, id); }
        public static Map3D LoadMap3D(int id) { return (Map3D)Instance.LoadAssetCached(AssetType.MapData, id); }
        public static AlbionPalette LoadPalette(int id)
        {
            var commonPalette = (byte[])Instance.LoadAssetCached(AssetType.PaletteNull, 0);
            return (AlbionPalette)Instance.LoadAssetCached(AssetType.Palette, id, new AlbionPalette.PaletteContext(id, commonPalette));
        }

        public static AlbionSprite LoadTexture(AssetType type, int id) { return (AlbionSprite)Instance.LoadAssetCached(type, id); }
        public static string LoadString(AssetType type, int id, GameLanguage language) { return (string)Instance.LoadAssetCached(AssetType.MapData, id, language); }
        public static AlbionSample LoadSample(AssetType type, int id) { return (AlbionSample)Instance.LoadAssetCached(type, id); }
        public static AlbionFont LoadFont(int id) { return (AlbionFont)Instance.LoadAssetCached(AssetType.Font, id); }

        public static AlbionVideo LoadVideo(int id, GameLanguage language)
        {
            // Don't cache videos.
            return (AlbionVideo) Instance.LoadAsset(AssetType.Flic, id, language, null);
        }
    }
}