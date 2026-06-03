using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace RAMQ.Samples.MessageTransitHelper
{
    /// <summary>
    /// Formateur console pour les samples EMT.
    /// Injecte les codes ANSI directement dans le texte — fonctionne même quand
    /// le terminal func CLI rende le stdout worker en bleu uniforme.
    ///
    /// Couleurs :
    ///   Debug       → gris
    ///   Information → cyan (visible sur fond sombre)
    ///   Warning     → jaune
    ///   Error       → rouge vif
    ///   Critical    → rouge sur fond blanc
    /// </summary>
    public sealed class EMTConsoleFormatter : ConsoleFormatter
    {
        public const string FormatterName = "emt";

        private static readonly string Reset  = "\x1b[0m";
        private static readonly string Grey   = "\x1b[90m";
        private static readonly string Cyan   = "\x1b[36m";
        private static readonly string Yellow = "\x1b[33m";
        private static readonly string Red    = "\x1b[91m";
        private static readonly string Bold   = "\x1b[1m";

        public EMTConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
            : base(FormatterName) { }

        public override void Write<TState>(
            in LogEntry<TState> logEntry,
            IExternalScopeProvider? scopeProvider,
            TextWriter textWriter)
        {
            var (color, label) = logEntry.LogLevel switch
            {
                LogLevel.Debug       => (Grey,   "dbug"),
                LogLevel.Information => (Cyan,   "info"),
                LogLevel.Warning     => (Yellow, "warn"),
                LogLevel.Error       => (Red,    "fail"),
                LogLevel.Critical    => (Bold + Red, "crit"),
                _                    => (Reset,  "trce")
            };

            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var category  = logEntry.Category;
            var message   = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception)
                            ?? logEntry.State?.ToString()
                            ?? string.Empty;

            // Format : [HH:mm:ss.fff] LEVEL CategoryShort: message
            var shortCategory = category.Contains('.')
                ? category[(category.LastIndexOf('.') + 1)..]
                : category;

            textWriter.Write($"{color}[{timestamp}] {label}{Reset}: ");
            textWriter.Write($"{Grey}{shortCategory}{Reset} ");
            textWriter.WriteLine(message);

            if (logEntry.Exception is { } ex)
                textWriter.WriteLine($"{Red}  {ex.GetType().Name}: {ex.Message}{Reset}");
        }
    }

    /// <summary>
    /// Extensions pour enregistrer le formateur EMT.
    /// </summary>
    public static class EMTConsoleFormatterExtensions
    {
        /// <summary>
        /// Ajoute la console avec le formateur EMT (couleurs par niveau, visible dans func CLI).
        /// </summary>
        public static ILoggingBuilder AddEMTConsole(this ILoggingBuilder logging)
        {
            logging.AddConsole(opts => opts.FormatterName = EMTConsoleFormatter.FormatterName);
            logging.AddConsoleFormatter<EMTConsoleFormatter, ConsoleFormatterOptions>();
            return logging;
        }
    }
}
