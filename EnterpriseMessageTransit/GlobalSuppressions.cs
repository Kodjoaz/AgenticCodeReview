// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Phase 2 (P2-C1) — accès internal aux tests
[assembly: InternalsVisibleTo("EnterpriseMessageTransit.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // NSubstitute / Moq

// Projets Exemples — Workers Routing Slip (IRoutingSlipExecutor interne accessible via IServiceScopeFactory)
[assembly: InternalsVisibleTo("RAMQ.Samples.Queue.RoutingSlip.Worker")]
[assembly: InternalsVisibleTo("RAMQ.Samples.Queue.RoutingSlip.Booking.Worker")]
[assembly: InternalsVisibleTo("RAMQ.Samples.Topic.RoutingSlip.Worker")]
[assembly: InternalsVisibleTo("RAMQ.Samples.Topic.RoutingSlip.Booking.Worker")]

[assembly: SuppressMessage("Maintenance", "RAMQ0101:Règle RAMQ 7.8 : L'utilisation du mot clé 'ref' ou 'out' n'est pas recommandé.", Justification = "Pattern TryResolve standard .NET (TryParse, TryGetValue). Le paramètre out est idiomatique pour les méthodes Try* retournant un booléen.", Scope = "member", Target = "~M:RAMQ.COM.EnterpriseMessageTransit.Configuration.EndpointResolver.TryResolve(System.String,System.String,System.String,RAMQ.COM.EnterpriseMessageTransit.Configuration.EndpointSettings@)~System.Boolean")]
