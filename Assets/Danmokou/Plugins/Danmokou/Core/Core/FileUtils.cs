﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Graphics;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ProtoBuf;
using Suzunoya.Data;
using UnityEngine;
#pragma warning disable 168

/// <summary>
/// Module providing methods to access on-disk files, as well as some convenience methods for parsing files.
/// <br/>It is preferable to use this module over File due to requirements around Application.persistentDataPath.
/// </summary>
public static class FileUtils {
    public const string SAVEDIR = "DMK_Saves/";
    public const string AYADIR = SAVEDIR + "Aya/";
    public const string INSTANCESAVEDIR = SAVEDIR + "Instances/";
    public enum ImageFormat {
        JPG,
        PNG
    }


    private static readonly JsonSerializerSettings JsonSettings = Suzunoya.Data.Serialization.JsonSettings; 

    public static IEnumerable<string> EnumerateDirectory(string dir) {
        CheckPath(ref dir);
        return Directory.EnumerateFiles(dir);
    }

    /// <summary>
    /// Modifies the path string to point to a valid location for R/W data
    /// (ie. may prepend Application.persistentDataPath), and ensures the existence of the directory.
    /// </summary>
    public static void CheckPath(ref string path) {
#if !UNITY_EDITOR && !UNITY_STANDALONE_WIN
        path = $"{Application.persistentDataPath}/{path}";
#endif
        void CheckDirectory(string? dir) {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                CheckDirectory(Path.GetDirectoryName(dir));
                Directory.CreateDirectory(dir);
            }
        }
        CheckDirectory(Path.GetDirectoryName(path));
    }

    public static T CopyJson<T>(T obj) =>
        Serialization.DeserializeJson<T>(JsonConvert.SerializeObject(obj, JsonSettings)) ?? 
        throw new Exception($"Failed to JSON-copy object {obj} of type {obj?.GetType()}");
    
    //The type may seem redundant here, but it's critical for serializing polymorphic types.
    public static void WriteJson<T>(string file, T obj) {
        CheckPath(ref file);
        using StreamWriter sw = new(file);
        sw.WriteLine(Serialization.SerializeJson(obj));
    }

    public static void WriteProto(string file, object obj) {
        CheckPath(ref file);
        using var fw = File.Create(file);
        Serializer.Serialize(fw, obj);
    }
    public static void WriteProtoCompressed(string file, object obj) {
        CheckPath(ref file);
        using var fw = File.Create(file);
        DeflateStream(fw, s => Serializer.Serialize(s, obj));
    }

    private static void DeflateStream(Stream target, Action<Stream> writer) {
        var strm = new MemoryStream();
        writer(strm);
        strm.Position = 0;
        var compressed = Compress(strm);
        using var w = new BinaryWriter(target);
        w.Write(compressed);
    }
    private static Stream InflateStream(Stream source) => Decompress(source);
    private static byte[] Compress(Stream input) {
        using var compressStream = new MemoryStream();
        using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress)) {
            input.CopyTo(compressor);
            compressor.Close();
        }
        return compressStream.ToArray();
    }
    
    private static Stream Decompress(Stream input) {
        var output = new MemoryStream();
        using (var decompressor = new DeflateStream(input, CompressionMode.Decompress)) {
            decompressor.CopyTo(output);
        }
        output.Position = 0;
        return output;
    }

    public static string Read(string file) {
        CheckPath(ref file);
        using StreamReader sr = new(file);
        return sr.ReadToEnd();
    }
    public static T? ReadJson<T>(string file) where T : class {
        CheckPath(ref file);
        try {
            using StreamReader sr = new(file);
            return Serialization.DeserializeJson<T>(sr.ReadToEnd());
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from file {file}. (JSON)\n{e.Message}", false, LogLevel.WARNING);
            return null;
        }
    }
    public static T? ReadProto<T>(string file) where T : class {
        CheckPath(ref file);
        try {
            using var fr = File.OpenRead(file);
            return Serializer.Deserialize<T>(fr);
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from file {file}. (PROTO)\n{e.Message}", false, LogLevel.WARNING);
            return null;
        }
    }
    public static T? ReadProtoCompressed<T>(string file) where T : class {
        CheckPath(ref file);
        try {
            using var fr = File.OpenRead(file);
            return Serializer.Deserialize<T>(InflateStream(fr));
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from file {file}. (PROTO-C)\n{e.Message}", false, LogLevel.WARNING);
            return null;
        }
    }
    public static T? ReadProtoCompressed<T>(TextAsset file) where T : class {
        try {
            using var fr = new MemoryStream(file.bytes);
            return Serializer.Deserialize<T>(InflateStream(fr));
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from textAsset {file.name}. (PROTO-C)\n{e.Message}", false, LogLevel.WARNING);
            return null;
        }
    }
    

    public static void WriteTex(string file, Texture tex, ImageFormat format = ImageFormat.JPG) {
        CheckPath(ref file);
        var (dispose, t2d) = tex switch {
            Texture2D tex2d => (false, tex2d),
            RenderTexture rt => (true, rt.IntoTex()),
            _ => throw new Exception($"Texture of type {tex.GetType()} cannot be saved to disk")
        };
        File.WriteAllBytes(file, format switch {
            ImageFormat.JPG => t2d.EncodeToJPG(95),
            ImageFormat.PNG => t2d.EncodeToPNG(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        });
        if (dispose)
            t2d.DestroyTexOrRT();
    }
    
    public static void WriteString(string file, string text) {
        CheckPath(ref file);
        File.WriteAllText(file, text);
    }

    public static void Delete(string file) {
        CheckPath(ref file);
        if (File.Exists(file)) File.Delete(file);
    }

    public static byte[] ReadAllBytes(string file) {
        CheckPath(ref file);
        return File.ReadAllBytes(file);
    }
}