namespace RAMQ.Samples.Queue.TDF.Integration.Frontend.Options;

/// <summary>
/// Options externalisées pour le Frontend TDF.
/// Section de configuration : "Frontend".
/// </summary>
public sealed class TdfFrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>
    /// Planification CRON du TimerTrigger.
    /// Valeur par défaut : toutes les 5 minutes.
    /// </summary>
    public string Schedule { get; set; } = "0 */5 * * * *";
}

