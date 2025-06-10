using System.Diagnostics;
using System.Text;
using log4net;
using log4net.Config;
using static NotesServer.Configuration;

namespace NotesServer.Services;

public enum LogLevel { Debug, Info, Warn, Error, Fatal }

/// <summary>
/// A minimalistic wrapper for log4net
/// </summary>
[RegisterImplementation(ServiceRegisterType.Singleton, typeof(LoggerService))]
public class LoggerService
{
    public LoggerService() => ConfigureLogger();

    /// <summary>
    /// Write logs
    /// </summary>
    public void WriteLine(object o, LogLevel level = LogLevel.Info, ILog? log4netLoggerInstance = null)
    {
        ILog logger;

        if (log4netLoggerInstance != null)
        {
            logger = log4netLoggerInstance;
        }
        else
        {
            // Get logger of calling type (this is horrible for performance but the caller code looks better this way)
            StackTrace st = new(true);
            var stFrames = st.GetFrames();
            var reconstructedOriginType = stFrames.ElementAt(1).GetMethod()?.DeclaringType;
            if (reconstructedOriginType == null) return;
            while (reconstructedOriginType != null && reconstructedOriginType.FullName != null && reconstructedOriginType.FullName.Contains("DisplayClass"))
                reconstructedOriginType = reconstructedOriginType.DeclaringType;
            if (reconstructedOriginType == null) return;
            logger = LogManager.GetLogger(reconstructedOriginType);
        }

        if (level == LogLevel.Debug)
            logger.Debug(o);
        else if (level == LogLevel.Info)
            logger.Info(o);
        else if (level == LogLevel.Warn)
            logger.Warn(o);
        else if (level == LogLevel.Error)
            logger.Error(o);
        else if (level == LogLevel.Fatal)
            logger.Fatal(o);
    }

    /// <summary>
    /// Override default configuration
    /// </summary>
    public void ConfigureLogger(bool logToConsole = true, bool logToDebug = false, bool logToFile = true)
    {
        // Set log level based on run configuration
#if DEBUG
        var levelValue = @"<level value=""ALL"" />";
#else
        var levelValue = @"<level value=""INFO"" />";
#endif

        // Add used loggers
        var rootBody = (logToConsole ? @"<appender-ref ref=""ConsoleAppender"" />" : "") +
                       (logToDebug ? @"<appender-ref ref=""DebugAppender"" />" : "") +
                       (logToFile ? @"<appender-ref ref=""FileAppender"" />" : "");
        var body =
            (logToConsole ? @"
                <appender name=""ConsoleAppender"" type=""log4net.Appender.ConsoleAppender"">
                    <layout type=""log4net.Layout.PatternLayout"">
                        <conversionPattern value=""%date %-5level%logger - %message%newline"" />
                    </layout>
                </appender>" : "") +
            (logToDebug ? @"
                <appender name=""DebugAppender"" type=""log4net.Appender.DebugAppender"">
                    <layout type=""log4net.Layout.PatternLayout"">
                        <conversionPattern value=""%date %-5level%logger - %message%newline"" />
                    </layout>
                </appender>" : "") +
            (logToFile ? @"
                <appender name=""FileAppender"" type=""log4net.Appender.RollingFileAppender"">
                    <file value=""log.txt"" />
                    <lockingModel type=""log4net.Appender.FileAppender+MinimalLock"" />
                    <appendToFile value=""true"" />
                    <rollingStyle value=""Size"" />
                    <maxSizeRollBackups value=""2"" />
                    <maximumFileSize value=""500KB"" />
                    <staticLogFileName value=""true"" />
                    <layout type=""log4net.Layout.PatternLayout"">
                        <conversionPattern value=""%date %-5level%logger - %message%newline"" />
                    </layout>
                    <threshold value=""INFO""/>
                </appender>" : "");

        // Combine
        var config = $@"
                <log4net>
                    <root>
                        {levelValue}
                        {rootBody}
                    </root>
                    {body}
                </log4net>";

        // Apply using string stream
        using var stringStream = new MemoryStream(Encoding.UTF8.GetBytes(config));
        XmlConfigurator.Configure(stringStream);
    }
}
