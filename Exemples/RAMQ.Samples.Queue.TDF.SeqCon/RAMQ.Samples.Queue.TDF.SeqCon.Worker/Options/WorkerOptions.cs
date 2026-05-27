namespace RAMQ.Samples.Queue.TDF.SeqCon.Worker.Options;

/// <summary>
/// Options externalisées pour l'Activateur TDF.
/// Section de configuration : "Worker".
/// </summary>
public sealed class TdfActivateurOptions
{
    public const string SectionName = "Worker";

    /// <summary>
    /// Planification CRON du TimerTrigger.
    /// Valeur par défaut : toutes les 5 minutes.
    /// </summary>
    public string Schedule { get; set; } = "0 */5 * * * *";
}
