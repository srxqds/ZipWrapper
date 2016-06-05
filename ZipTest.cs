/*----------------------------------------------------------------
// Copyright (C) 2015 广州，Lucky Game
//
// 模块名：
// 创建者：D.S.Qiu
// 修改者列表：
// 创建日期：April 03 2016
// 模块描述：
//----------------------------------------------------------------*/

using System.IO.Compression;
using UnityEngine;
using UnityEditor;

public class ZipTest
{
    //GZipStream is the same as DeflateStream but it adds some CRC to ensure the data has no error.

    [MenuItem("Test/Zip")]
    public static void DoTest()
    {
        /*string compressDirectory = @"C:\Users\D.S.Qiu\Downloads\DotNetZip-src-v1.9.1.8\DotNetZip";
        ZipFile.compressMode = ZipArchive.Compression.LZMA;
        ZipFile.ZipDirectory(EditorUtils.AssetPath2FilePath("Assets/Editor/Test/Lzma.zip"), compressDirectory);

        ZipFile.Unzip(EditorUtils.AssetPath2FilePath("Assets/Editor/Test/Lzma.zip"), EditorUtils.AssetPath2FilePath("Assets/Editor/Test/Lzma"));
        ZipFile.compressMode = ZipArchive.Compression.Deflate;
        ZipFile.ZipDirectory(EditorUtils.AssetPath2FilePath("Assets/Editor/Test/Zlib.zip"), compressDirectory);
        ZipFile.Unzip(EditorUtils.AssetPath2FilePath("Assets/Editor/Test/Zlib.zip"), EditorUtils.AssetPath2FilePath("Assets/Editor/Test/Zlib"));*/
        ZipFile.Unzip(@"E:\data\data\c场景配置表 - 副本.zip", @"E:\data\data\c场景配置表 - 副本\");
    }
}
