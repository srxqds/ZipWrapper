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
using Zip;

public class ZipTest
{
    //GZipStream is the same as DeflateStream but it adds some CRC to ensure the data has no error.

    [MenuItem("Test/Zip")]
    public static void DoTest()
    {
        string compressDirectory = @"C:\Users\D.S.Qiu\Downloads\DotNetZip-src-v1.9.1.8\DotNetZip";
        ZipUtility.compressMode = ZipStorer.Compression.LZMA;
        ZipUtility.ZipDirectory(EditorUtils.GetSystemFileFromAssetPath("Assets/Editor/Test/Lzma.zip"), compressDirectory);

        ZipUtility.Unzip(EditorUtils.GetSystemFileFromAssetPath("Assets/Editor/Test/Lzma.zip"), EditorUtils.GetSystemFileFromAssetPath("Assets/Editor/Test/Lzma"));
        ZipUtility.compressMode = ZipStorer.Compression.Deflate;
        ZipUtility.ZipDirectory(EditorUtils.GetSystemFileFromAssetPath("Assets/Editor/Test/Zlib.zip"), compressDirectory);
        ZipUtility.Unzip(EditorUtils.GetSystemFileFromAssetPath("Assets/Editor/Test/Zlib.zip"), EditorUtils.GetSystemFileFromAssetPath("Assets/Editor/Test/Zlib"));


    }
}
