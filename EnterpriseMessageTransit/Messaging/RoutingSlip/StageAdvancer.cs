namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Implémentation pure de <see cref="IStageAdvancer"/> — aucun I/O, aucune dépendance SDK Azure.
    /// <para>
    /// Stratégies de résolution d'index (dans l'ordre) :
    /// <list type="number">
    /// <item>Correspondance exacte sur <c>StageId</c> (insensible à la casse).</item>
    /// <item>Correspondance exacte sur <c>Target</c> (insensible à la casse).</item>
    /// <item>Stage de forme <c>Consumer.Action</c> → essai sur la partie avant le point (<c>baseSeg</c>).</item>
    /// <item>Stage sans point → essai sur le préfixe de <c>StageId</c> avant le premier point.</item>
    /// </list>
    /// Ces stratégies reproduisent fidèlement celles de l'ancienne méthode
    /// <c>BaseConsumer.FindIndexFromStage</c>.
    /// </para>
    /// </summary>
    public sealed class StageAdvancer : IStageAdvancer
    {
        /// <summary>Instance statique réutilisable — sans état mutable.</summary>
        public static readonly StageAdvancer Default = new();

        /// <inheritdoc/>
        public int FindIndex(RoutingSlip slip, string effectiveStage)
        {
            if (slip is null) throw new ArgumentNullException(nameof(slip));
            if (string.IsNullOrWhiteSpace(effectiveStage))
                throw new InvalidOperationException("Le stage effectif ne peut pas être vide.");

            var stages = slip.Stages;

            // Stratégie 1 — correspondance exacte sur StageId
            for (int i = 0; i < stages.Count; i++)
                if (string.Equals(stages[i].StageId, effectiveStage, StringComparison.OrdinalIgnoreCase))
                    return i;

            // Stratégie 2 — correspondance exacte sur Target
            for (int i = 0; i < stages.Count; i++)
                if (string.Equals(stages[i].Target, effectiveStage, StringComparison.OrdinalIgnoreCase))
                    return i;

            var dot = effectiveStage.IndexOf('.');

            // Stratégie 3 — si Consumer.Action, tester la partie Consumer seule (baseSeg)
            if (dot > 0)
            {
                var baseSeg = effectiveStage[..dot];
                for (int i = 0; i < stages.Count; i++)
                    if (string.Equals(stages[i].Target, baseSeg, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(stages[i].StageId, baseSeg, StringComparison.OrdinalIgnoreCase))
                        return i;
            }

            // Stratégie 4 — si pas de point, comparer au préfixe Consumer du StageId (Consumer.Action → Consumer)
            if (dot < 0)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    var sid = stages[i].StageId;
                    var sidDot = sid.IndexOf('.');
                    if (sidDot > 0
                     && string.Equals(sid[..sidDot], effectiveStage, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            throw new InvalidOperationException(
                $"Stage '{effectiveStage}' introuvable dans l'itinéraire ({stages.Count} étapes).");
        }

        /// <inheritdoc/>
        public RoutingSlipResult Advance(
            RoutingSlip slip,
            string currentStage,
            IReadOnlyDictionary<string, object>? variables = null)
        {
            if (slip is null) throw new ArgumentNullException(nameof(slip));
            if (slip.Stages.Count == 0)
                throw new InvalidOperationException("Itinéraire vide — impossible d'avancer.");

            var idx = FindIndex(slip, currentStage);
            var isFinal = idx == slip.Stages.Count - 1;
            var nextStage = isFinal ? null : slip.Stages[idx + 1].StageId;

            return new RoutingSlipResult(
                CurrentStage: currentStage,
                CurrentIndex: idx,
                NextStage: nextStage,
                IsFinal: isFinal,
                Variables: variables);
        }
    }
}
