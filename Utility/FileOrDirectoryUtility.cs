using System;
using UnityEngine;
using System.Collections;
using System.IO;

namespace Utility
{
    public class PathUtility
    {
        
        public static string Trim(string root, string filePath)
        {
            filePath = filePath.Replace(root, "");
            filePath = ConvertDirectorySeparator(filePath);
            filePath = filePath.TrimStart('/');
            return filePath;
        }

        public static string GetParentDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogUtility.LogWarning("The path is null or empty!");
                return string.Empty;
            }
            path = path.TrimEnd('/');
            return Path.GetDirectoryName(path);
        }

        public static string ConvertDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogUtility.LogWarning("The path is null or empty!");
                return string.Empty;
            }
            return path.Replace(@"\", "/");
        }
    } 
    //Editor下用UnityEditor.FileUtil
    public class FileUtility
    {
        public static void Delete(string path)
        {
            if (!Exists(path))
                return;
#if UNITY_METRO
            UnityEngine.Windows.File.Delete(path);

#else
            File.Delete(path);
#endif
        }


        public static bool Exists(string path)
        {
#if UNITY_METRO
            return UnityEngine.Windows.File.Exists(path);

#else
            return File.Exists(path);
#endif
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (!Exists(path))
            {
                LogUtility.LogWarning("The file doest not exists, path:" + path);
                return new byte[0];
            }
#if UNITY_METRO
            return UnityEngine.Windows.File.ReadAllBytes(path);

#else
            return File.ReadAllBytes(path);
#endif
        }

        public static void WriteAllBytes(string path, byte[] data)
        {
            //先创建目录
            string directoryPath = Path.GetDirectoryName(path);
            if(!DirectoryUtility.Exists(directoryPath))
                DirectoryUtility.CreateDirectory(directoryPath);
#if UNITY_METRO
            UnityEngine.Windows.File.WriteAllBytes(path,data);

#else
            File.WriteAllBytes(path, data);
#endif
        }
    }

    public class DirectoryUtility
    {
        
        public static void CreateDirectory(string path)
        {
            if (Exists(path))
                return;
#if UNITY_METRO
            UnityEngine.Windows.Directory.CreateDirectory(path);
#else
            Directory.CreateDirectory(path);
#endif


        }
        
        public static void Delete(string path)
        {
            if (!Exists(path))
                return;
#if UNITY_METRO
            UnityEngine.Windows.Directory.Delete(path);
#else
            Directory.Delete(path,true);
#endif
        }

        public static bool Exists(string path)
        {
#if UNITY_METRO
            return UnityEngine.Windows.Directory.Exists(path);
#else
            return Directory.Exists(path);
#endif

        }
    }

}

