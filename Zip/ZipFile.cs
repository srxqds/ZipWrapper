/*----------------------------------------------------------------
// Copyright (C) 2015 广州，Lucky Game
//
// 模块名：
// 创建者：D.S.Qiu
// 修改者列表：
// 创建日期：April 02 2016
// 模块描述：
//----------------------------------------------------------------*/

using System.Collections.Generic;
using Utility;

namespace System.IO.Compression
{
    public class ZipFile
    {
        public static ZipArchive.Compression compressMode;

        public static void ZipDirectory(string zipFileName, string sourceDirectory)
        {
            if (!DirectoryUtility.Exists(sourceDirectory))
            {
                LogUtility.LogWarning("The zip directory does not exist!");
                return;
            }
            string[] files = Directory.GetFiles(sourceDirectory,"*.*",SearchOption.AllDirectories);
            string zipInArchiveRoot = PathUtility.GetParentDirectory(sourceDirectory);
            Zip(zipFileName, zipInArchiveRoot,files);
        }

        public static void Zip(string zipFileName, string zipInArchiveRoot, params string[] files)
        {
            if (files.IsNullOrEmpty())
            {
                LogUtility.LogWarning("There are no input compress file!");
                return;
            }
            FileUtility.Delete(zipFileName);
            DirectoryUtility.CreateDirectory(Path.GetDirectoryName(zipFileName));
            try
            {
                using (ZipArchive zipArchive = ZipArchive.Create(zipFileName, string.Empty))
                {
                    string inputFile;
                    string fileNameInArchive;
                    for (int i = 0; i < files.Length; i++)
                    {
                        inputFile = files[i];
                        if (!inputFile.Contains(zipInArchiveRoot))
                        {
                            LogUtility.LogWarning("The file isn't in the root directory!");
                            continue;
                        }
                        fileNameInArchive = PathUtility.Trim(zipInArchiveRoot, inputFile);
                        zipArchive.AddFile(compressMode, inputFile, fileNameInArchive, string.Empty);
                    }
                    //ZipArchive.Close();
                }
            }
            catch (Exception ex)
            {
                LogUtility.LogError("[ZipFile.Zip]" + ex.ToString());
            }
        }

        //com
        public static void Unzip(string zipFilePath, string destDirectory)
        {
            if(FileUtility.Exists(zipFilePath))
            {
                try
                {
                    using (ZipArchive zipArchive = ZipArchive.Open(zipFilePath, FileAccess.Read))
                    {
                        List<ZipArchive.ZipEntry> fileEntries = zipArchive.GetEntries();
                        ZipArchive.ZipEntry zipEntry;
                        string saveFilePath;
                        for (int i = 0; i < fileEntries.Count; i++)
                        {
                            zipEntry = fileEntries[i];
                            saveFilePath = Path.Combine(destDirectory, zipEntry.FilenameInZip);
                            zipArchive.ExtractFile(zipEntry, saveFilePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtility.LogError("[ZipFile.UnZip]" + ex.ToString());
                }
            }
            else
            {
                LogUtility.LogError("The zip file does not exist! " + zipFilePath);
            }
        }

        /*public static void AddFile(string filePath, string fileInArchiveName)
        {
            
        }

        public static void RemoveFile(string fileInArchiveName)
        {
            
        }*/
        
    }
}
