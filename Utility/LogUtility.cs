/*
 * File: Assets/Scripts/Game/Utility/LoggerUtility.cs
 * Project: ****
 * Company: Lucky
 * Code Porter: D.S.Qiu 
 * Create Date: 10/9/2015 10:11:53 PM
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
#endif
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Utility
{
    public enum LogLevel : byte
    {
        None = 0,
        Exception = 1,
        Error = 2,
        Warning = 3,
        Info = 4,
    }

    public class LogUtility
    {
        

        public static LogLevel logLevel = LogLevel.Info;
        public static string infoColor = "#909090";
        public static string warningColor = "orange";
        public static string errorColor = "red";

        public static void LogBreak(object message, UnityEngine.Object sender = null)
        {
            LogInfo(message, sender);
            Debug.Break();
        }

        public static void LogFormat(LogLevel level,string format, params object[] message)
        {
            if (logLevel >= level)
                LogLevelFormat(level, string.Format(format, message), null);
        }

        public static void LogInfo(object message, UnityEngine.Object sender = null)
        {
            if(logLevel >= LogLevel.Info)
                LogLevelFormat(LogLevel.Info,message,sender);
        }

        public static void LogWarning(object message, UnityEngine.Object sender = null)
        {
            if (logLevel >= LogLevel.Warning)
                LogLevelFormat(LogLevel.Warning, message,  sender);
        }

        public static void LogError(object message, UnityEngine.Object sender = null)
        {
            if (logLevel >= LogLevel.Error)
            {
                LogLevelFormat(LogLevel.Error, message, sender);
            }
        }

        public static void LogException(Exception exption, UnityEngine.Object sender = null)
        {
            if (logLevel >= LogLevel.Exception)
            {
                LogLevelFormat(LogLevel.Exception, exption, sender);
            }
        }

        private static void LogLevelFormat(LogLevel level, object message, UnityEngine.Object sender)
        {
            string levelFormat =  level.ToString().ToUpper();
            StackTrace stackTrace = new StackTrace(true);
            var stackFrame = stackTrace.GetFrame(2);
#if UNITY_EDITOR
            s_LogStackFrameList.Add(stackFrame);
#endif
            string stackMessageFormat = Path.GetFileName(stackFrame.GetFileName()) + ":" + stackFrame.GetMethod().Name + "():at line " + stackFrame.GetFileLineNumber();
            string timeFormat = "Frame:" + Time.frameCount + "," + DateTime.Now.Millisecond + "ms";
            string objectName = string.Empty;
            string colorFormat = infoColor;
            if (level == LogLevel.Warning)
                colorFormat = warningColor;
            else if (level == LogLevel.Error)
                colorFormat = errorColor;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("<color={3}>[{0}][{4}][{1}]{2}</color>", levelFormat, timeFormat, message, colorFormat, stackMessageFormat);
            Debug.Log(sb,sender);
        }

#if UNITY_EDITOR
        private static int s_InstanceID;
        private static int s_Line = 104;
        private static List<StackFrame> s_LogStackFrameList = new List<StackFrame>();
        //ConsoleWindow
        private static object s_ConsoleWindow = null;
        private static object s_LogListView;
        private static FieldInfo s_LogListViewTotalRows;
        private static FieldInfo s_LogListViewCurrentRow;
        //LogEntry
        private static MethodInfo s_LogEntriesGetEntry;
        private static object s_LogEntry;
        //instanceId ��UnityEngine.Object������ʱ InstanceID Ϊ������ֻ���� LogEntry.Condition �ж�
        private static FieldInfo s_LogEntryInstanceId;
        private static FieldInfo s_LogEntryLine;
        private static FieldInfo s_LogEntryCondition;
        static LogUtility()
        {
            s_InstanceID = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Plugins/Utility/LogUtility.cs").GetInstanceID();
            s_LogStackFrameList.Clear();

            //GetConsoleWindowListView();
        }

        private static void GetConsoleWindowListView()
        {
            if (s_LogListView == null)
            {
                Assembly unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
                Type consoleWindowType = unityEditorAssembly.GetType("UnityEditor.ConsoleWindow");
                FieldInfo fieldInfo = consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
                s_ConsoleWindow = fieldInfo.GetValue(null);
                FieldInfo listViewFieldInfo = consoleWindowType.GetField("m_ListView", BindingFlags.Instance | BindingFlags.NonPublic);
                s_LogListView = listViewFieldInfo.GetValue(s_ConsoleWindow);
                s_LogListViewTotalRows = listViewFieldInfo.FieldType.GetField("totalRows", BindingFlags.Instance | BindingFlags.Public);
                s_LogListViewCurrentRow = listViewFieldInfo.FieldType.GetField("row", BindingFlags.Instance | BindingFlags.Public);
                //LogEntries
                Type logEntriesType = unityEditorAssembly.GetType("UnityEditorInternal.LogEntries");
                s_LogEntriesGetEntry = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                Type logEntryType = unityEditorAssembly.GetType("UnityEditorInternal.LogEntry");
                s_LogEntry = Activator.CreateInstance(logEntryType);
                s_LogEntryInstanceId = logEntryType.GetField("instanceID", BindingFlags.Instance | BindingFlags.Public);
                s_LogEntryLine = logEntryType.GetField("line", BindingFlags.Instance | BindingFlags.Public);
                s_LogEntryCondition = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public);
            }
        }
        private static StackFrame GetListViewRowCount()
        {
            GetConsoleWindowListView();
            if (s_LogListView == null)
                return null;
            else
            {
                int totalRows = (int)s_LogListViewTotalRows.GetValue(s_LogListView);
                int row = (int)s_LogListViewCurrentRow.GetValue(s_LogListView);
                int logByThisClassCount = 0;
                for (int i = totalRows - 1; i >= row; i--)
                {
                    s_LogEntriesGetEntry.Invoke(null, new object[] { i, s_LogEntry });
                    string condition = s_LogEntryCondition.GetValue(s_LogEntry) as string;
                    //�ж��Ƿ�����LoggerUtility��ӡ����־
                    if (condition.Contains("][") && condition.Contains("Frame"))
                        logByThisClassCount++;
                }

                //ͬ����־�б�ConsoleWindow ���Clear ������
                while (s_LogStackFrameList.Count > totalRows)
                    s_LogStackFrameList.RemoveAt(0);
                if (s_LogStackFrameList.Count >= logByThisClassCount)
                    return s_LogStackFrameList[s_LogStackFrameList.Count - logByThisClassCount];
                return null;
            }
        }

        [UnityEditor.Callbacks.OnOpenAssetAttribute(0)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (instanceID == s_InstanceID && s_Line == line)
            {
                var stackFrame = GetListViewRowCount();
                if (stackFrame != null)
                {
                    string fileName = stackFrame.GetFileName();
                    string fileAssetPath = fileName.Substring(fileName.IndexOf("Assets"));
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(fileAssetPath), stackFrame.GetFileLineNumber());
                    return true;
                }
            }
           
            return false;
        }
#endif
    }

}
