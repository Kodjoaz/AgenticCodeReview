namespace RAMQ.Samples.Queue.ClaimCheck.PDF.Message
{
    /// <summary>
    /// Métadonnées d'un rapport PDF médical publié via Claim Check.
    /// Le payload réel (PDF sérialisé en JSON) est stocké en Blob Storage ;
    /// ce message ne porte que les métadonnées nécessaires au routing et à l'audit.
    /// </summary>
    public record PdfRapportMessage
    {
        public string RapportId    { get; init; } = string.Empty;
        public string PatientId    { get; init; } = string.Empty;
        public string TypeRapport  { get; init; } = string.Empty;  // ex. "IRM", "Radio", "Bilan"
        public string FileName     { get; init; } = string.Empty;
        public long   TailleOctets { get; init; }
        public DateTime DateRapport { get; init; }
    }
}
