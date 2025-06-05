using System;
using System.IO;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using UnityEngine;

public class NLogUnityLogger : MonoBehaviour
{
    [SerializeField] private string logFolderName = "";
    
    public void Awake()
    {
        string dataFolder = Application.dataPath;
        string exeFolder  = Directory.GetParent(dataFolder).FullName;
        string logPath = Path.Combine(exeFolder, logFolderName);

        if (!Directory.Exists(logPath))
        {
            Directory.CreateDirectory(logPath);
            Debug.Log("로그 경로가 없어서 새로 생성했습니다. 경로: " + logPath);
        }

        //logPath 설정.
        GlobalDiagnosticsContext.Set("LogPath", logPath);
        LogManager.ThrowConfigExceptions = true;

        string nlogConfigPath = Path.Combine(Application.streamingAssetsPath, "NLog.config");
        Debug.Log("NLog Config Path : " + nlogConfigPath);

        if(!File.Exists(nlogConfigPath))
        {
            Debug.LogError("NLog 설정 파일을 찾을 수 없습니다.");
            return;
        }

        try{
            var config = new XmlLoggingConfiguration(nlogConfigPath);
            LogManager.Configuration = config;

            // Unity 기본 로거를 NLog로 재정의
            Debug.unityLogger.logHandler = new NLogLogHandler();
            Debug.unityLogger.filterLogType = LogType.Log; // 모든 로그종류를 처리
            Debug.Log("NLog 초기화 완료. Unity Logger와 연동.");
        }
        catch(Exception ex)
        {
            Debug.LogError("NLog 설정 파일 로드 실패 : " + ex.Message);
        }
    }
}

internal class NLogLogHandler : ILogHandler
{
    private readonly ILogHandler defaultLogHandler = Debug.unityLogger.logHandler;

    // NLog Logger 인스턴스. NLog.config에 정의한 Logger이름 설정
    private static readonly NLog.Logger nlogLogger = LogManager.GetLogger("SangwhaLoger");

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        // 예외를 NLog로 전달
        string fullMessage = $"{exception.Message}\nStackTrace:\n{exception.StackTrace}";
        nlogLogger.Error(fullMessage);

        defaultLogHandler.LogException(exception, context);
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        string message = string.Format(format, args);
        string stackTrace = Environment.StackTrace; // 현재 스택 트레이스 추적.

        string fullMessage = $"{message}\nStackTrace:\n{stackTrace}";

        // Unity 로그를 NLog로 전달
        switch (logType)
        {
            case LogType.Error:
            case LogType.Exception:
                nlogLogger.Error(fullMessage);
                break;
            case LogType.Warning:
                nlogLogger.Warn(fullMessage);
                break;
            case LogType.Log:
                nlogLogger.Info(fullMessage);
                break;
        }

        // Unity 로그 출력
        defaultLogHandler.LogFormat(logType, context, format, args);
    }
}