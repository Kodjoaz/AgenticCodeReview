# HOA5 — Architecture d'intégration à l'état actuel — Flux de transmission préliminaire

> Objectif : Documenter l'architecture à l'état actuel de HOA5, en fournissant une base fiable et fondée sur des preuves pour la planification de la future migration vers Azure.  
> Public cible : Architecture d'entreprise, Équipe d'intégration, Opérations, Sécurité, et outils assistés par IA (p. ex., GitHub Copilot).  
> Statut : Ébauche v0.9 (à valider avec les experts métier).  
> Responsable : Bureau d'architecture

---

## 1. Contexte du projet

### 1.1 Nature des données

HOA5 traite des **dossiers clinico-administratifs de santé à caractère personnel** relatifs aux soins et services fournis aux individus admis ou inscrits pour une **chirurgie d'un jour** dans un centre hospitalier du Québec. Ces données sont utilisées pour :

- Évaluer les besoins en soins de santé et la consommation de services ;
- Planifier, organiser et évaluer les services rendus ;
- Transmettre les données à la **RAMQ** (Régie de l'assurance maladie du Québec).

> **Sensibilité :** Données de santé personnelles — classification **Confidentiel**. Tout traitement hors production doit être validé avec les équipes de Sécurité et de Conformité.

---

### 1.2 Types de transmission

La plateforme d'intégration MEC gère deux types de transmissions. **Ce document couvre uniquement le flux de transmission préliminaire HOA5.** Le flux de transmission régulière est une solution distincte qui partage des composants d'infrastructure TDF communs (TDF Integration, TDF Frontend), mais est hors portée ici.

Les deux types de transmission utilisent un format **XML compressé** :

| Type | Code | Taille moyenne | Taille max. (non compressée) | Entités de données | Fréquence | Condition |
|------|------|---------------|-----------------------------|--------------------|-----------|----------|
| Transmission préliminaire | **HOA5** | ~1 Mo | ~20 Mo | 3 | **Quotidienne** | Individu toujours en soins hospitaliers — aucune date de départ |
| Transmission régulière | **HOA1 / HOB1** | ~10 Mo | ~40 Mo | 10 | **Quotidienne** (à cause trauma : traité dès réception) | Individu ayant quitté l'hôpital — date de départ connue |

**Nombre de centres hospitaliers transmetteurs :** ~140 à travers le Québec.

**Destination finale :** Chaque fichier est **validé, chargé et traité** dans la base de données opérationnelle **Oracle**.

---

### 1.3 Architecture d'intégration de bout en bout (HOA5 — Transmissions préliminaires)

Le diagramme ci-dessous décrit le flux des **transmissions préliminaires HOA5** à travers six couches distinctes.

> **Clarification importante — modèle d'hébergement (vérifié dans le code source) :** Le **HOA5 Frontend est un service WCF** (`FichDestinataireEntrant.svc`, déployé sur IIS) qui **héberge le TDF Frontend** par héritage de classe. Toute la logique du protocole de transmission (authentification, acheminement de fichier, suivi, ACK) réside dans la **classe de base `DestinataireEntrant`** — c'est le TDF Frontend. La classe du service WCF HOA5 Frontend (`FichDestinataireEntrant`) hérite de `DestinataireEntrant` et redéfinit ses 3 opérations WCF avec des appels d'une seule ligne vers `MyBase.*`. À l'exécution, le code du TDF Frontend s'exécute **à l'intérieur du processus du service WCF HOA5 Frontend** — l'assemblage TDF (`RAMQ.OO.OOA2_ServTranfMsgFich_svc.dll`) est chargé via une référence de projet (HintPath) dans le même AppDomain IIS. Il n'y a **aucun appel réseau** entre eux ; `MyBase.EnvoyerLotFichier()` est un appel de méthode .NET standard en processus.
>
> **Autrement dit :** HOA5 Frontend = le service WCF (l'hôte). TDF Frontend = la classe de base à l'intérieur de ce service (la logique hébergée). Ce ne sont pas deux services distincts — c'est un seul service WCF déployé avec un comportement hérité.
>
> **Clarification d'infrastructure — modèle de déploiement :** Les composants applicatifs legacy couverts par l'état actuel — **HOA5 Frontend, TDF Frontend, TDF Integration, HOA5 Integration et HOA5 Backend** — sont tous **déployés sur des machines virtuelles Azure IaaS**. Les composants hébergés sur IIS s'exécutent sur IIS installé sur des VM Azure, et les composants hébergés par BizTalk s'exécutent sur BizTalk Server installé sur des VM Azure. Dans ce document, **hors BizTalk** versus **dans BizTalk** décrit la **frontière logique / d'exécution**, et non un modèle d'hébergement d'infrastructure différent.
>
> **Remarque de normalisation — noms des composants :** Pour assurer la cohérence entre les versions anglaise et française, les **noms canoniques des composants** sont conservés en anglais dans tout le document : **HOA5 Frontend, TDF Frontend, TDF Integration, HOA5 Integration et HOA5 Backend**.

Le **HOA5 Frontend** est le service WCF qui reçoit le fichier compressé de l'application CH. Il **héberge** le **TDF Frontend** (classe de base `DestinataireEntrant`), qui contient toute la logique du protocole de transmission. Le TDF Frontend est **générique et réutilisable pour tous les flux PPP/SFU** (HOA5, HOA1, ...) et communique avec le **TDF Integration** (BizTalk Server) en utilisant un protocole TDF standard agnostique au PPP/SFU. Le **TDF Integration** achemine vers le **HOA5 Integration** (BizTalk Server, spécifique à HOA5), qui transforme le message et appelle le **HOA5 Backend** (point de terminaison WCF). Le HOA5 Backend traite la transmission en séquence (vérifié dans le code source de `TraiterTransmPrel.vb`) : (1) sauvegarde le ZIP dans DepoApli via `SauvegarderFichierRecu` en utilisant `FileStream(FileMode.Create)`, (2) décompresse le ZIP via `ObtenirFichierXmlRecu`/`DecompresserFichZip`, (3) analyse le XML en 3 DataSets typés via `RemplirInfosFichierXmlRecu`, (4) insère dans Oracle via `InsererDonneesFich` en utilisant le wrapper `OracleOdp` avec transaction explicite (`DebuterTransaction`/`TerminerTransaction`).

> **Propriété des équipes — important pour la coordination de la migration :**
> - **TDF Integration** et **TDF Frontend** sont développés et maintenus par l'**équipe TDF** (dépôt `TDF-OOA2-Serv-Tranf-Fich`). Ce sont des **composants d'infrastructure partagée** utilisés par plusieurs flux (HOA5, HOA1, HOB1...). Tout changement aux composants TDF nécessite une coordination avec l'équipe TDF.
> - **HOA5 Integration**, **HOA5 Frontend** et **HOA5 Backend** sont développés par l'**équipe HOA5** (dépôt `MEC-AlimenterBanque`). HOA5 Integration dépend de TDF Integration pour l'acheminement des messages.

> **Contrainte de migration — frontières de mutabilité WCF (hypothèse du projet) :**
> - **HOA5 Frontend** (hors BizTalk, face à l'application CH) : son contrat de service est **verrouillé** — l'hypothèse du projet est de **ne pas modifier l'application CH**. L'interface du HOA5 Frontend doit être préservée telle quelle lors de la migration afin d'éviter tout impact sur les systèmes hospitaliers.
> - **TDF Integration et HOA5 Integration** (à l'intérieur de BizTalk) : **doivent être replateformés vers Azure** — le retrait de BizTalk est l'**objectif principal de migration** de ce projet (migration vers Enterprise Message Transit sur Azure). TDF Integration appartient à l'équipe TDF ; HOA5 Integration appartient à l'équipe HOA5 — les deux équipes doivent collaborer pour la migration.
> - **TDF Frontend et HOA5 Backend** (hors BizTalk) : peuvent être modifiés lorsque cela est techniquement **bien justifié**, **fondé sur des preuves** et **gouverné** (approbation du Bureau d'architecture requise). Les modifications au TDF Frontend nécessitent une coordination supplémentaire avec l'équipe TDF puisqu'il est partagé entre les flux.

#### 1.3.1 Vue d'ensemble des composants

| Composant | Équipe | Technologies | Rôle |
|-----------|--------|-------------|------|
| **CH App** | TI hospitalière | Application hospitalière | Envoie un fichier XML compressé via HTTPS. **~140 centres hospitaliers** à travers le Québec. |
| **HOA5 Frontend** | Équipe HOA5 | Service WCF (IIS sur VM Azure IaaS) | **Le service WCF qui héberge le TDF Frontend.** Déployé sur une **VM Azure IaaS**. La classe `FichDestinataireEntrant` hérite de `DestinataireEntrant` (classe de base du TDF Frontend). `InstanceContextMode.PerCall`. Les 3 opérations WCF (`InitierEnvoi`, `EnvoyerLotFichier`, `CorrellerEnvoyer`) sont des surcharges d'une seule ligne qui appellent `MyBase.*` — déléguant toute la logique de protocole à la classe de base TDF Frontend s'exécutant dans le même processus. Le comportement spécifique à HOA5 est injecté via l'objet stratégie `MessageFichierEntrantCH`. **Le contrat de service est verrouillé** — hypothèse du projet : le CH App ne change pas. |
| **TDF Frontend** | Équipe TDF | Classe de base hébergée dans le service WCF HOA5 Frontend (IIS sur VM Azure IaaS) | **La classe de base `DestinataireEntrant` qui contient toute la logique du protocole de transmission.** À l'exécution, elle est déployée **dans le processus IIS du HOA5 Frontend sur une VM Azure IaaS**. Compilée en un seul assemblage DLL (`RAMQ.OO.OOA2_ServTranfMsgFich_svc.dll`, depuis `OOA2_ServTranfMsgFich_svc.vbproj`). **Générique et réutilisable pour tous les flux PPP/SFU** (HOA5, HOA1...). Implémente le protocole complet en 3 étapes : **Étape 1** (`InitierEnvoi`) — authentification, vérification de la règle d'accès AGS (droit \#246), création du numéro d'échange (`CreerNoEchange()` — `IdEntIntvnEchg` + horodatage UTC `YYYYMMDDHHmmssfff`). **Étape 2** (`EnvoyerLotFichier`) — envoie le message fichier à BizTalk via `HttpClient.PostAsync` → `BTSHTTPReceive.dll` (HTTP POST), puis crée les DataSets de suivi (`SuiviMessage_DS` + `SuiviFichier_DS`) et envoie le message de suivi à BizTalk via proxy SOAP, puis génère l'accusé de réception (`GenererAccRecep()` — `IdEntIntvnEchg` + horodatage local `YYYYMMDDHHmmss`) et le retourne au CH App. **Étape 3** (`CorrellerEnvoyer`) — le CH App rappelle avec l'accusé de réception de l'étape 2; le TDF Frontend valide l'accusé de réception (`ValiderAccRecep()`), puis envoie le message de corrélation à BizTalk via proxy SOAP (`EnvoyerCorrellerServCourtage()` → port `poRecptInfoCorln`), puis efface tout l'état de l'échange (`ViderContexte()`). Au total, envoie **3 messages distincts** au TDF Integration (BizTalk) durant les étapes 2 et 3 : fichier via HTTP POST (étape 2), suivi via proxy SOAP (étape 2), corrélation via proxy SOAP (étape 3). |
| **TDF Integration** | Équipe TDF | BizTalk Server sur VM Azure IaaS | **Doit être replateformé vers Azure** (cible principale de migration). Le runtime BizTalk actuel est déployé sur une **VM Azure IaaS**. Orchestration `orcTrnsmFichEntrant` — patron **Parallel Activating Convoy** : 3 branches en attente des messages fichier, suivi et corrélation avec des délais d'expiration de 9 minutes. Persiste le suivi vers `OOA2_InscrireSuiviFich_Ws`, achemine le fichier vers HOA5 Integration. Partagé entre les flux de transmission — propriété de l'équipe TDF. |
| **HOA5 Integration** | Équipe HOA5 | BizTalk Server sur VM Azure IaaS | **Doit être replateformé vers Azure** (cible principale de migration). Le runtime BizTalk actuel est déployé sur une **VM Azure IaaS**. Spécifique à HOA5; applique la map `transformerMsgFichCHToMsgSVC`, appelle le HOA5 Backend via un port d'envoi WCF-Custom. |
| **HOA5 Backend** | Équipe HOA5 | Point de terminaison WCF (IIS sur VM Azure IaaS) | Déployé sur une **VM Azure IaaS**. Ordre de traitement vérifié dans le code source (`TraiterTransmPrel.vb`) : (1) `SauvegarderFichierRecu` — sauvegarde le ZIP dans DepoApli via `FileStream(FileMode.Create)`, (2) `ObtenirFichierXmlRecu`/`DecompresserFichZip` — décompresse, (3) `RemplirInfosFichierXmlRecu` — charge le XML dans 3 DataSets typés, (4) `InsererDonneesFich` — insertion Oracle via `OracleOdp.DebuterTransaction()`/`TerminerTransaction()`. COMMIT uniquement si le nombre de lignes correspond. **Peut être modifié si justifié** — approbation du comité d'architecture. |
| **Oracle** | Équipe DBA | BD opérationnelle Oracle | Destination finale. 3 tables : `MEC.MEC_PRE_SEJ_HOSP`, `MEC.MEC_SEJ_SOIN_ALTRN`, `MEC.MEC_PRE_SEJ_SOIN_INTSF`. INSERT SQL dynamique (pas de procédures stockées). |
| **DepoApli** | Équipe opérations | Partage de fichiers SMB sur site | Reçoit le ZIP comme **première étape** — avant Oracle. Écrit via `System.IO.FileStream` (pas à l'intérieur de la transaction Oracle). **Non couplé transactionnellement à Oracle** : pas de MSDTC, pas de validation en deux phases. Si Oracle échoue après l'écriture du ZIP, le fichier reste en permanence — scénario de fichier orphelin. **Cycle de vie opérationnel :** les fichiers sont déplacés de DepoApli vers `\\corpoprodque\donne\` (S:\) par une tâche planifiée **Control-M** — DepoApli est généralement vide après le transfert. Les fichiers servent de **mécanisme de reprise manuelle** pour le pilotage (conservation intentionnelle, pas de nettoyage automatisé). Voir sections 1.6.5 et 10.1 (fait confirmé \#4). |

> **Ce que signifie « non couplé transactionnellement » — expliqué pour les développeurs juniors :** L'écriture du fichier ZIP vers DepoApli et l'insertion dans Oracle sont **deux opérations d'E/S indépendantes exécutées en séquence** — il n'y a **aucun verrou englobant** les deux. HOA5 Backend écrit d'abord le fichier ZIP dans le partage de fichiers (fait, pas de retour en arrière possible), puis ouvre une transaction Oracle et insère les lignes. Si Oracle échoue, le fichier ZIP reste dans DepoApli sans aucune trace en base de données — c'est le scénario de **fichier orphelin**. En termes techniques : il n'y a pas de transaction distribuée (pas de MSDTC, pas de protocole XA) coordonnant les deux écritures comme une seule unité atomique. Voir section 1.6.5 pour le détail complet.

| **`YRD1_RechrRegleAcces_ws`** | Équipe TDF (AGS) | ASMX / SOAP (HTTP) | **Sécurité applicative / vérification des règles d'accès** (système AGS). Vérifie que l'utilisateur appelant possède la permission applicative spécifique \#246 pour HOA5. Appelé par le TDF Frontend durant l'étape 1 (`InitierEnvoi`) via `ValiderSecurIdUtilIntvn()` — vérifié dans le code source de `Destinataire.vb`. Utilise le proxy SOAP `RechrRegleAccesSoapClient` → méthode `ObtnDroitContxRegleAcces`. Si le droit \#246 est absent, rejet avec l'erreur 9282. Résultats mis en cache pendant 90 secondes. Hébergé sur `nlb-fonct-app4.ramq.gov`. **Mode de défaillance confirmé : pas de mécanisme de repli, échec immédiat** — si le service est indisponible, l'exception se propage directement à l'application CH; aucune boucle de réessai au niveau du TDF Frontend (vérifié dans le code source). **Dans le dépôt en portée** — `AGS-ControleAcces`. Voir sections 11.1 et fait confirmé \#6 (section 10.1). |
| **`OOA2_InscrireSuiviFich_Ws`** | Équipe TDF | ASMX / SOAP (via adaptateur SOAP BizTalk) | **Persistance du suivi/audit ET persistance du contenu des fichiers** (« suivi »). Appelé par TDF Integration (port d'envoi SOAP BizTalk `poInscrireSuiviFich`, à l'intérieur de la boucle de réessai `loopResilience`). **Configuration de réessai du port d'envoi** (vérifié dans les fichiers de liaison de tous les 9 environnements) : transport primaire `RetryCount=3`, `RetryInterval=5` min; transport secondaire `RetryCount=1000`, `RetryInterval=60` min (~41 jours de réessais potentiels). Reçoit les données combinées fichier+suivi (après que la map BizTalk `mapInscrSuiviFich` fusionne le message fichier avec le message de suivi). Persiste dans la **base de données Oracle TDF** (vérifié dans le code source) : **tables de suivi** — `TDF_V_JOURN_ECHG_FICH` (journal des échanges), `TDF_V_MSG_ECHG_FICH` (suivi des messages), `TDF_V_FICH_ECHG` (suivi des fichiers), `TDF_V_STA_FICH_ECHG` (statut des fichiers); **contenu du fichier** — stocke conditionnellement le contenu du fichier décodé en Base64 dans la colonne BLOB `VAL_CONT_FICH_ENTRE` de `TDF_V_FICH_ECHG_ENTRE` (contrôlé par l'indicateur `IndValContFichEntre`). Méthode `InscrireSuiviFichCorln` pour les échanges complets; `InscrireSuiviFichAbandon` pour les échanges abandonnés (l'étape 3 n'est jamais arrivée — nettoie aussi les fichiers côté serveur). Hébergé sur `srv-prod-seltrt01`. **Dans le dépôt en portée** — `TDF-OOA2-Serv-Tranf-Fich`. Voir sections 11.1 et fait confirmé \#7 (section 10.1). |

#### 1.3.2 Flux de bout en bout — Interaction étape par étape

> **Comment lire ce diagramme :** Le flux est organisé par **étape** (Étape 1, Étape 2, Étape 3), montrant toutes les interactions de composants au sein de chaque étape. Chaque flèche indique le **mécanisme de transport** (HTTP POST, proxy SOAP, appel .NET in-process), la **méthode source** et le **point de terminaison cible**. Cela élimine toute ambiguïté sur ce qui appelle quoi.
>
> **IMPORTANT — Les étapes du TDF Frontend ≠ les branches du TDF Integration.** L'étape 2 envoie 2 messages (vers 2 branches différentes) ; l'étape 3 envoie 1 message (vers 1 branche) ; l'étape 1 n'en envoie aucun. Le TDF Frontend n'appelle pas une branche directement — il y a une **frontière réseau** : le TDF Frontend envoie des requêtes HTTP/SOAP aux points de terminaison des adaptateurs BizTalk ; BizTalk publie dans le MessageBox ; le moteur d'abonnement achemine vers la bonne branche d'orchestration en utilisant `NoEchg`.

```
════════════════════════════════════════════════════════════════════════════════════
 STEP 1 — InitierEnvoi (Initiate Transmission)
════════════════════════════════════════════════════════════════════════════════════

  CH App                        HOA5 Frontend               TDF Frontend (base class)           TDF Integration (BizTalk)
  ══════                        ═════════════               ═════════════════════════           ═════════════════════════
    │                                │                              │
    │  HTTPS + Basic Auth            │                              │
    │  InitierEnvoi(ByRef _strXml,   │                              │
    │    ByRef _objEntete)           │                              │
    ├───────────────────────────────►│                              │
    │                                │  MyBase.InitierEnvoi()       │
    │                                ├─────────────────────────────►│                                  NOT INVOLVED
    │                                │  (in-process .NET call)      │                                  ────────────
    │                                │                              │                                  No message is
    │                                │                              │  1. Validate hospital ID + type   sent to BizTalk
    │                                │                              │  2. SOAP call → AGS service       in Step 1.
    │                                │                              │     (YRD1_RechrRegleAcces_ws,
    │                                │                              │      checks right #246)
    │                                │                              │  3. CreerNoEchange()
    │                                │                              │     → NoEchgFich = IdEntIntvnEchg
    │                                │                              │       + UTC YYYYMMDDHHmmssfff
    │                                │                              │  4. Save state → EnteteRAMQ
    │                                │                              │     .EtatEchgFich (Base64)
    │                                │                              │
    │                                │◄─────────────────────────────┤
    │◄───────────────────────────────┤
    │  Returns: exchange number      │
    │  (NoEchgFich in _strXmlSortie  │
    │   + updated EnteteRAMQ ByRef)  │
    │                                │
    │  CH App stores NoEchgFich and  │
    │  EnteteRAMQ for Steps 2 and 3  │


════════════════════════════════════════════════════════════════════════════════════
 STEP 2 — EnvoyerLotFichier (Send File Batch)
════════════════════════════════════════════════════════════════════════════════════

  CH App                        HOA5 Frontend               TDF Frontend (base class)           TDF Integration (BizTalk)
  ══════                        ═════════════               ═════════════════════════           ═════════════════════════
    │                                │                              │                                  │
    │  HTTPS + Basic Auth            │                              │                                  │
    │  EnvoyerLotFichier(            │                              │                                  │ Orchestration:
    │    _strXmlEntree,              │                              │                                  │ orcTrnsmFichEntrant
    │    _bytContFich(),             │                              │                                  │ (Parallel Activating
    │    ByRef _strXmlSortie,        │                              │                                  │  Convoy, 3 branches)
    │    ByRef _objEntete)           │                              │                                  │
    ├───────────────────────────────►│                              │                                  │
    │                                │  MyBase.EnvoyerLotFichier()  │                                  │
    │                                ├─────────────────────────────►│                                  │
    │                                │  (in-process .NET call)      │                                  │
    │                                │                              │                                  │
    │                                │                              │  (A) Validate entête,            │
    │                                │                              │      exchange#, credentials       │
    │                                │                              │                                  │
    │                                │                              │  (B) ═══ MESSAGE 1: FILE ══════  │
    │                                │                              │      EnvoyerMessageServCourtage() │
    │                                │                              │      HTTP POST (raw XML,          │
    │                                │                              │        no SOAP envelope)          │
    │                                │                              │      up to 5 retries              │
    │                                │                              │      ┌───────────────────────────►│
    │                                │                              │      │  Target: BTSHTTPReceive.dll│
    │                                │                              │      │  Payload: <ns0:Fichier     │── ► BRANCH 2
    │                                │                              │      │   NoEchg="12345"           │    (scopeFichMsg)
    │                                │                              │      │   IndExecOrcSpec="O">      │    rcvInfoFichMsg
    │                                │                              │      │   <FichMsg>                │    receives file msg
    │                                │                              │      │    <Contenu>[ZIP]</Contenu>│    gblnFichierPresent
    │                                │                              │      │   </FichMsg>               │      = True
    │                                │                              │      │  </ns0:Fichier>            │
    │                                │                              │      │                            │
    │                                │                              │                                  │
    │                                │                              │  (C) Recevoir() — polymorphic    │
    │                                │                              │      (HOA5: MessageFichierEntrant │
    │                                │                              │       CH → ZIP + XML validation)  │
    │                                │                              │      Builds SuiviMessage_DS +     │
    │                                │                              │        SuiviFichier_DS            │
    │                                │                              │      Sets Erreur="0" if OK,       │
    │                                │                              │            Erreur="1" if fail     │
    │                                │                              │      NOTE: file already in        │
    │                                │                              │      BizTalk BEFORE this runs     │
    │                                │                              │                                  │
    │                                │                              │  (D) ═══ MESSAGE 2: SUIVI ═════  │
    │                                │                              │      EnvoyerSuiviServCourtage()   │
    │                                │                              │      SOAP proxy                   │
    │                                │                              │      (TrnsmInfoSuiviEntrantBtWS   │
    │                                │                              │       .opRecptInfoSuivi)          │
    │                                │                              │      ┌───────────────────────────►│
    │                                │                              │      │  Target: .svc endpoint     │── ► BRANCH 3
    │                                │                              │      │  Payload: <Root            │    (scopeSuivi)
    │                                │                              │      │   NoEchg="12345"           │    rcvInfoSuivi
    │                                │                              │      │   Erreur="0|1">            │    receives suivi msg
    │                                │                              │      │   <SuiviFichier>...</>     │    gblnSuiviPresent
    │                                │                              │      │   <SuiviMessage>...</>     │      = True
    │                                │                              │      │  </Root>                   │
    │                                │                              │      │                            │
    │                                │                              │                                  │
    │                                │                              │  (E) ObtenirAccRecep()            │
    │                                │                              │      Generates ACK =              │
    │                                │                              │       IdEntIntvnEchg +            │
    │                                │                              │       local YYYYMMDDHHmmss        │
    │                                │                              │      Saves ACK in EnteteRAMQ      │
    │                                │                              │       .EtatEchgFich (Base64)      │
    │                                │                              │                                  │
    │                                │◄─────────────────────────────┤                                  │
    │◄───────────────────────────────┤                              │                                  │
    │  Returns: accusé de réception  │                              │                                  │
    │  (ACK in _strXmlSortie         │                              │                                  │
    │   + updated EnteteRAMQ ByRef)  │                              │                                  │
    │                                │                              │                                  │
    │  CH App stores ACK to pass     │                              │                                  │
    │  back in Step 3                │                              │                                  │


════════════════════════════════════════════════════════════════════════════════════
 STEP 3 — CorrellerEnvoyer (Confirm & Correlate)
════════════════════════════════════════════════════════════════════════════════════

  CH App                        HOA5 Frontend               TDF Frontend (base class)           TDF Integration (BizTalk)
  ══════                        ═════════════               ═════════════════════════           ═════════════════════════
    │                                │                              │                                  │
    │  HTTPS + Basic Auth            │                              │                                  │
    │  CorrellerEnvoyer(             │                              │                                  │
    │    _strXmlEntree,              │                              │                                  │
    │    ByRef _strXmlSortie,        │                              │                                  │
    │    ByRef _objEntete)           │                              │                                  │
    ├───────────────────────────────►│                              │                                  │
    │                                │  MyBase.CorrellerEnvoyer()   │                                  │
    │                                ├─────────────────────────────►│                                  │
    │                                │  (in-process .NET call)      │                                  │
    │                                │                              │                                  │
    │                                │                              │  (A) Parse ACK from XML input    │
    │                                │                              │                                  │
    │                                │                              │  (B) 6 validations:              │
    │                                │                              │      entête, exchange#, ACK       │
    │                                │                              │      matches context-stored ACK,  │
    │                                │                              │      entity ID, entity type,      │
    │                                │                              │      security credentials         │
    │                                │                              │                                  │
    │                                │                              │  (C) ═══ MESSAGE 3: CORRELATION  │
    │                                │                              │      EnvoyerCorrellerServCourtage │
    │                                │                              │      SOAP proxy                   │
    │                                │                              │      (TrnsmCorlnEntrantBtWS       │
    │                                │                              │       .opRecptInfoCorln)          │
    │                                │                              │      ┌───────────────────────────►│
    │                                │                              │      │  Target: .svc endpoint     │── ► BRANCH 1
    │                                │                              │      │  Payload: <Root            │    (scopeCorln)
    │                                │                              │      │   NoEchg="12345" />        │    rcvInfoCorln
    │                                │                              │      │  (only NoEchg — pure       │    receives corln msg
    │                                │                              │      │   confirmation signal)      │    gblnCorreler
    │                                │                              │      │                            │      = True
    │                                │                              │                                  │
    │                                │                              │  (D) ViderContexte()             │
    │                                │                              │      Clears all exchange state    │
    │                                │                              │                                  │
    │                                │◄─────────────────────────────┤                                  │
    │◄───────────────────────────────┤                              │                                  │
    │  Returns: final confirmation   │                              │                                  │


════════════════════════════════════════════════════════════════════════════════════
 AFTER ALL 3 STEPS — TDF Integration Decision + HOA5 Processing
════════════════════════════════════════════════════════════════════════════════════

  TDF Integration (BizTalk)            HOA5 Integration (BizTalk)          HOA5 Backend              Oracle / DepoApli
  ═════════════════════════            ══════════════════════════          ════════════              ═════════════════
    │                                        │                                │                          │
    │  All 3 branches complete               │                                │                          │
    │  (received or timed out)               │                                │                          │
    │                                        │                                │                          │
    │  Decision: decideTransaction           │                                │                          │
    │                                        │                                │                          │
    │  HAPPY PATH (all 3 TRUE):              │                                │                          │
    │  ├─ Map: mapInscrSuiviFich             │                                │                          │
    │  │  (merge file + suivi msgs)          │                                │                          │
    │  ├─ SOAP call (with retry loop)        │                                │                          │
    │  │  → OOA2_InscrireSuiviFich_Ws        │                                │                          │
    │  │    (persist tracking to TDF         │                                │                          │
    │  │     Oracle DB)                      │                                │                          │
    │  ├─ If Erreur="0" AND                  │                                │                          │
    │  │   IndExecOrcSpec="O":               │                                │                          │
    │  │   HTTP POST → MessageBox            │                                │                          │
    │  │   ┌────────────────────────────────►│                                │                          │
    │  │   │  (pub/sub routing)              │  Map: transform to             │                          │
    │  │   │                                 │   HOA5 Backend format          │                          │
    │  │   │                                 │  WCF-Custom call               │                          │
    │  │   │                                 │  (with credential bridge)      │                          │
    │  │   │                                 ├───────────────────────────────►│                          │
    │  │   │                                 │  EnregistrerDonneesTrnsmCH     │                          │
    │  │   │                                 │                                │  1. Save ZIP ────────────►│ DepoApli
    │  │   │                                 │                                │     (FileStream)          │ {NoEchg}.zip
    │  │   │                                 │                                │  2. Unzip                 │
    │  │   │                                 │                                │  3. XML → 3 DataSets      │
    │  │   │                                 │                                │  4. Oracle INSERT ────────►│ Oracle DB
    │  │   │                                 │                                │     (explicit transaction) │ 3 tables
    │  │   │                                 │                                │     COMMIT if row          │
    │  │   │                                 │                                │     count matches          │
    │  │   │                                 │◄───────────────────────────────┤                          │
    │  │   │                                 │  Response (CodRetou)           │                          │
    │  │   │                                 │  If ≠ "0": Suspend            │                          │
    │                                        │                                │                          │
    │  ABANDONED (file+suivi, NO corln):     │                                │                          │
    │  └─ SOAP: InscrireSuiviFichAbandon     │                                │                          │
    │                                        │                                │                          │
    │  PARTIAL (missing file or suivi):      │                                │                          │
    │  └─ FILE adapter → rejection share     │                                │                          │
    │                                        │                                │                          │
    │  Always: log to Windows Event Log      │                                │                          │
```

> **Clarification importante — Étapes ≠ Branches :**
> L'étape 2 envoie **2 messages** à BizTalk (fichier → Branche 2, suivi → Branche 3). L'étape 3 envoie **1 message** (corrélation → Branche 1). L'étape 1 n'en envoie **aucun**. Le TDF Integration possède 3 branches parce qu'il attend 3 messages, et non parce qu'il y a 3 étapes.
>
> **Clarification importante — mécanisme de répartition (et non des appels de méthode directs) :**
> Le TDF Frontend n'appelle pas la Branche 1 directement. La répartition fonctionne via l'infrastructure BizTalk :
> 1. Le TDF Frontend envoie une requête vers une **URL de point de terminaison d'adaptateur BizTalk** (voir le tableau ci-dessous pour identifier quel adaptateur)
> 2. L'adaptateur BizTalk reçoit la requête — **deux types d'adaptateurs différents** sont utilisés selon le message :
>    - **Adaptateur HTTP** (`BTSHTTPReceive.dll`) — reçoit **uniquement le message fichier**. Le TDF Frontend l'envoie sous forme de HTTP POST brut (`HttpClient.PostAsync()`, `Content-Type: application/x-www-form-urlencoded`). L'adaptateur HTTP retourne une réponse synchrone (`text/xml`) avec `ReturnCorrelationHandle` activé, que le Frontend lit en retour pour confirmer que BizTalk a bien reçu le fichier.
>    - **Adaptateur WCF-BasicHttp** (points de terminaison `.svc`) — reçoit les messages de **suivi** et de **corrélation**. Le TDF Frontend les envoie via des classes proxy SOAP générées (`SoapHttpClientRAMQ`, Document/Literal). Ce sont des messages de contrôle typés et légers (pas de charges utiles volumineuses), donc un appel d'opération SOAP standard est approprié.
> 3. BizTalk publie le message dans la base de données **MessageBox**
> 4. Le moteur d'abonnement BizTalk fait correspondre la valeur de `NoEchg` à la bonne instance d'orchestration
> 5. Le message est livré à la branche en attente
>
> **Pourquoi deux adaptateurs différents ?** Le message fichier est une charge utile binaire volumineuse (~1-20 Mo en base64) nécessitant un HTTP POST brut avec accusé de réception synchrone et des en-têtes HTTP personnalisés pour la propagation d'identité (`X-RAMQ-Nom-Ident`, `X-RAMQ-Type-Auth`). Les messages de suivi et de corrélation sont de petits messages de contrôle XML typés — BizTalk publie les ports de réception d'orchestration en tant que points de terminaison SOAP/WCF nativement, et les proxys SOAP fournissent un typage au niveau des opérations ainsi qu'une validation contractuelle. Voir la section 1.4.2 pour l'explication complète en 5 raisons.
>
> | Méthode du TDF Frontend | Transport | Adaptateur BizTalk | Patron d'URL du point de terminaison | Branche |
> |---|---|---|---|---|
> | `EnvoyerMessageServCourtage()` | HTTP POST (`HttpClient`) | **HTTP** (`BTSHTTPReceive.dll`) | `/TDFAPP/OO/BTSHTTPRECEIVE64_HTP/BTSHTTPReceive.dll` | Branche 2 (`poRecptInfoFichMsg`) |
> | `EnvoyerSuiviServCourtage()` | Proxy SOAP (`SoapHttpClientRAMQ`) | **WCF-BasicHttp** (`.svc`) | `.../OOA2_TrnsmFichEntrant_PoRecptInfoSuivi_svc/...svc` | Branche 3 (`poRecptInfoSuivi`) |
> | `EnvoyerCorrellerServCourtage()` | Proxy SOAP (`SoapHttpClientRAMQ`) | **WCF-BasicHttp** (`.svc`) | `.../OOA2_TrnsmFichEntrant_PoRecptInfoCorln_svc/...svc` | Branche 1 (`poRecptInfoCorln`) |
>
> **Source :** `Prod.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` (fichier de liaisons — types d'adaptateurs et URLs des points de terminaison), `DestinataireEntrant.svc.vb` (méthodes d'envoi et mécanismes de transport), `Constante.vb` (clés de configuration d'URL et constante de type de contenu).
>
> Pour les développeurs juniors — le point clé à retenir est que BizTalk utilise **différents types d'adaptateurs pour différents patrons de communication** : l'adaptateur HTTP pour l'ingestion binaire brute avec réponse synchrone, l'adaptateur WCF-BasicHttp pour les appels d'opération SOAP typés. C'est un patron BizTalk courant où le choix de l'adaptateur est dicté par la nature de la charge utile et le style d'interaction requis, et non par préférence.

#### 1.3.3 Détail de l'architecture physique (couche par couche)

> **Comment lire cette section :** Le flux étape par étape ci-dessus montre les INTERACTIONS. Les diagrammes de couches ci-dessous montrent la STRUCTURE INTERNE de chaque composant en détail.

```
╔══════════════════════════════════════════════════════════════════════════════════╗
║  LAYER 1 — CH App (Hospital Application, ~140 centers)                         ║
║                                                                                ║
║  Calls 3 WCF operations in sequence (same IDestinataireEntrant contract):      ║
║  1. InitierEnvoi()   → get exchange number                                     ║
║  2. EnvoyerLotFichier() → send compressed XML file                             ║
║  3. CorrellerEnvoyer()  → confirm receipt                                      ║
╚═══════════════════════════════════════╤══════════════════════════════════════════╝
                                        │
                          HTTPS + Basic Auth (transport-level security)
                          Target: HOA5 Frontend WCF endpoint (.svc)
                                        │
                                        ▼
╔══════════════════════════════════════════════════════════════════════════════════╗
║  LAYER 2 — HOA5 Frontend WCF Service (hosts TDF Frontend base class)            ║
║  Repos: MEC-AlimenterBanque (HOA5), TDF-OOA2-Serv-Tranf-Fich (TDF)            ║
║                                                                                ║
║  ┌──────────────────────────────────────────────────────────────────────────┐   ║
║  │ HOA5 Frontend — WCF Service (FichDestinataireEntrant.svc)               │   ║
║  │ Inherits from DestinataireEntrant (TDF Frontend base class)             │   ║
║  │                                                                        │   ║
║  │  ┌─ HOA5-specific (FichDestinataireEntrant) ──────────────────────┐     │   ║
║  │  │ • Overrides: MyBase.InitierEnvoi()                            │     │   ║
║  │  │ • Overrides: MyBase.EnvoyerLotFichier()                       │     │   ║
║  │  │ • Overrides: MyBase.CorrellerEnvoyer()                        │     │   ║
║  │  │ • Constructor injects MessageFichierEntrantCH strategy object  │     │   ║
║  │  │   into inherited field objMessageFichierEntrant (polymorphism) │     │   ║
║  │  │   → plugs HOA5 validation rules into generic TDF pipeline     │     │   ║
║  │  └───────────────────────────────────────────────────────────────┘     │   ║
║  │  ┌─ TDF Frontend (DestinataireEntrant base class) ──────────────────┐  │   ║
║  │  │ ALL protocol logic lives here (see Section 1.3.2)              │  │   ║
║  │  └────────────────────────────────────────────────────────────────┘  │   ║
║  └─────────────────────────────────────────────────────────────────────────┘   ║
╚═══════════════════════════════════════╤══════════════════════════════════════════╝
                                        │
                          3 messages sent by TDF Frontend (see Section 1.3.2)
                          HTTP POST (file) + SOAP (suivi) + SOAP (correlation)
                                        │
                                        ▼
╔══════════════════════════════════════════════════════════════════════════════════╗
║  LAYER 3 — TDF Integration (BizTalk Server, TDF team)                          ║
║  BizTalk application: TDF_OOA2 | Orchestration: orcTrnsmFichEntrant            ║
║  Repo: TDF-OOA2-Serv-Tranf-Fich                                               ║
║                                                                                ║
║  (See Section 1.3.2 for how TDF Frontend sends 3 messages here.)              ║
║                                                                                ║
║  ADAPTERS SUMMARY:                                                             ║
║  ┌──────────────────────────────────────────────────────────────────────────┐  ║
║  │ INBOUND (3 receive ports — how messages ENTER):                         │  ║
║  │ • HTTP Adapter (BTSHTTPReceive.dll) ← file message from TDF Frontend    │  ║
║  │ • WCF-BasicHttp Adapter (.svc)      ← suivi message from TDF Frontend   │  ║
║  │ • WCF-BasicHttp Adapter (.svc)      ← correlation from TDF Frontend     │  ║
║  │   All 3 also have FILE adapter backup for manual recovery/replay        │  ║
║  │                                                                         │  ║
║  │ OUTBOUND (4 send ports — how messages EXIT):                            │  ║
║  │ • SOAP Adapter → OOA2_InscrireSuiviFich_Ws (tracking persistence)       │  ║
║  │ • HTTP Adapter → BTSHTTPReceive.dll (routes file to HOA5 Integration)   │  ║
║  │ • FILE Adapter → rejection share (rejected file messages)               │  ║
║  │ • FILE Adapter → rejection share (rejected suivi messages)              │  ║
║  └──────────────────────────────────────────────────────────────────────────┘  ║
║                                                                                ║
║  ORCHESTRATION: Parallel Activating Convoy (BizTalk built-in pattern).         ║
║  The FIRST of the 3 messages to arrive creates the orchestration instance       ║
║  and establishes the correlation value (NoEchg). The other 2 branches then      ║
║  wait for their messages (or time out after 9 minutes).                         ║
║                                                                                ║
║  ┌─────────────────────────────────────────────────────────────────────────────┐║
║  │ scopeRecvrFichEntrant (10-min timeout, long-running transaction)            │║
║  │                                                                             │║
║  │ ┌─── parallActEtapeRecptFichEntrant (Parallel shape) ───────────────────┐  │║
║  │ │                                                                       │  │║
║  │ │ BRANCH 1 (scopeCorln)     BRANCH 2 (scopeFichMsg)  BRANCH 3          │  │║
║  │ │                                                     (scopeSuivi)      │  │║
║  │ │ FROM: Step 3              FROM: Step 2              FROM: Step 2      │  │║
║  │ │ (CorrellerEnvoyer)        (EnvoyerLotFichier)       (EnvoyerLotFich)  │  │║
║  │ │                                                                       │  │║
║  │ │ Receive shape:            Receive shape:            Receive shape:    │  │║
║  │ │ rcvInfoCorln              rcvInfoFichMsg             rcvInfoSuivi     │  │║
║  │ │                                                                       │  │║
║  │ │ BizTalk Adapter:          BizTalk Adapter:          BizTalk Adapter:  │  │║
║  │ │ WCF-BasicHttp (.svc)      HTTP (BTSHTTPReceive.dll) WCF-BasicHttp    │  │║
║  │ │                                                                       │  │║
║  │ │ Port: poRecptInfoCorln    Port: poRecptInfoFichMsg  Port: poRecptInfo │  │║
║  │ │                                                     Suivi             │  │║
║  │ │ Schema:                   Schema:                   Schema:           │  │║
║  │ │ schInfoCorlnFichEntrant   schInfoFichMsgFichEntrant schInfoSuiviFich  │  │║
║  │ │ (contains only NoEchg)    (contains file content)   Entrant           │  │║
║  │ │                                                     (contains both    │  │║
║  │ │ Timeout: 9 min            Timeout: 9 min            tracking DSs)     │  │║
║  │ │                                                     Timeout: 9 min    │  │║
║  │ │ On OK: Expression shape   On OK: Expression shape   On OK: Expr.     │  │║
║  │ │ gblnCorreler = True       gblnFichierPresent = True gblnSuiviPresent │  │║
║  │ │                                                     = True            │  │║
║  │ │ On timeout: Exception     On timeout: Exception     On timeout: Exc.  │  │║
║  │ │ handler catches           handler catches           handler catches   │  │║
║  │ │ TimeoutException →        TimeoutException →        TimeoutException  │  │║
║  │ │ Expression shape:         Expression shape:         → Expr. shape:    │  │║
║  │ │ gblnCorreler = False      gblnFichierPresent=False  gblnSuiviPresent │  │║
║  │ │ + Construct shape:        + Construct shape:        = False           │  │║
║  │ │ dummy <Root/> msg         dummy <Fichier/> msg      + Construct:      │  │║
║  │ │                                                     dummy <Root/> msg │  │║
║  │ └───────────────────────────────────────────────────────────────────────┘  │║
║  │                                                                             │║
║  │ AFTER ALL 3 BRANCHES COMPLETE (received or timed out):                      │║
║  │                                                                             │║
║  │   Decision shape: decideTransaction (3 rules)                               │║
║  │                                                                             │║
║  │   Rule 1 — HAPPY PATH (all 3 TRUE):                                        │║
║  │     • Construct shape: applies BizTalk Map mapInscrSuiviFich.btm            │║
║  │       (merges file msg + suivi msg → InscrireSuiviFichCorln request)        │║
║  │     • loopResilience (While loop): retries until SOAP call succeeds         │║
║  │       ├─ Send shape: calls OOA2_InscrireSuiviFich_Ws.InscrireSuiviFichCorln │║
║  │       │   via SOAP Adapter (port poInscrireSuiviFich)                       │║
║  │       ├─ Receive shape: receives SOAP response                              │║
║  │       ├─ On success: Expression shape → gblnAppelServiceOK = True (exit)    │║
║  │       └─ On exception: Expression shape (log) + Suspend shape (manual fix)  │║
║  │     • Decision shape: decideIndicExecOrcSpec                                │║
║  │       ├─ If Erreur="0" AND IndExecOrcSpec="O":                              │║
║  │       │   Construct shape → builds output message (ns0:ROOT)                │║
║  │       │   Send shape → HTTP Adapter → BTSHTTPReceive.dll                    │║
║  │       │   (publishes to MessageBox → HOA5 Integration subscribes)           │║
║  │       └─ Else: no send (tracking-only exchange or validation error)         │║
║  │                                                                             │║
║  │   Rule 2 — ABANDONED (file+suivi but NO correlation):                       │║
║  │     • BizTalk Map → InscrireSuiviFichAbandon request                        │║
║  │     • loopResilience → SOAP call: InscrireSuiviFichAbandon                  │║
║  │                                                                             │║
║  │   Rule 3 — PARTIAL (missing file or suivi):                                 │║
║  │     • Decision shape: decidePresenceEntrants                                │║
║  │       ├─ If file present: FILE Adapter → dump to rejection share            │║
║  │       └─ If suivi present: FILE Adapter → dump to rejection share           │║
║  │                                                                             │║
║  │   Always: Expression shape exprJournaliser → Windows Event Log              │║
║  └─────────────────────────────────────────────────────────────────────────────┘║
║                                                                                ║
║  On Rule 1 (happy path): routes file msg → BizTalk MessageBox → HOA5 Integr.  ║
╚═══════════════════════════════════════╤══════════════════════════════════════════╝
                                        │
                  BizTalk MessageBox publish/subscribe
                  (filter on BizTalk application MEC_HOA5)
                                        │
                                        ▼
╔══════════════════════════════════════════════════════════════════════════════════╗
║  LAYER 4 — HOA5 Integration (BizTalk Server, HOA5 team)                        ║
║  BizTalk application: MEC_HOA5 | Orchestration: OrcServTransmPrel              ║
║  Repo: MEC-AlimenterBanque                                                     ║
║                                                                                ║
║  Simple sequential orchestration (NOT parallel — unlike TDF Integration):      ║
║                                                                                ║
║  (1) Receive shape: rcvFichierTransmis (Activate=True, MessageBox binding)     ║
║      Port: poRcvFichierCH (one-way, direct-bound to MessageBox)                ║
║      Schema: msgTypeFichierCHSpec (TDF's file message format)                  ║
║                                                                                ║
║  (2) Construct shape: construireMessageServiceSvc                              ║
║      ├─ Transform shape: applies BizTalk Map transformerMsgFichCHToMsgSVC.btm  ║
║      │   Input:  SchFichCHSpec (TDF generic file schema)                       ║
║      │   Output: EnregistrerDonneesTrnsmCH (HOA5 Backend WCF request)          ║
║      └─ MessageAssignment shape: assignerMessage                               ║
║           Sets SOAP.ClientConnectionTimeout = 9,000,000 ms (~150 minutes)      ║
║           (overrides BizTalk proxy default to let HOA5 Backend process large   ║
║            files — the server's web.config timeout becomes the effective limit) ║
║                                                                                ║
║  (3) Send shape: sndMessageTraiterFichRequete                                  ║
║      Port: poSndFichierCH (request-response)                                  ║
║      Adapter: WCF-Custom (see explanation below)                               ║
║      Operation: EnregistrerDonneesTrnsmCH                                      ║
║      Target: HOA5 Backend (ServTransmPrel.svc)                                 ║
║                                                                                ║
║  (4) Receive shape: rcvMessageTraiterFichReponse (response from HOA5 Backend)  ║
║                                                                                ║
║  (5) VariableAssignment shape: RecupererCodeRetour                             ║
║      Extracts: strCodeRetour = response.strCodRetou                            ║
║                                                                                ║
║  (6) Decision shape: SiCodeRetour                                              ║
║      ├─ IF strCodeRetour ≠ "0" → Suspend shape (manual investigation)          ║
║      └─ ELSE → orchestration ends normally (success)                           ║
║                                                                                ║
║  ┌─ WHY WCF-CUSTOM ADAPTER (not WCF-BasicHttp)? ────────────────────────────┐ ║
║  │                                                                          │ ║
║  │  The standard BizTalk WCF-BasicHttp adapter has a FIXED set of config    │ ║
║  │  properties — it does NOT expose an EndpointBehaviorConfiguration        │ ║
║  │  property. This means you cannot register custom WCF behaviors.          │ ║
║  │                                                                          │ ║
║  │  The WCF-Custom adapter provides FULL extensibility:                     │ ║
║  │  • EndpointBehaviorConfiguration → custom behavior extensions            │ ║
║  │  • BindingConfiguration → full XML control over binding settings         │ ║
║  │  • BindingType → can use ANY WCF binding (not limited to basicHttp)      │ ║
║  │                                                                          │ ║
║  │  This migration from WCF-BasicHttp to WCF-Custom was done specifically   │ ║
║  │  to inject the TranfBasic2IntegBehaviorExtn custom endpoint behavior.    │ ║
║  │  Source evidence: the repo contains BOTH the original WCF-BasicHttp      │ ║
║  │  binding (ServTransmPrel.BindingInfo.xml — no custom behavior) and the   │ ║
║  │  WCF-Custom binding (ServTransmPrel_Custom.BindingInfo.xml — with        │ ║
║  │  EndpointBehaviorConfiguration). Production uses WCF-Custom.             │ ║
║  │                                                                          │ ║
║  │  Production WCF-Custom config (from binding XML):                        │ ║
║  │  • BindingType: basicHttpBinding                                         │ ║
║  │  • Security: TransportCredentialOnly, clientCredentialType = Windows     │ ║
║  │  • EndpointBehavior: <TranfBasic2IntegBehaviorExtn />                   │ ║
║  │  • SOAP Action: IServTransmPrel/EnregistrerDonneesTrnsmCH               │ ║
║  │  • Retry: 3 retries, 5-minute interval                                  │ ║
║  └──────────────────────────────────────────────────────────────────────────┘ ║
║                                                                                ║
║  ┌─ TranfBasic2IntegBehaviorExtn — CREDENTIAL BRIDGE ────────────────────────┐ ║
║  │                                                                          │ ║
║  │  Library: RAMQ.CO.COD3_V2InteropWsdl_cpo (GAC-deployed, machine.config)  │ ║
║  │  Source: NOT in any HOA5/TDF/AGS repo — lives in a separate CO (Commun)  │ ║
║  │          team security infrastructure repository (not cloned on disk).    │ ║
║  │                                                                          │ ║
║  │  Purpose: Acts as an authentication bridge between BizTalk and the       │ ║
║  │  HOA5 Backend WCF service.                                               │ ║
║  │                                                                          │ ║
║  │  The problem it solves:                                                   │ ║
║  │  • BizTalk outbound binding declares clientCredentialType = "Windows"    │ ║
║  │    (BizTalk send handler uses its Windows service account identity)       │ ║
║  │  • HOA5 Backend web.config declares clientCredentialType = "Basic"       │ ║
║  │  • Without this behavior extension, there is an auth protocol mismatch   │ ║
║  │                                                                          │ ║
║  │  What it does:                                                            │ ║
║  │  The behavior extension intercepts the outbound WCF request and          │ ║
║  │  transforms the BizTalk send handler's Windows/Kerberos identity into    │ ║
║  │  the expected authentication format for the target endpoint. This allows  │ ║
║  │  BizTalk to authenticate to HOA5 Backend using Windows identity while    │ ║
║  │  the backend expects Basic auth — transparently bridging the mismatch.   │ ║
║  │                                                                          │ ║
║  │  The same library also exposes TrnsfAliasWsdlExtn (WSDL alias +          │ ║
║  │  SSL transform behavior), registered in HOA5 Frontend/Backend            │ ║
║  │  Web.config for inbound service endpoints.                               │ ║
║  │                                                                          │ ║
║  │  Risk: HIGH — non-standard, GAC-deployed, source not in scope repos.     │ ║
║  │  Single point of failure for all BizTalk-to-Backend communication.       │ ║
║  │  No documentation, no unit tests, no source available for audit.         │ ║
║  └──────────────────────────────────────────────────────────────────────────┘ ║
╚═══════════════════════════════════════╤══════════════════════════════════════════╝
                                        │
                WCF-Custom adapter (basicHttpBinding, Windows auth)
                Target: http://srv-prod-seltrt01/MECAPP/.../ServTransmPrel.svc
                                        │
                                        ▼
╔══════════════════════════════════════════════════════════════════════════════════╗
║  LAYER 5 — HOA5 Backend (WCF endpoint, HOA5 team)                              ║
║  Class: ServTransmPrel → delegates to TraiterTransmPrel                        ║
║  Repo: MEC-AlimenterBanque                                                     ║
║                                                                                ║
║  EnregistrerDonneesTrnsmCH(_objParamEntree) — verified call order:             ║
║                                                                                ║
║  Step 1: SauvegarderFichierRecu()                                              ║
║          FileStream(FileMode.Create) → Write bytes to DepoApli UNC path        ║
║          File name: {NoEchgFich}.zip                                           ║
║          Path from appSetting "RprtDepoFich"                                   ║
║                     │                                                          ║
║  Step 2: ObtenirFichierXmlRecu() → DecompresserFichZip()                       ║
║          Extracts XML from ZIP, returns XmlDocument                            ║
║                     │                                                          ║
║  Step 3: RemplirInfosFichierXmlRecu()                                          ║
║          Parses XML into 3 typed DataSets:                                     ║
║          • MEC_TRANSM_PRELIM_ADMISSION (table: MEC.MEC_PRE_SEJ_HOSP)          ║
║          • MEC_TRANSM_PRELIM_SOIN_ALTRN (table: MEC.MEC_SEJ_SOIN_ALTRN)      ║
║          • MEC_TRANSM_PRELIM_SOIN_INTSF (table: MEC.MEC_PRE_SEJ_SOIN_INTSF)  ║
║                     │                                                          ║
║  Step 4: InsererDonneesFich()                                                  ║
║          OracleOdp.OuvrirCnn() → DebuterTransaction()                          ║
║          Dynamic SQL INSERT (from Constantes.vb, NOT stored procedures)        ║
║          COMMIT only if: inserted rows = total rows across all 3 DataSets      ║
║          On failure: no explicit rollback — implicit rollback on cnn.Close()   ║
╚═══════════════════════════════════════╤══════════════════════════════════════════╝
                                        │
   ┌────────────────────────────────────┼─────────────────────────────────────┐
   │                                    │                                     │
   ▼                                    ▼                                     │
╔══════════════════════╗  ╔════════════════════════════════════════════╗       │
║ LAYER 6a — DepoApli  ║  ║ LAYER 6b — Oracle operational DB           ║       │
║ SMB file share       ║  ║ Connection: ODP.NET via UDL file            ║       │
║                      ║  ║ (MEC_ORA.UDL on HOA5 Backend host)          ║       │
║ {NoEchgFich}.zip     ║  ║                                             ║       │
║ Written FIRST (Step1)║  ║ Written LAST (Step 4): 3 INSERT statements  ║       │
║ NO rollback possible ║  ║ within explicit Oracle transaction          ║       │
╚══════════════════════╝  ╚═════════════════════════════════════════════╝       │
```

> **Pour les développeurs juniors — ce que signifie un point de terminaison SOAP de réception publié par BizTalk :** Lorsque BizTalk crée une orchestration avec des ports de réception (comme `poRecptInfoSuivi`), il peut automatiquement publier un point de terminaison de service web WCF (fichier `.svc`) que les applications externes peuvent appeler. Le TDF Frontend n'appelle pas directement les composants internes de BizTalk — il appelle un service web SOAP standard que BizTalk a publié. Les classes proxy SOAP (`TrnsmInfoSuiviEntrantBtWS`, `TrnsmCorlnEntrantBtWS`) sont auto-générées à partir du WSDL de ces points de terminaison publiés par BizTalk — les classes proxy utilisent le style ASMX `SoapHttpClientProtocol` (créées via « Add Web Reference »), mais les **points de terminaison côté serveur sont WCF-BasicHttp** (`.svc`), et non ASMX. Lorsque le TDF Frontend appelle `wsTrnsmInfoSuivi.opRecptInfoSuivi(dsSuiviFichBtWS)`, il envoie une requête SOAP HTTP à ce point de terminaison `.svc` publié, que BizTalk reçoit et achemine vers l'orchestration.

> **Pour les développeurs juniors — ce que signifie Parallel Activating Convoy :** Il s'agit d'un patron de conception BizTalk spécifique. Normalement, une orchestration BizTalk démarre lorsqu'UN message spécifique arrive (réception activatrice unique). Dans le patron **convoy**, l'orchestration peut être démarrée par N'IMPORTE LEQUEL de plusieurs messages — celui qui arrive en premier. Dans TDF Integration, n'importe lequel des 3 messages (fichier, suivi, corrélation) peut être le premier à arriver et créer l'instance d'orchestration. La shape Parallel attend ensuite les messages restants. Il s'agit d'une capacité intégrée de BizTalk, pas de code personnalisé — le moteur d'abonnement de BizTalk gère le routage des 2e et 3e messages vers la bonne instance d'orchestration en utilisant la valeur de corrélation `NoEchg`.

#### 1.3.4 Sécurité et identité

- **Sécurité du transport :** HTTPS entre le CH App et le point de terminaison WCF du HOA5 Frontend.
- **Authentification :** Authentification basique (Basic Authentication) sur le point de terminaison WCF du HOA5 Frontend (état actuel — **interface verrouillée**, hypothèse du projet : le CH App ne change pas).
- **Identité :** Les utilisateurs des centres hospitaliers sont gérés dans l'**Active Directory de la RAMQ**.
- **Codes de réponse HTTP :** `202 Accepted` (traitement asynchrone) ou `200 OK` (synchrone).

#### 1.3.5 Observations clés

- Le **HOA5 Frontend** est le service WCF (classe `FichDestinataireEntrant`, `InstanceContextMode.PerCall`) qui **héberge le TDF Frontend** par héritage de classe — il hérite de la classe de base `DestinataireEntrant` où réside toute la logique de protocole. **Le contrat de service est verrouillé** — l'hypothèse du projet est de ne pas modifier le CH App ; par conséquent, l'interface du HOA5 Frontend ne doit pas changer.

  **Vérifié dans le code source — attributs du service WCF sur `FichDestinataireEntrant` :**


  ```vb
  <ServiceBehavior(ConcurrencyMode:=ConcurrencyMode.Single, _
                   InstanceContextMode:=InstanceContextMode.PerCall, _
                   Namespace:="http://www.ramq.gouv.qc.ca/OOA2_ServTranfMsgFich_svc/1")> _
  <AspNetCompatibilityRequirements(RequirementsMode:=AspNetCompatibilityRequirementsMode.Required)> _
  Public Class FichDestinataireEntrant
      Inherits DestinataireEntrant
  ```

  | Attribut | Valeur | Signification |
  |----------|--------|---------------|
  | `ConcurrencyMode.Single` | Un seul thread actif par instance à la fois | Pas de concurrence intra-instance — sécuritaire pour du code non thread-safe |
  | `InstanceContextMode.PerCall` | Nouvelle instance pour chaque appel WCF | Chacun des 3 appels du CH App (`InitierEnvoi`, `EnvoyerLotFichier`, `CorrellerEnvoyer`) obtient un objet `FichDestinataireEntrant` **neuf** — l'objet précédent est détruit |
  | `Namespace` | `http://www.ramq.gouv.qc.ca/OOA2_ServTranfMsgFich_svc/1` | Espace de noms SOAP/WSDL — correspond au contrat de service `IDestinataireEntrant` |
  | `AspNetCompatibilityRequirementsMode.Required` | Pipeline IIS/ASP.NET requis | Permet l'accès à `HttpContext.Current` pour l'état de session (`objContexteWS`), l'impersonation Windows et `Identity.Name` |

  > **Pour les développeurs juniors — ce que signifie héberge ici :** La classe `FichDestinataireEntrant` (HOA5 Frontend) **hérite** de `DestinataireEntrant` (TDF Frontend). Lorsque le runtime WCF crée une instance de `FichDestinataireEntrant` pour traiter une requête, cet objet contient tout le code des deux classes. Le code du TDF Frontend s'exécute **à l'intérieur** du processus du service WCF du HOA5 Frontend — il n'y a pas d'appel réseau, pas de service séparé. `MyBase.EnvoyerLotFichier()` est simplement un appel de méthode .NET à la classe de base dans le même objet.

  > **Pour les développeurs juniors — `InstanceContextMode.PerCall` :** En WCF, `PerCall` signifie que le serveur crée une **toute nouvelle instance de service pour chaque requête HTTP**. Donc, si le CH App effectue 3 appels (`InitierEnvoi`, `EnvoyerLotFichier`, `CorrellerEnvoyer`), chaque appel obtient un objet `FichDestinataireEntrant` neuf — l'objet précédent n'existe plus. Il n'y a **pas de session WCF**. Comment l'état survit-il entre les 3 appels ? Le numéro d'échange (`NoEchgFich`) et les données contextuelles (ACK, indicateurs d'état de l'échange) sont sérialisés dans `EnteteRAMQ.EtatEchgFich` sous forme de données **encodées en Base64** à l'aide de `ColctContxEchgFich` — une collection clé-valeur sérialisée via `SerialiseurBase64`. Puisque `EnteteRAMQ` est un paramètre `ByRef`, le `EtatEchgFich` mis à jour est retourné au CH App dans chaque réponse. Le CH App doit renvoyer le même objet `EnteteRAMQ` lors de l'appel suivant — c'est ainsi que l'état côté serveur transite par le client. **Note :** Le code source contient des appels `Session(...)` commentés — l'état de session ASP.NET était le mécanisme **d'origine**, mais il a été remplacé par la sérialisation Base64 de `EnteteRAMQ.EtatEchgFich`.

  > **`PerCall` vs `PerSession` — classe de base vs classe dérivée :** La classe de base du TDF Frontend (`DestinataireEntrant`) est déclarée avec `InstanceContextMode.PerSession` — WCF créerait normalement une seule instance par canal de session WCF. Cependant, la classe dérivée du HOA5 Frontend (`FichDestinataireEntrant`) re-déclare `[ServiceBehavior]` avec `InstanceContextMode.PerCall`. En WCF, l'attribut `[ServiceBehavior]` sur la **classe de service concrète** (la plus dérivée) a la priorité. Donc, à l'exécution, `FichDestinataireEntrant` fonctionne en mode `PerCall` — une instance par appel, indépendamment de ce que la classe de base déclare.

  ##### 1.3.5.1 Contrat de service WCF — `IDestinataireEntrant` (vérifié dans le code source)

  Le CH App appelle le service WCF HOA5 Frontend via ce contrat. Les 3 opérations sont définies par l'interface `IDestinataireEntrant` (TDF Frontend). Le HOA5 Frontend (`FichDestinataireEntrant`) implémente cette interface par héritage de `DestinataireEntrant`.

  ```vb
  <ServiceContract(SessionMode:=SessionMode.Allowed, _
                   Namespace:="http://www.ramq.gouv.qc.ca/OOA2_ServTranfMsgFich_svc/1")>
  Public Interface IDestinataireEntrant

      <OperationContract()>
      Sub InitierEnvoi(ByRef _strXmlSortie As String,
                       ByRef _objEntete As EnteteRAMQ)

      <OperationContract()>
      Sub EnvoyerLotFichier(ByVal _strXmlEntree As String,
                            ByVal _bytContFich() As Byte,
                            ByRef _strXmlSortie As String,
                            ByRef _objEntete As EnteteRAMQ)

      <OperationContract()>
      Sub CorrellerEnvoyer(ByVal _strXmlEntree As String,
                           ByRef _strXmlSortie As String,
                           ByRef _objEntete As EnteteRAMQ)
  End Interface
  ```

  | Attribut | Signification |
  |----------|---------------|
  | `SessionMode.Allowed` | La session WCF est autorisée mais non requise — c'est le binding qui décide. En pratique, HOA5 utilise `PerCall`, donc il n'y a pas de session WCF. |

  > **Pour les développeurs juniors — direction des paramètres :**
  > - `ByVal` = entrée seulement (le client envoie, le serveur lit)
  > - `ByRef` = entrée ET sortie (le serveur peut modifier et retourner la valeur mise à jour au client)
  > - `_strXmlSortie` (`ByRef String`) = le serveur écrit le XML de résultat dans ce paramètre
  > - `_objEntete` (`ByRef EnteteRAMQ`) = le serveur lit l'identité du CH à partir de celui-ci ET y écrit le `NoEchgFich` (numéro d'échange) à l'étape 1

  ##### 1.3.5.2 `EnteteRAMQ` — Objet de contexte d'échange (vérifié dans le code source)

  `EnteteRAMQ` est un objet `DataContract` qui circule en tant que **paramètre `ByRef`** dans le corps SOAP (PAS un en-tête SOAP). Il transporte l'identité du CH et le numéro d'échange à travers les 3 étapes. Le `Inherits SoapHeader` commenté dans le code source confirme qu'il s'agissait à l'origine d'un en-tête SOAP ASMX — converti en DataContract régulier lors de la migration WCF sous VS2010.

  ```vb
  <DataContract(Namespace:="http://www.ramq.gouv.qc.ca/OOA2_ServTranfMsgFich_svc/1")>
  Public Class EnteteRAMQ
      'Inherits SoapHeader    ← COMMENTED OUT — trace of the old ASMX version
  ```

  | Propriété | Ordre DataMember | Type | Rôle |
  |-----------|-----------------|------|------|
  | `IdEntIntvnEchg` | 0 | `String` | Code du centre hospitalier (identifiant du CH — l'entité participant à l'échange) |
  | `NoEchgFich` | 1 | `String` | **Numéro d'échange** — créé par TDF à l'étape 1 (`InitierEnvoi`), retourné au CH App, puis repassé aux étapes 2 et 3. C'est le **jeton de corrélation** utilisé par BizTalk pour associer les 3 messages. |
  | `TypEntIntvnEchg` | 2 | `String` | Code du type d'entité (p. ex., type d'hôpital) |
  | `EtatEchgFich` | 3 | `String` | État de l'échange, encodé en Base64 |

  > **Comment `EnteteRAMQ` transporte l'état à travers les 3 étapes PerCall :**
  > 1. **Étape 1 (`InitierEnvoi`) :** Le CH App envoie `EnteteRAMQ` avec `IdEntIntvnEchg` et `TypEntIntvnEchg` remplis. Le serveur crée `NoEchgFich` via `CreerNoEchange()` (`IdEntIntvnEchg` + horodatage UTC `YYYYMMDDHHmmssfff`) et l'écrit dans `_objEntete.NoEchgFich`. Le serveur sérialise également l'état contextuel (numéro d'échange, indicateurs) dans `_objEntete.EtatEchgFich` en Base64 via `ColctContxEchgFich`. Le CH App reçoit l'`EnteteRAMQ` mis à jour (grâce au `ByRef`).
  > 2. **Étape 2 (`EnvoyerLotFichier`) :** Le CH App renvoie le même `EnteteRAMQ` avec `NoEchgFich` et `EtatEchgFich` de l'étape 1. Le serveur désérialise `EtatEchgFich` pour valider l'état de l'échange, puis ajoute de nouvelles données de contexte (ACK, indicateur d'état de l'échange) et re-sérialise.
  > 3. **Étape 3 (`CorrellerEnvoyer`) :** Même patron — le CH App passe `EnteteRAMQ` avec `EtatEchgFich`. Le serveur désérialise, valide l'ACK, envoie la corrélation à TDF Integration (BizTalk), puis efface le contexte via `ViderContexte()`.
  >
  > **Mécanisme d'état (vérifié dans le code source) :** `objContexteWS.Sauvegarder()` appelle `ColctContxEchgFich.AjouterContx()`, qui désérialise `EnteteRAMQ.EtatEchgFich` depuis le Base64 vers une `List(Of ContxEchgFich)`, ajoute la paire clé-valeur, et re-sérialise en Base64. Les appels `Session(...)` sont **commentés** dans le code source — l'état de session ASP.NET était le mécanisme original mais a été remplacé par cette approche d'état côté client.
  >
  > **Observation clé :** Le patron `ByRef EnteteRAMQ` signifie que le CH App transporte l'état côté serveur à travers les 3 instances PerCall. Le `EtatEchgFich` encodé en Base64 agit comme un jeton d'état opaque — le CH App ne peut ni le lire ni le modifier, seulement le retransmettre. Il s'agit d'un patron de gestion d'état côté client qui évite le stockage de session côté serveur.

  ##### 1.3.5.3 Méthodes substituées de `FichDestinataireEntrant` — Inventaire complet (vérifié dans le code source)

  `FichDestinataireEntrant` (HOA5 Frontend) substitue **exactement 4 méthodes** de la classe de base. 3 sont de la pure délégation (les opérations WCF), 1 contient de la logique spécifique à HOA5 (journalisation des erreurs).

  **Constructeur** — lit la configuration spécifique à HOA5 et injecte l'objet de stratégie :

  ```vb
  Public Sub New()
      Me.strChemConfg = ConfigurationManager.AppSettings.Get(Constantes.ChemConfg)
      Me.strCasUtil   = ConfigurationManager.AppSettings.Get(Constantes.CasUtil)

      Dim objMsgFichEntrantInstc As New MessageFichierEntrantCH
      objMsgFichEntrantInstc.ChemConfg = Me.strChemConfg
      objMsgFichEntrantInstc.CasUtil   = Me.strCasUtil

      Me.objMessageFichierEntrant = objMsgFichEntrantInstc   ' ← strategy injection
  End Sub
  ```

  > **Ce que fait le constructeur :** Lit deux appSettings du `web.config` (`ChemConfg` = chemin du fichier de configuration, `CasUtil` = identifiant du cas d'utilisation), crée une instance de `MessageFichierEntrantCH` (stratégie de validation spécifique à HOA5), la configure et l'assigne au champ hérité `objMessageFichierEntrant`. C'est le **seul point** où le comportement spécifique à HOA5 est injecté dans le pipeline générique du protocole TDF.

  **Substitution 1 — `InitierEnvoi` (étape WCF 1 : Initier la transmission) :**

  ```vb
  Public Overrides Sub InitierEnvoi(ByRef _strXmlSortie As String,
                                    ByRef _objEntete As EnteteRAMQ)
      MyBase.InitierEnvoi(_strXmlSortie, _objEntete)
  End Sub
  ```

  Pure délégation — aucune logique spécifique à HOA5. Tout le travail est effectué par `DestinataireEntrant.InitierEnvoi()` (TDF Frontend).

  **Substitution 2 — `EnvoyerLotFichier` (étape WCF 2 : Envoyer le fichier) :**

  ```vb
  Public Overrides Sub EnvoyerLotFichier(ByVal _strXmlEntree As String,
                                          ByVal _bytContFich() As Byte,
                                          ByRef _strXmlSortie As String,
                                          ByRef _objEntete As EnteteRAMQ)
      MyBase.EnvoyerLotFichier(_strXmlEntree, _bytContFich, _strXmlSortie, _objEntete)
  End Sub
  ```

  Pure délégation — aucune logique spécifique à HOA5. Le paramètre `_bytContFich()` contient le fichier ZIP compressé sous forme de tableau d'octets.

  **Substitution 3 — `CorrellerEnvoyer` (étape WCF 3 : Confirmer et corréler) :**

  ```vb
  Public Overrides Sub CorrellerEnvoyer(ByVal _strXmlEntree As String,
                                         ByRef _strXmlSortie As String,
                                         ByRef _objEntete As EnteteRAMQ)
      MyBase.CorrellerEnvoyer(_strXmlEntree, _strXmlSortie, _objEntete)
  End Sub
  ```

  Pure délégation — aucune logique spécifique à HOA5.

  **Substitution 4 — `JournaliserErrTechn` (Journalisation des erreurs techniques — spécifique à HOA5) :**

  ```vb
  Protected Overrides Sub JournaliserErrTechn(ByVal _exExcep As System.Exception)
      Dim objJournErrTech As New JournErrTech
      Dim objParamEntreErr As New ParamEntreErr

      With objParamEntreErr
          .CodAppli    = Constantes.CodAppli        ' "MEC" — HOA5 application code
          .DetlErr     = _exExcep.Message
          .IDUtil      = System.Environment.UserName
          .LibelMsgErr = _exExcep.Message
          .NivInter    = ParamEntreErr.EnumNivInter.NonPrisEnCharge
          .NoMsgErr    = Constantes.NoMsgErr         ' HOA5-specific error number
          .NomUt       = Constantes.CodUnitTach      ' HOA5-specific unit code
          .PileAppel   = _exExcep.StackTrace
          .CodSeverErr = ParamEntreErr.EnumCodSever.Severe
      End With

      objJournErrTech.JournaliserErrTech(objParamEntreErr)
  End Sub
  ```

  Il s'agit de la **seule substitution avec une logique spécifique à HOA5**. Elle remplace la journalisation d'erreurs générique de TDF par le composant partagé de journalisation d'erreurs techniques de la RAMQ (`COC2_JournErrTech_cpo`), enrichissant les erreurs avec les métadonnées HOA5 (`CodAppli = "MEC"`, `CodUnitTach`, `NoMsgErr`).

  | Substitution | Visibilité | Logique personnalisée ? | Appelle MyBase ? | Objectif |
  |--------------|-----------|------------------------|-----------------|----------|
  | `InitierEnvoi` | Public | Non — pure délégation | Oui | Étape WCF 1 : Initier la transmission |
  | `EnvoyerLotFichier` | Public | Non — pure délégation | Oui | Étape WCF 2 : Envoyer le fichier compressé |
  | `CorrellerEnvoyer` | Public | Non — pure délégation | Oui | Étape WCF 3 : Confirmer et corréler |
  | `JournaliserErrTechn` | Protected | **Oui** — spécifique à HOA5 | Non | Journalisation des erreurs techniques avec métadonnées HOA5 (appelée par TDF lors de toute exception non gérée) |

  > **Note concernant les anciennes signatures commentées (migration VS2010) :** Le code source contient des versions commentées des 3 opérations WCF avec des **signatures plus simples** (sans paramètre `_objEntete`). Il s'agissait des **signatures ASMX originales** où `EnteteRAMQ` était passé en tant qu'en-tête SOAP (`Inherits SoapHeader`). Lors de la migration VS2010 d'ASMX vers WCF, `EnteteRAMQ` a été converti d'un en-tête SOAP à un paramètre `ByRef DataContract` régulier dans le corps de la méthode. Les anciennes signatures ont été conservées en commentaires pour la traçabilité. Il s'agit d'un **changement de contrat non rétrocompatible** — le WSDL du CH App a été régénéré à ce moment-là.

- Le **TDF Frontend** est la classe de base `DestinataireEntrant` **hébergée à l'intérieur du service WCF HOA5 Frontend**. Son projet (`OOA2_ServTranfMsgFich_svc.vbproj`) est **générique et réutilisable dans tous les flux PPP/SFU** (HOA5, HOA1, ...). Il implémente l'interface `IDestinataireEntrant` agnostique PPP/SFU (3 opérations : `InitierEnvoi`, `EnvoyerLotFichier`, `CorrellerEnvoyer`). Il authentifie l'utilisateur du CH, valide les règles d'accès (appelle `YRD1_RechrRegleAcces_ws`), gère la création du numéro d'échange (`CreerNoEchange()` — `IdEntIntvnEchg` + horodatage UTC `YYYYMMDDHHmmssfff`), le transfert de fichier vers TDF Integration (BizTalk) via `HttpClient.PostAsync` → `BTSHTTPReceive.dll`, la création d'enregistrements de suivi (DataSets `SuiviMessage_DS` + `SuiviFichier_DS` pour la piste d'audit), et la génération d'ACK (`GenererAccRecep()` — `IdEntIntvnEchg` + horodatage local `YYYYMMDDHHmmss`, un jeton de handshake retourné au CH App et validé à l'étape 3). Le comportement spécifique à HOA5 est injecté via l'objet `MessageFichierEntrantCH` (Strategy Pattern). Le TDF Frontend communique avec TDF Integration en utilisant le même protocole TDF agnostique PPP/SFU, quel que soit le flux.

  > **Confirmé dans le code source : type de projet et modèle d'hébergement du TDF Frontend.** Le fichier projet (`RAMQ.TDF.OOA2_ServTranfMsgFich_svc.vbproj`) est une **application Web WCF** (les ProjectTypeGuids incluent `{349c5851-65df-11da-9384-00065b846f21}` = application Web, avec des fichiers de point de terminaison `.svc`). Dans le contexte HOA5, le service WCF HOA5 Frontend **héberge** le TDF Frontend en référençant son assembly DLL compilée (`RAMQ.OO.OOA2_ServTranfMsgFich_svc.dll` — HintPath confirmé dans `HOA5_RecvrTrnsmPrelimCH_svc.vbproj`) et en héritant de la classe de base `DestinataireEntrant`. Le fichier projet ne référence **aucun assembly `Microsoft.BizTalk.*`** : toute communication avec BizTalk se fait par HTTP (`System.Net.Http.HttpClient` POST vers `BTSHTTPReceive.dll`).
- Le **TDF Integration** (BizTalk Server, `OOA2_TrnsmFichEntrant_bt`) est un projet BizTalk partagé. Confirmé dans le code source à partir de `orcTrnsmFichEntrant.odx` : l'orchestration s'exécute en tant que **transaction atomique de longue durée** (scope externe de 10 minutes) et utilise un **shape Parallel** (`parallActEtapeRecptFichEntrant`) avec 3 branches concurrentes, chacune attendant jusqu'à 9 minutes :
  1. **Branche de corrélation** (`scopeCorln`) : attend le message d'information de corrélation (`poRecptInfoCorln`) envoyé par le TDF Frontend à l'étape 3 (`CorrellerEnvoyer`). Met `gblnCorreler = true` à l'arrivée ; met `false` en cas de délai d'expiration.
  2. **Branche de fichier** (`scopeFichMsg`) : attend le message de fichier (`poRecptInfoFichMsg`) envoyé par le TDF Frontend à l'étape 2 via `BTSHTTPReceive.dll`. Met `gblnFichierPresent = true` à l'arrivée ; met `false` en cas de délai d'expiration.
  3. **Branche de suivi** (`scopeSuivi`) : attend les données de suivi (`poRecptInfoSuivi`) également envoyées lors de l'étape 2. Met `gblnSuiviPresent = true` à l'arrivée ; met `false` en cas de délai d'expiration.

  Après que les 3 branches sont terminées (ou ont expiré), un shape **Decision** évalue : si les 3 indicateurs sont `true`, l'orchestration applique la map `mapInscrSuiviFich` pour combiner les données de fichier et de suivi, puis appelle `OOA2_InscrireSuiviFich_Ws.InscrireSuiviFichCorln` à l'intérieur d'une **boucle de réessai** (`loopResilience`) jusqu'à ce que l'appel réussisse. Elle achemine ensuite le fichier vers HOA5 Integration via le MessageBox BizTalk.
- Le **HOA5 Integration** (BizTalk Server, `HOA5_ServTransmPrel_bt`) est le projet BizTalk spécifique à HOA5. Confirmé dans le code source à partir de `OrcServTransmPrel.odx` : l'orchestration (1) reçoit le message de fichier du MessageBox BizTalk (`poRcvFichierCH`, unidirectionnel, liaison directe), (2) **applique la map `transformerMsgFichCHToMsgSVC`** à l'intérieur d'un shape Construct — convertissant le format de message CH de TDF (`SchFichCHSpec`) vers le format de requête WCF du HOA5 Backend (`IServTransmPrel_EnregistrerDonneesTrnsmCH_InputMessage`) — et définit `SOAP.ClientConnectionTimeout = 9000000` pour remplacer le délai d'expiration du proxy BizTalk, (3) **envoie la requête transformée** au HOA5 Backend via le port d'envoi WCF `poSndFichierCH` (adaptateur WCF-Custom, opération `EnregistrerDonneesTrnsmCH`), (4) reçoit la réponse et extrait `strCodRetou` (code de retour), puis (5) **suspend l'orchestration** si le code de retour n'est pas `"0"`. **La transformation par map est appliquée avant l'appel WCF** — confirmé par l'ordre des shapes de l'orchestration dans l'ODX.
- Le **HOA5 Backend** (point de terminaison WCF, `HOA5_ServTransmPrel_svc`) traite la transmission en séquence : (1) sauvegarde le fichier ZIP reçu dans DepoApli (chemin UNC), (2) décompresse le fichier et extrait le XML, (3) charge le XML dans des DataSets (3 entités de données : ADMISSION, SOIN_ALTRN, SOIN_INTSF), (4) insère les données dans Oracle via `ODP.NET` (fichier de connexion UDL). Ce composant **peut être modifié** s'il y a une justification solide et fondée sur des preuves — approuvée par le comité d'architecture.
- L'authentification Basic Authentication sur HTTPS est le mécanisme de sécurité **actuel** (CH App → HOA5 Frontend). L'**interface du HOA5 Frontend est verrouillée** conformément à l'hypothèse du projet — le CH App ne change pas.

#### 1.3.6 HOA5 Frontend et TDF Frontend — Hiérarchie de classes et pseudo-code

> **Source :** Vérifié dans le code à partir de `IDestinataireEntrant.vb`, `Destinataire.vb`, `DestinataireEntrant.svc.vb` (TDF Frontend), `FichDestinataireEntrant.svc.vb` (HOA5 Frontend) et `MessageFichierEntrantCH.vb` (HOA5 CPO).

Cette sous-section explique exactement comment le HOA5 Frontend et le TDF Frontend sont liés l'un à l'autre au niveau du code, à l'aide de pseudo-code simplifié dérivé du code source. Il s'agit d'un contexte clé pour quiconque travaille sur la migration.

**Hiérarchie de classes (chaîne d'héritage) :**

```
IDestinataireEntrant              ← WCF service contract (interface, 3 operations)
    │
Destinataire                      ← Abstract base class (session context, suivi DataSets, security)
    │                                Repo: TDF-OOA2-Serv-Tranf-Fich
    │
    ├── DestinataireEntrant       ← TDF Frontend base class (generic 3-step protocol logic)
    │       │                        Repo: TDF-OOA2-Serv-Tranf-Fich
    │       │                        [ServiceBehavior(InstanceContextMode = PerSession)]
    │       │
    │       └── FichDestinataireEntrant  ← HOA5 Frontend WCF service (hosts TDF Frontend, overrides PerCall)
    │                                       Repo: MEC-AlimenterBanque
    │                                       [ServiceBehavior(InstanceContextMode = PerCall)]
    │                                       Overrides: InitierEnvoi, EnvoyerLotFichier, CorrellerEnvoyer (pure delegation)
    │                                                  JournaliserErrTechn (HOA5-specific error logging)
    │
MessageFichierEntrant             ← Abstract strategy class (file processing pipeline — Template Method pattern)
    │                                Repo: TDF-OOA2-Serv-Tranf-Fich
    │                                Hook methods: TraitSpecPreCreatSuiviFich(), TraitSpecPreInscrFichSuivi() (empty default)
    │
    └── MessageFichierEntrantCH   ← HOA5-specific validation strategy
                                     Repo: MEC-AlimenterBanque
                                     Overrides: TraitSpecPreCreatSuiviFich (ZIP-level validation)
                                                TraitSpecPreInscrFichSuivi (per-file XML validation)
```

**Pseudo-code — Comment un appel du CH App traverse la hiérarchie de classes :**

```
─── WCF request arrives from CH App (e.g., EnvoyerLotFichier) ───

  WCF runtime creates: FichDestinataireEntrant instance (PerCall = new object each call)

  FichDestinataireEntrant constructor:
      │  reads config: ChemConfgHOA5, CasUtilFichCHEntrant
      │  creates: MessageFichierEntrantCH (HOA5-specific strategy)
      │  assigns: Me.objMessageFichierEntrant = MessageFichierEntrantCH
      │  PURPOSE: plugs HOA5 validation rules (ZIP structure, XML XSD schema)
      │           into the generic TDF Recevoir() pipeline via polymorphism
      ▼

  FichDestinataireEntrant.EnvoyerLotFichier(...):
      │  MyBase.EnvoyerLotFichier(...)  ← one-liner, delegates everything upward
      ▼

  DestinataireEntrant.EnvoyerLotFichier(...):          ← [TDF Frontend — generic logic]
      │  1. Read EnteteRAMQ (ByRef parameter) → extract NoEchgFich, IdEntIntvnEchg
      │  2. Validate exchange state, credentials, access rules (AGS / right #246)
      │  3. Format XML message with Base64 file content
      │  4. HTTP POST file to TDF Integration (BizTalk) via BTSHTTPReceive.dll (up to 5 retries)
      │         → this publishes the FILE MESSAGE into BizTalk MessageBox
      │         → TDF Integration Branch 2 (scopeFichMsg) will receive it asynchronously
      │
      │  ┌─────────────────────────────────────────────────────────────────┐
      │  │ IMPORTANT — FILE IS SENT BEFORE VALIDATION (by design):       │
      │  │                                                                │
      │  │ The HTTP POST above (step 4) happens BEFORE Recevoir() below  │
      │  │ (step 5). This means the file is already in BizTalk's         │
      │  │ MessageBox BEFORE ZIP/XML validation runs in step 5.c/5.d.    │
      │  │                                                                │
      │  │ WHY this is safe: BizTalk does NOT start processing the file  │
      │  │ until ALL 3 messages arrive (Parallel Activating Convoy).     │
      │  │ The suivi message sent after validation (step 6) carries the  │
      │  │ Erreur flag ("0"=OK, "1"=error). If validation fails, the    │
      │  │ orchestration's decideIndicExecOrcSpec checks Erreur and will │
      │  │ NOT route the file to HOA5 Integration.                       │
      │  │                                                                │
      │  │ WHY this ordering: if the HTTP POST to BizTalk fails (after   │
      │  │ 5 retries), the system STOPS immediately with an exception —  │
      │  │ no orphan tracking records are created. The send-then-track   │
      │  │ order ensures that tracking records only exist for files that  │
      │  │ successfully reached BizTalk.                                  │
      │  │                                                                │
      │  │ Source evidence (line 672 comment): "Si cette méthode est      │
      │  │ exécutée avec succès, on est certain que le message fichier    │
      │  │ est rendu au niveau de BizTalk."                               │
      │  └─────────────────────────────────────────────────────────────────┘
      │
      │  5. Call objMessageFichierEntrant.Recevoir(...)  ← POLYMORPHIC CALL
      │
      │     ┌─────────────────────────────────────────────────────────────────┐
      │     │ HOW THIS WORKS (for junior developers):                        │
      │     │                                                                │
      │     │ The field objMessageFichierEntrant is declared in TDF base     │
      │     │ class as type MessageFichierEntrant (abstract).                │
      │     │                                                                │
      │     │ But in the constructor of FichDestinataireEntrant (HOA5),      │
      │     │ it was assigned:                                               │
      │     │   Me.objMessageFichierEntrant = New MessageFichierEntrantCH()  │
      │     │                                                                │
      │     │ So when TDF code calls objMessageFichierEntrant.Recevoir(),    │
      │     │ .NET polymorphism routes to MessageFichierEntrantCH.Recevoir() │
      │     │ — the HOA5-specific implementation. TDF code never imports     │
      │     │ or references MessageFichierEntrantCH directly.                │
      │     └─────────────────────────────────────────────────────────────────┘
      │
      │         → MessageFichierEntrantCH.Recevoir(...)  ← HOA5 implementation runs
      │         → base Recevoir() pipeline (Template Method pattern):
      │             NOTE: Recevoir() is NOT a gatekeeper that controls BizTalk processing.
      │             The file is ALREADY in BizTalk (sent in step 4 above).
      │             Recevoir() creates the tracking/suivi DataSets. If validation fails,
      │             the suivi message sent in step 6 will carry Erreur="1", telling
      │             BizTalk NOT to route the file to HOA5 Integration.
      │             a. Associate UDL connection
      │             b. Initialize SuiviMessage row (exchange tracking)
      │             c. Call HOA5 TraitSpecPreCreatSuiviFich() ← ZIP validation:
      │                  - Exactly 1 file in ZIP? (error 21379 / 21382)
      │                  - ZIP name matches MEC_EEEEEEEE_YYYYMMDDHHMMSS.ZIP? (error 21376/21381)
      │                  - Establishment ID in filename = IdEntIntvnEchg? (error 21377)
      │             d. For each file in ZIP:
      │                  Call HOA5 TraitSpecPreInscrFichSuivi() ← XML validation:
      │                      - XML name matches MEC_EEEEEEEE_YYYYMMDDHHMMSS.XML? (error 21378)
      │                      - XML content non-empty? (error 21380)
      │                      - XML validates against XSD schema? (error 21380)
      │                  Build SuiviFichier row (file-level tracking)
      │         → builds SuiviMessage_DS + SuiviFichier_DS
      │
      │  6. TDF Frontend sends SUIVI MESSAGE to TDF Integration (BizTalk)
      │         Contains: SuiviMessage_DS (exchange-level audit) + SuiviFichier_DS (file-level audit)
      │         WHO calls: TDF Frontend (DestinataireEntrant.EnvoyerSuiviServCourtage())
      │         HOW: SOAP proxy class TrnsmInfoSuiviEntrantBtWS, method opRecptInfoSuivi()
      │         TARGET: BizTalk-published SOAP receive endpoint (port poRecptInfoSuivi)
      │         NOT: the CH App calling HOA5 Frontend — this is an INTERNAL server-to-server call
      │         → TDF Integration Branch 3 (scopeSuivi) receives it
      │
      │  7. Generate accusé de réception (ACK = IdEntIntvnEchg + local YYYYMMDDHHmmss),
      │         save in EnteteRAMQ.EtatEchgFich context (Base64)
      │  8. Return accusé de réception to CH App
      ▼

  WCF runtime destroys: FichDestinataireEntrant instance (PerCall)
```

> **Pour les développeurs juniors — ce que fait `MyBase` en VB.NET :**
> `MyBase` est le mot-clé VB.NET qui appelle la méthode de la **classe parente**. Quand HOA5 Frontend appelle `MyBase.EnvoyerLotFichier(...)`, cela signifie : « Je ne fais rien de plus — j'exécute simplement le code défini dans la classe parente `DestinataireEntrant`. » C'est ce que signifie dire « HOA5 Frontend héberge TDF Frontend » — **ses 3 méthodes** sont des appels simples d'une seule ligne vers `MyBase.*`, déléguant tout à la classe de base TDF Frontend. La logique du protocole en 3 étapes réside entièrement dans le TDF Frontend (`DestinataireEntrant`).

> **Point clé — Strategy Pattern pour la validation spécifique à HOA5 :**
> Le TDF Frontend est générique — il gère le protocole de transmission en 3 étapes pour n'importe quel système (HOA5, HOA1, HOB1, ...). Mais chaque système a besoin de **règles de validation différentes** pour les fichiers qu'il reçoit. La solution utilise le **Strategy Pattern** : le HOA5 Frontend injecte un objet `MessageFichierEntrantCH` (stratégie spécifique à HOA5) dans le champ hérité `objMessageFichierEntrant`. Durant l'étape 2, le TDF Frontend appelle `objMessageFichierEntrant.Recevoir(...)` — un appel polymorphique. À l'exécution, cela exécute les méthodes surchargées de `MessageFichierEntrantCH` (`TraitSpecPreCreatSuiviFich` pour la validation ZIP, `TraitSpecPreInscrFichSuivi` pour la validation XML). Le code TDF ne référence jamais directement les classes HOA5 — il ne connaît que le type abstrait de base `MessageFichierEntrant`.

> **`MessageFichierEntrantCH` — Méthodes surchargées spécifiques à HOA5 (vérifié dans le code source) :**
>
> `MessageFichierEntrantCH` (dépôt `MEC-AlimenterBanque`) hérite de `MessageFichierEntrant` (base TDF, dépôt `TDF-OOA2-Serv-Tranf-Fich`) et surcharge **2 méthodes « hook »** que le pipeline TDF `Recevoir()` appelle durant l'étape 2 (`EnvoyerLotFichier`). Ce sont les **seuls endroits où réside la logique de validation de fichiers spécifique à HOA5** :
>
> **Surcharge 1 — `TraitSpecPreCreatSuiviFich` (validation au niveau ZIP) :**
>
> ```vb
> ''' <summary>
> '''  Performs HOA5-specific validations on the ZIP file lot.
> ''' </summary>
> Protected Overrides Sub TraitSpecPreCreatSuiviFich(
>     ByVal _strNoEchgFich As String,       ' Exchange number
>     ByVal _strNoIntvn As String,           ' Hospital center identifier
>     ByVal _strTypEntIntvnEchg As String,   ' Entity type of the hospital
>     ByVal _strIdUtil As String,            ' Current user logon (e.g., AGS99999A)
>     ByVal _strNomFich As String,           ' ZIP file name
>     ByVal _bytContFich() As Byte,          ' ZIP file content as byte array
>     ByRef _objIdxListeFich As Array,       ' OUT: array of file names inside the ZIP
>     ByRef _objListeFich As Array,          ' OUT: 2D array — file names (dim 1) + contents (dim 2)
>     ByRef _dsSuiviMsg As SuiviMessage_DS,  ' IN/OUT: exchange-level tracking DataSet
>     ByRef _dsSuiviFich As SuiviFichier_DS) ' IN/OUT: file-level tracking DataSet
> ```
>
> **Appelée une fois par transmission** — avant la boucle fichier par fichier. Valide l'archive ZIP dans son ensemble :
> - Exactement 1 fichier dans le ZIP ? (erreur 21379 si vide, erreur 21382 si >1 fichier)
> - Le nom du fichier ZIP correspond au patron `MEC_EEEEEEEE_YYYYMMDDHHMMSS.ZIP` ? (erreur 21376 format incorrect, erreur 21381 mauvaise extension)
> - L'identifiant d'établissement (`EEEEEEEE`) dans le nom du fichier ZIP correspond à l'identité du CH (`IdEntIntvnEchg`) provenant de `EnteteRAMQ` ? (erreur 21377 en cas de non-concordance)
>
> **Surcharge 2 — `TraitSpecPreInscrFichSuivi` (validation de fichier individuel) :**
>
> ```vb
> ''' <summary>
> '''    Performs HOA5-specific validations on each file inside the ZIP.
> ''' </summary>
> Protected Overrides Sub TraitSpecPreInscrFichSuivi(
>     ByVal _strNoIntvn As String,           ' Hospital center identifier
>     ByVal _strTypEntIntvnEchg As String,   ' Entity type of the hospital
>     ByVal _strIdUtil As String,            ' Current user logon
>     ByVal _strNomFich As String,           ' XML file name (inside the ZIP)
>     ByVal _strIdFichEchg As String,        ' File exchange identifier
>     ByVal _bytContFich() As Byte,          ' File content as byte array
>     ByVal _strNoEchgFich As String,        ' Exchange number
>     ByVal _intIndex As Integer,            ' File index in the list
>     ByVal _strIdxListeFich As Array,       ' Array of all file names in the ZIP
>     ByVal _strListeFich As Array,          ' 2D array — file names + contents
>     ByRef _dsSuiviFich As SuiviFichier_DS, ' IN/OUT: file-level tracking DataSet
>     ByRef _dsSuiviMsg As SuiviMessage_DS,  ' IN/OUT: exchange-level tracking DataSet
>     ByRef _blnAjouFichSuivi As Boolean)    ' OUT: whether to add this file to tracking
> ```
>
> **Appelée une fois par fichier** à l'intérieur du ZIP — dans la boucle fichier par fichier. Valide chaque fichier XML extrait :
> - Le nom du fichier XML correspond au patron `MEC_EEEEEEEE_YYYYMMDDHHMMSS.XML` ? (erreur 21378 si format incorrect)
> - Le contenu XML est non vide ? (erreur 21380 si vide)
> - Le contenu XML est valide selon le schéma XSD HOA5 ? (erreur 21380 si invalide)
>
> | Méthode | Portée | Appelée | Objectif |
> |---------|--------|---------|----------|
> | `TraitSpecPreCreatSuiviFich` | ZIP entier | Une fois par transmission | Validation au niveau ZIP : nombre de fichiers, convention de nommage, concordance de l'identifiant d'établissement |
> | `TraitSpecPreInscrFichSuivi` | Chaque fichier du ZIP | Une fois par fichier | Validation au niveau fichier : nommage XML, contenu non vide, validation du schéma XSD |
>
> **Pour les développeurs juniors — comment TDF appelle ces méthodes :**
> La classe de base TDF `MessageFichierEntrant.Recevoir()` possède un pipeline de traitement fixe. À des points spécifiques de ce pipeline, elle appelle `TraitSpecPreCreatSuiviFich()` et `TraitSpecPreInscrFichSuivi()` — ce sont des méthodes `Protected Overridable` avec des **implémentations par défaut vides** dans la classe de base TDF. Chaque système (HOA5, HOA1, etc.) les surcharge avec ses propres règles de validation. Le code TDF ne voit que le type de base `MessageFichierEntrant` — il appelle la méthode, et le polymorphisme .NET achemine l'appel vers la surcharge correcte à l'exécution. C'est le patron de conception **Template Method** (le pipeline est fixe, les points d'extension sont personnalisables).

> **Ce que contiennent les DataSets de suivi (vérifié dans le code source à partir de `MessageFichierEntrant.Recevoir()`) :**
> Durant l'étape 2, le pipeline `Recevoir()` construit deux DataSets typés qui enregistrent ce qui s'est passé avec cet échange. Ce sont le **journal d'audit** de chaque transmission :
> - **`SuiviMessage_DS`** (suivi au niveau échange — 3 tables) :
>   - `TDF_V_JOURN_ECHG_FICH_DT` : une ligne par échange — enregistre `COD_APPLI` ("MEC"), `NO_ECHG_FICH`, `ID_ENT_INTVN_ECHG` (identifiant du CH), `TYP_ENT_INTVN_ECHG`, `ID_UTIL` (utilisateur), `TYP_ECHG_FICH`, `NO_ACC_RECEP` (numéro d'accusé de réception), `IND_CORLN_ACC_RECEP`, `NOM_FICH` (nom du fichier)
>   - `TDF_V_MSG_ECHG_FICH_DT` : lignes ajoutées lorsque la validation échoue — enregistre les codes d'erreur (`ID_MSG_RETRN`, `ID_FICH_ECHG`, `NO_OCC_MSG_ECHG`)
>   - `BANQ_DT` : chemin du fichier UDL Oracle pour la connexion à la base de données
> - **`SuiviFichier_DS`** (suivi au niveau fichier — 7 tables) :
>   - `TDF_V_FICH_ECHG_DT` : une ligne par fichier dans le ZIP — enregistre `ID_FICH_ECHG`, `NOM_FICH`, `NO_ECHG_FICH`, `IND_TRANF_FICH` (indicateur de transfert)
>   - `TDF_V_STA_FICH_ECHG_DT` : suivi du statut du fichier — `STA_FICH_ECHG`, `DAT_STA_FICH_ECHG`
>   - `TDF_V_FICH_ECHG_ENTRE_DT` : métadonnées du fichier entrant + contenu — `VAL_VAR_1..10_FICH_ENTRE` (métadonnées variables), `VAL_CONT_FICH_ENTRE` (contenu réel du fichier, stocké conditionnellement en BLOB), `IND_VAL_CONT_FICH`
>   - `TDF_V_FICH_ECHG_SORTI_DT` : variables du fichier sortant
>   - `TDF_V_ENT_INTVN_ECHG_DT` : information sur l'entité intervenante
>   - `INFO_SERVR_APPLI_DT` : chemin de l'application serveur
>   - `BANQ_DT` : chemin du fichier UDL Oracle
>
> Ces DataSets sont envoyés à TDF Integration (BizTalk) en tant que **message de suivi** (TDF Frontend → port BizTalk `poRecptInfoSuivi`, via proxy SOAP — PAS l'application CH appelant HOA5 Frontend). À l'intérieur de BizTalk, l'orchestration TDF Integration fusionne le message de fichier et le message de suivi (via la carte BizTalk `mapInscrSuiviFich`), puis appelle `OOA2_InscrireSuiviFich_Ws` qui persiste dans la **base de données Oracle TDF** : les métadonnées de suivi dans les tables `TDF_V_JOURN_ECHG_FICH`, `TDF_V_MSG_ECHG_FICH`, `TDF_V_FICH_ECHG`, `TDF_V_STA_FICH_ECHG`, et conditionnellement le **contenu réel du fichier** (décodé en Base64) dans la colonne BLOB `VAL_CONT_FICH_ENTRE` de `TDF_V_FICH_ECHG_ENTRE`. Ils constituent le **journal d'audit** de chaque transmission — essentiel pour la traçabilité et le diagnostic d'erreurs.

> **Intégration AGS — flux détaillé de `ValiderSecurIdUtilIntvn()` (confirmé dans le code source à partir de `Destinataire.vb`) :**
> 1. Obtenir l'identifiant de l'utilisateur courant via `SecuriteAppli.IdUtilAppelWebServ()`.
> 2. Usurper le contexte Windows (`ObtnNouvContxPrincipal("CISEL")`).
> 3. Créer le client SOAP `RechrRegleAccesSoapClient("EndPointRechrRegleAccesSoap")` — URL du point de terminaison chargée depuis `web.config`, appSetting `UrlAGS`.
> 4. Construire l'entrée : `InfoRegleAccesContxDS` avec `cod_appli` (depuis la configuration) et `id_util` (utilisateur courant).
> 5. Appeler AGS : `ObtnDroitContxRegleAcces(input, output)`.
> 6. Filtrer les droits retournés : `cod_contx_droit_acces = TypEntIntvnEchg AND id_contx_regle_acces = IdEntIntvnEchg`.
> 7. Charger le droit requis depuis la configuration : `NoSeqDroitAccesExig` — pour HOA5, il s'agit du **`246`** (permission de transmettre les fichiers préliminaires).
> 8. Vérifier si le droit \#246 est présent dans l'ensemble retourné — sinon, lever l'erreur **9282** (sécurité/accès refusé).

#### 1.3.7 Injection de stratégie en profondeur — Comment les règles de validation HOA5 s'intègrent au pipeline TDF

> **Source :** Vérifié dans le code source à partir de `MessageFichierEntrant.vb` (stratégie de base TDF, dépôt `TDF-OOA2-Serv-Tranf-Fich`), `MessageFichierEntrantCH.vb` (surcharge de stratégie HOA5, dépôt `MEC-AlimenterBanque`), `DestinataireEntrant.svc.vb` (service TDF Frontend) et `FichDestinataireEntrant.svc.vb` (constructeur HOA5 Frontend).

Cette section explique, en détail complet, le **mécanisme exact** par lequel les règles de validation de fichiers spécifiques à HOA5 sont injectées dans le pipeline de traitement générique TDF. C'est le point d'extensibilité clé de l'architecture TDF — le comprendre est essentiel pour quiconque travaille sur la migration ou l'intégration d'un nouveau système.

##### 1.3.7.1 Le problème de conception

Le TDF Frontend est un **composant générique et réutilisable** partagé par plusieurs systèmes (HOA5, HOA1, HOB1, etc.). Tous ces systèmes suivent le même protocole de transmission en 3 étapes (voir Section 1.4). Mais chaque système a des **règles de validation différentes** pour les fichiers qu'il reçoit :

- **HOA5** (transmissions préliminaires) : attend un ZIP contenant exactement 1 fichier XML, format de nom de fichier `MEC_EEEEEEEE_YYYYMMDDHHMMSS`, valide le XML contre le schéma XSD de HOA5
- **HOA1** (transmissions régulières) : possède ses propres patrons de noms de fichiers, schéma et logique de validation
- **Autres systèmes** : chacun avec ses propres règles

Le code TDF ne peut pas contenir de logique de validation spécifique à HOA5 — cela créerait des dépendances inter-équipes et du couplage. Au lieu de cela, l'équipe TDF a conçu un **mécanisme de points d'extension enfichables** utilisant deux patrons de conception classiques :

| Patron | Ce qu'il fait | Où il se trouve |
|--------|--------------|-----------------|
| **Strategy Pattern** | HOA5 injecte son propre objet stratégie dans un champ déclaré dans la classe de service TDF | Constructeur de `FichDestinataireEntrant` → `Me.objMessageFichierEntrant = New MessageFichierEntrantCH()` |
| **Template Method Pattern** | La stratégie de base TDF définit un pipeline de traitement fixe (`Recevoir`) avec des méthodes « hook » vides que les sous-classes surchargent | `MessageFichierEntrant.Recevoir()` appelle `TraitSpecPreCreatSuiviFich()` et `TraitSpecPreInscrFichSuivi()` — vides dans la base, surchargées par HOA5 |

##### 1.3.7.2 Les trois classes impliquées

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                                                                 │
│  TDF LAYER (repo: TDF-OOA2-Serv-Tranf-Fich)                                   │
│  ──────────────────────────────────────────                                     │
│                                                                                 │
│  ┌─ DestinataireEntrant (WCF service — TDF Frontend) ───────────────────────┐  │
│  │                                                                          │  │
│  │  Protected objMessageFichierEntrant As MessageFichierEntrant   ← FIELD  │  │
│  │                                                      ▲                   │  │
│  │                                                      │ (polymorphic)     │  │
│  │  EnvoyerLotFichier():                                │                   │  │
│  │      ...                                             │                   │  │
│  │      objMessageFichierEntrant.Recevoir(...)  ◄───────┘                   │  │
│  │      ...                                                                 │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌─ MessageFichierEntrant (base strategy class) ────────────────────────────┐  │
│  │                                                                          │  │
│  │  Recevoir():    ← Template Method — FIXED pipeline                      │  │
│  │      1. AssocierFichierUDL()                                             │  │
│  │      2. InitialiserMessageEntrant()                                      │  │
│  │      3. ValiderPresFichAttach()                                          │  │
│  │      4. Decompress ZIP → file list                                       │  │
│  │      5. ► TraitSpecPreCreatSuiviFich()   ← HOOK #1 (empty in base)     │  │
│  │      6. CreerSuiviFich():                                                │  │
│  │            For each file:                                                │  │
│  │              ► TraitSpecPreInscrFichSuivi()  ← HOOK #2 (empty in base) │  │
│  │              Build SuiviFichier row                                      │  │
│  │      7. TraitSpecPostCreatSuiviFich()                                    │  │
│  │                                                                          │  │
│  │  TraitSpecPreCreatSuiviFich():    Protected Overridable → EMPTY BODY    │  │
│  │  TraitSpecPreInscrFichSuivi():    Protected Overridable → sets True      │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                                                                                 │
│  HOA5 LAYER (repo: MEC-AlimenterBanque)                                        │
│  ──────────────────────────────────────                                         │
│                                                                                 │
│  ┌─ FichDestinataireEntrant (HOA5 WCF service) ────────────────────────────┐  │
│  │                                                                          │  │
│  │  Sub New()    ← CONSTRUCTOR — THE INJECTION POINT                       │  │
│  │      objMsgFichEntrantInstc = New MessageFichierEntrantCH()             │  │
│  │      objMsgFichEntrantInstc.ChemConfg = Me.strChemConfg                 │  │
│  │      objMsgFichEntrantInstc.CasUtil = Me.strCasUtil                     │  │
│  │      Me.objMessageFichierEntrant = objMsgFichEntrantInstc   ← INJECT   │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌─ MessageFichierEntrantCH (HOA5 strategy override) ──────────────────────┐  │
│  │  Inherits MessageFichierEntrant (TDF base)                               │  │
│  │                                                                          │  │
│  │  TraitSpecPreCreatSuiviFich():    Protected Overrides  → ZIP validation │  │
│  │  TraitSpecPreInscrFichSuivi():    Protected Overrides  → XML validation │  │
│  │                                                                          │  │
│  │  Private helpers:                                                        │  │
│  │    ValiderFichier()         → filename pattern + extension + estab. ID   │  │
│  │    ValiderContenu()         → non-empty binary content check             │  │
│  │    ValiderChargerSchem()    → XSD schema validation                      │  │
│  │    ObtenirDesMsg()          → resolve localized error text               │  │
│  │    InsererMsgEchgFich()     → overloaded: insert error into suivi DS     │  │
│  │    ConvertirEnChaine()      → byte[] → string conversion                 │  │
│  └──────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

> Pour les développeurs juniors — le principe clé est **« programmer vers une interface, pas vers une implémentation. »** La classe de service TDF déclare le champ avec le type de base `MessageFichierEntrant`. Elle ne sait jamais (et ne se soucie pas) qu'à l'exécution, l'objet réel est un `MessageFichierEntrantCH`. Quand TDF appelle `objMessageFichierEntrant.Recevoir()`, .NET examine le **type réel** de l'objet en mémoire (qui est `MessageFichierEntrantCH`) et dirige l'appel vers ses méthodes surchargées. C'est le **polymorphisme** — le fondement de la conception orientée objet.

##### 1.3.7.3 Trace d'exécution étape par étape — Du constructeur à la validation

Cette trace montre exactement ce qui se passe à l'exécution lorsqu'un CH App envoie un fichier (Étape 2 — `EnvoyerLotFichier`) :

```
TIME ─────────────────────────────────────────────────────────────────────────────

 ① WCF CREATES FichDestinataireEntrant (PerCall = new instance per request)
    │
    ├── Reads config: ChemConfg, CasUtil (from web.config appSettings)
    │
    ├── Creates: objMsgFichEntrantInstc = New MessageFichierEntrantCH()
    │            ↑ This is the HOA5-specific strategy object
    │
    ├── Configures: objMsgFichEntrantInstc.ChemConfg = ChemConfg path
    │               objMsgFichEntrantInstc.CasUtil = "FichCHEntrant"
    │
    └── ★ INJECTS: Me.objMessageFichierEntrant = objMsgFichEntrantInstc
                    ↑ From this point, the TDF field holds an HOA5 object.
                      TDF code doesn't know — it sees only the base type.

 ② FichDestinataireEntrant.EnvoyerLotFichier(xml, bytes, output, entete)
    │
    └── MyBase.EnvoyerLotFichier(...)  ← pure delegation (one-liner)

 ③ DestinataireEntrant.EnvoyerLotFichier(...)  [TDF FRONTEND]
    │
    ├── Validates exchange state, credentials, file parameters
    │
    ├── HTTP POST file → BizTalk (BTSHTTPReceive.dll)   [FILE IS NOW IN BIZTALK]
    │                                                     [before validation runs!]
    │
    ├── ★ objMessageFichierEntrant.Recevoir(...)   ← POLYMORPHIC CALL
    │    │
    │    │  .NET resolves: actual type = MessageFichierEntrantCH
    │    │  Calls: MessageFichierEntrant.Recevoir()  (base method — not overridden)
    │    │
    │    │  ④ INSIDE Recevoir() — TEMPLATE METHOD PIPELINE:
    │    │
    │    ├── a. AssocierFichierUDL()     → set Oracle UDL connection in DataSets
    │    ├── b. GenererAccRecep()        → generate ACK number
    │    ├── c. InitialiserMessageEntrant() → create exchange journal row
    │    ├── d. ValiderPresFichAttach()   → verify byte array not empty
    │    ├── e. RecevoirFichierMessage()  → decompress ZIP → file name list + content array
    │    │
    │    ├── f. ★ TraitSpecPreCreatSuiviFich()     ← HOOK #1 — HOA5 ZIP VALIDATION
    │    │       │
    │    │       │  [TDF base: empty body — does nothing]
    │    │       │  [HOA5 override: MessageFichierEntrantCH.TraitSpecPreCreatSuiviFich()]
    │    │       │
    │    │       ├── Check: file count in ZIP = 1?
    │    │       │     NO (>1) → error 21379 "trop de fichiers dans le lot"
    │    │       │     NO (=0) → error 21382 "aucun fichier dans le lot"
    │    │       │
    │    │       ├── Check: ZIP filename = MEC_EEEEEEEE_YYYYMMDDHHMMSS.ZIP?
    │    │       │     Wrong extension → error 21381
    │    │       │     Wrong pattern   → error 21376
    │    │       │
    │    │       ├── Check: EEEEEEEE in filename = IdEntIntvnEchg (hospital ID)?
    │    │       │     Mismatch → error 21377 "droit d'échange invalide"
    │    │       │
    │    │       └── On any error:
    │    │             → ObtenirDesMsg(errorCode) → get localized French error text
    │    │             → InsererMsgEchgFich() → add error row to SuiviMessage_DS
    │    │             → Set blnFichNonValid = True  (flag: skip per-file validation)
    │    │
    │    ├── g. CreerSuiviFich() — FOR EACH FILE IN ZIP:
    │    │       │
    │    │       ├── ★ TraitSpecPreInscrFichSuivi()  ← HOOK #2 — HOA5 XML VALIDATION
    │    │       │    │
    │    │       │    │  [TDF base: sets _blnAjouFichSuivi = True — always adds file]
    │    │       │    │  [HOA5 override: MessageFichierEntrantCH.TraitSpecPreInscrFichSuivi()]
    │    │       │    │
    │    │       │    ├── IF blnFichNonValid = True → SKIP (ZIP already failed)
    │    │       │    │
    │    │       │    ├── ConvertirEnChaine() → convert byte[] to XML string
    │    │       │    │
    │    │       │    ├── ValiderFichier():
    │    │       │    │     Check: XML filename = MEC_EEEEEEEE_YYYYMMDDHHMMSS.XML?
    │    │       │    │       Wrong pattern → error 21378
    │    │       │    │       EEEEEEEE ≠ hospital ID → error 21377
    │    │       │    │
    │    │       │    ├── ValiderContenu():
    │    │       │    │     Check: byte array length > 0?
    │    │       │    │       Empty → error 21380 "fichier invalide"
    │    │       │    │
    │    │       │    ├── ValiderChargerSchem():
    │    │       │    │     Load XSD from config key (ChemXsdFich)
    │    │       │    │     XmlReader.Create() with XmlReaderSettings.Schemas
    │    │       │    │     XmlDocument.Load() + Validate()
    │    │       │    │       XmlSchemaException → error 21380 "fichier invalide"
    │    │       │    │
    │    │       │    └── On any error:
    │    │       │          → ObtenirDesMsg(errorCode) → localized French text
    │    │       │          → InsererMsgEchgFich() → add error to SuiviMessage_DS
    │    │       │          → _blnAjouFichSuivi = False (exclude from file tracking)
    │    │       │
    │    │       └── IF _blnAjouFichSuivi = True:
    │    │             Build TDF_V_FICH_ECHG_DT row (file tracking)
    │    │             Build TDF_V_FICH_ECHG_ENTRE_DT row (file content/metadata)
    │    │
    │    └── h. TraitSpecPostCreatSuiviFich()  → [TDF base: empty — HOA5 does NOT override]
    │
    ├── SOAP proxy → send SuiviMessage_DS + SuiviFichier_DS to BizTalk
    │     (suivi message → Branch 3)
    │
    └── Return ACK to CH App

 ⑤ WCF DESTROYS FichDestinataireEntrant (PerCall)

TIME ─────────────────────────────────────────────────────────────────────────────
```

##### 1.3.7.4 Codes d'erreur de validation HOA5 — Référence complète

Tous les codes d'erreur ci-dessous sont produits exclusivement par `MessageFichierEntrantCH` (couche HOA5). La classe de base TDF ne génère **jamais** ces erreurs — elle fournit uniquement le mécanisme pour les enregistrer dans `SuiviMessage_DS`.

| Code d'erreur | Méthode hook | Validation | Condition | Description en français (résolue par `ObtenirDesMsg`) |
|---|---|---|---|---|
| **21376** | `TraitSpecPreCreatSuiviFich` | Patron du nom de fichier ZIP | Le format `MEC_EEEEEEEE_YYYYMMDDHHMMSS.ZIP` a échoué à la regex | Forme du fichier ZIP invalide |
| **21377** | `TraitSpecPreCreatSuiviFich` / `TraitSpecPreInscrFichSuivi` | Correspondance de l'ID d'établissement | L'ID à 8 chiffres dans le nom de fichier ≠ `IdEntIntvnEchg` de `EnteteRAMQ` | Droit d'échange invalide (inclut le numéro d'établissement) |
| **21378** | `TraitSpecPreInscrFichSuivi` | Patron du nom de fichier XML | Le format `MEC_EEEEEEEE_YYYYMMDDHHMMSS.XML` a échoué à la regex | Forme du fichier XML invalide |
| **21379** | `TraitSpecPreCreatSuiviFich` | Nombre de fichiers > 1 | Le ZIP contient plus d'un fichier | Trop de fichiers dans le lot |
| **21380** | `TraitSpecPreInscrFichSuivi` | Contenu ou XSD | `byte[].Length = 0` OU `XmlSchemaException` lors de la validation XSD | Fichier invalide |
| **21381** | `TraitSpecPreCreatSuiviFich` | Extension ZIP | Le fichier ne se termine pas par `.ZIP` | Extension du fichier invalide |
| **21382** | `TraitSpecPreCreatSuiviFich` | Nombre de fichiers = 0 | Le ZIP contient 0 fichier | Aucun fichier dans le lot |

> **Source :** Constantes de codes d'erreur provenant de `Constantes.vb` (`HOA5_RecvrTrnsmPrelimCH_cpo`). Texte localisé résolu à l'exécution par la classe `MsgTrait` (bibliothèque RAMQ partagée de résolution de messages) avec `CodAppli` = "MEC".

##### 1.3.7.5 Propagation des erreurs — De la validation à la décision BizTalk

Lorsqu'une erreur de validation survient, elle ne lance PAS d'exception vers le CH App. Au lieu de cela, elle suit un chemin de **propagation d'erreur différée** :

```
MessageFichierEntrantCH                         TDF Frontend                        TDF Integration (BizTalk)
    detects error                            sends tracking DataSets               receives suivi message
        │                                           │                                      │
        ▼                                           │                                      │
  InsererMsgEchgFich()                              │                                      │
   → adds error row to                              │                                      │
     SuiviMessage_DS                                │                                      │
     .TDF_V_MSG_ECHG_FICH_DT                        │                                      │
        │                                           │                                      │
        ▼                                           ▼                                      │
  Recevoir() completes         →      EnvoyerSuiviServCourtage()                           │
  (does NOT throw exception)           → SOAP proxy sends                                  │
                                         SuiviMessage_DS + SuiviFichier_DS                 │
                                         to BizTalk port poRecptInfoSuivi                  │
                                                    │                                      │
                                                    └──────────────────────►               │
                                                                                           ▼
                                                                           BizTalk checks:
                                                                           SuiviMessage_DS
                                                                           .TDF_V_MSG_ECHG_FICH_DT
                                                                           .Rows.Count > 0 ?
                                                                                   │
                                                                            ┌──────┴──────┐
                                                                            │             │
                                                                           YES           NO
                                                                            │             │
                                                                            ▼             ▼
                                                                   Erreur = "1"    Erreur = "0"
                                                                   Do NOT route    Route file to
                                                                   to HOA5         HOA5 Integration
                                                                   Integration     → Backend → Oracle
                                                                   (suspend)
```

> Pour les développeurs juniors — il s'agit d'une conception **« fail-forward »**. Le système ne s'arrête PAS lorsqu'une erreur de validation est détectée. Au lieu de cela :
> 1. L'erreur est **enregistrée** dans un DataSet (comme si on l'écrivait sur un formulaire)
> 2. Le DataSet est **envoyé** à BizTalk sous forme de message de suivi
> 3. BizTalk **lit** les enregistrements d'erreur et **décide** s'il doit traiter le fichier ou non
> 4. Le CH App reçoit toujours une **réponse normale** (pas un SOAP fault) — l'erreur est retournée dans la sortie XML
>
> Pourquoi cette conception ? Parce que le fichier a déjà été envoyé à BizTalk AVANT la validation (voir Section 1.3.6 « FILE IS SENT BEFORE VALIDATION »). Le système ne peut pas annuler l'envoi. Alors, il informe plutôt BizTalk « ce fichier contient des erreurs — ne le traitez pas. »

##### 1.3.7.6 Règles de validation des noms de fichier — Format détaillé

La méthode privée `ValiderFichier()` valide les noms de fichier à l'aide de patrons regex et d'analyse de dates :

```
Expected filename format:  MEC_EEEEEEEE_YYYYMMDDHHMMSS.EXT

Where:
  MEC         = fixed prefix (application code for MedÉcho)
  _           = underscore separator (CONS_SEPARATEUR_FICH = "_")
  EEEEEEEE    = 8-digit establishment number (must match IdEntIntvnEchg from EnteteRAMQ)
  _           = underscore separator
  YYYYMMDDHHMMSS = 14-character date-time (validated with Date.TryParseExact)
  .EXT        = .ZIP (for lot) or .XML (for individual data file)

Regex patterns (from Constantes.vb):
  ZIP: RegexNomFichZip → matches MEC_XXXXXXXX_XXXXXXXXXXXXXX.ZIP
  XML: RegexNomFichXml → matches MEC_XXXXXXXX_XXXXXXXXXXXXXX.XML

Validation steps:
  1. Check extension (.ZIP or .XML)     → error 21381 if ZIP extension wrong
  2. Check regex match                  → error 21376 (ZIP) or 21378 (XML) if pattern fails
  3. Extract date substring (position 13, length 14) → parse as date
     → same error code if date invalid
  4. Extract establishment ID (split by "_", take [1])
     → error 21377 if ≠ IdEntIntvnEchg

Example valid filename:  MEC_12345678_20260325143052.ZIP
  MEC         → OK (prefix)
  12345678    → OK (8-digit establishment number)
  20260325143052 → OK (March 25, 2026, 14:30:52)
  .ZIP        → OK (extension)
  12345678 must equal the hospital's IdEntIntvnEchg from EnteteRAMQ
```

##### 1.3.7.7 Validation de schéma XSD — Fonctionnement de `ValiderChargerSchem()`

Le chemin du schéma XSD est chargé à l'exécution depuis les `appSettings` du `web.config` (clé provenant de `Constantes.CleChemXsdFich`) :

```vb
' 1. Load schema
objXmlRs = New XmlReaderSettings()
objXmlRs.Schemas.Add("", ChemXsdFich)          ' Add XSD with no target namespace
objXmlRs.ValidationType = ValidationType.Schema

' 2. Parse XML
Dim xmlTxtRdr As New XmlTextReader(New StringReader(_strContFichXml))
Dim reader As XmlReader = XmlReader.Create(xmlTxtRdr, objXmlRs)
_xmlDoc = New XmlDocument
_xmlDoc.Load(reader)        ' Loads + validates during parse

' 3. Post-load validation
_xmlDoc.Validate(AddressOf ValiderSchemaEventHandler)
' Handler throws XmlSchemaException on Warning or Error severity
```

> Pour les développeurs juniors — XSD (XML Schema Definition) est un fichier qui décrit la **structure attendue** d'un fichier XML : quels éléments doivent être présents, quels types de données ils utilisent, lesquels sont optionnels, etc. `ValiderChargerSchem()` charge le schéma XSD HOA5 et valide le XML entrant par rapport à celui-ci. Si le XML ne correspond pas (éléments erronés, champs obligatoires manquants, types de données incorrects), .NET lance une `XmlSchemaException`. Le bloc `Catch ex As XmlSchemaException` dans `TraitSpecPreInscrFichSuivi()` intercepte cette exception et enregistre l'erreur **21380** dans le DataSet de suivi.

---

### 1.4 TDF Frontend — Protocole de transmission en 3 étapes

> **Source :** Vérifié dans le code source de `DestinataireEntrant.svc.vb` (TDF Frontend, `OOA2_ServTranfMsgFich_svc`) et `FichDestinataireEntrant.svc.vb` (HOA5 Frontend, `HOA5_RecvrTrnsmPrelimCH_svc`).

Le protocole de transmission TDF expose une **séquence de 3 opérations WCF** que chaque application de centre hospitalier (CH) doit suivre pour soumettre une transmission. Ces 3 opérations sont définies par l'interface `IDestinataireEntrant` (TDF Frontend). Le HOA5 Frontend héberge cette interface et délègue toute l'exécution à la classe de base du TDF Frontend (`DestinataireEntrant`).

#### 1.4.1 Étape 1 — `InitierEnvoi` (Initier la transmission)

- **Rôle :** Initialise la session de transaction.
- **Actions (confirmées à partir du code source) :**
  - Lit l'en-tête SOAP (`EnteteRAMQ`) pour identifier le CH.
  - Appelle `ValiderInfoTranfInitierEnvoi()` — valide les identifiants du CH auprès de l'Active Directory RAMQ et vérifie les **droits d'accès au niveau applicatif** via le système **AGS** (service ASMX `YRD1_RechrRegleAcces_ws`). Pour HOA5, le droit requis est le **\#246** (permission de transmettre des fichiers préliminaires). Voir Section 1.3.6 et Section 11.1.
  - Appelle `CreerNoEchange()` — génère un **numéro d'échange** unique (`NoEchgFich`). Algorithme (vérifié dans le code source de `Destinataire.vb`) : `IdEntIntvnEchg` + `Date.Now.ToUniversalTime()` formaté en `YYYYMMDDHHmmssfff` (horodatage UTC à la milliseconde, 17 chiffres). Exemple : hôpital `AGS12345` à 2026-03-25 14:30:52.123 UTC → `AGS1234520260325143052123`.
  - Remplit l'objet de réponse `objContexteWS` avec le numéro d'échange et le sérialise dans le paramètre de sortie XML `_strXmlSortie` pour retour au client. Sérialise également l'état contextuel dans `EnteteRAMQ.EtatEchgFich` en Base64 via `ColctContxEchgFich`.
- **Sortie :** Retourne le **numéro d'échange** à l'application CH dans le paramètre de sortie XML `_strXmlSortie`.
- **Important — pas de session WCF côté serveur :** `InstanceContextMode.PerCall` est déclaré sur `FichDestinataireEntrant` (HOA5 Frontend) — une **nouvelle instance de service** est créée et détruite à chaque appel. Le numéro d'échange (`NoEchgFich`) est retourné dans le paramètre de sortie XML `_strXmlSortie` à l'application CH, qui est responsable de le stocker et de le repasser aux étapes 2 et 3. L'état qui doit survivre entre les appels (numéro d'échange, accusé de réception, drapeaux d'état de l'échange) est sérialisé dans **`EnteteRAMQ.EtatEchgFich`** sous forme de données encodées en Base64 via `ColctContxEchgFich` — l'application CH reçoit l'`EnteteRAMQ` mis à jour (via `ByRef`) et doit le renvoyer inchangé lors du prochain appel. **Note :** Le code source contient des appels `Session(...)` commentés — l'état de session ASP.NET était le mécanisme d'état **original**, mais il a été remplacé par l'approche de sérialisation Base64 `EnteteRAMQ.EtatEchgFich`. Le suivi de corrélation côté serveur est également écrit dans la base de données Oracle TDF via `OOA2_InscrireSuiviFich_Ws` lors de l'étape 2.

#### 1.4.2 Étape 2 — `EnvoyerLotFichier` (Envoyer le lot de fichiers)

- **Rôle :** Transfère le contenu réel du fichier à TDF Integration (BizTalk) et crée les enregistrements de suivi.
- **Actions (ordre d'appel vérifié dans le code source de `DestinataireEntrant.svc.vb`) :**
  1. **Validation** — `ValiderInfoTranfEnvoyerLot()` : valide l'en-tête, le numéro d'échange, l'état, les identifiants, le nom du fichier et la compression.
  2. **Envoi du MESSAGE 1 (fichier) à BizTalk** — `EnvoyerMessageServCourtage()` : envoie le fichier compressé à TDF Integration via **HTTP POST** (voir explication ci-dessous). Cible : `BTSHTTPReceive.dll` (clé de configuration `UrlServCourtageEnvoyLot`). Ceci publie le **message fichier** (`schInfoFichMsgFichEntrant`) dans le MessageBox BizTalk. La **branche 2** de TDF Integration (`scopeFichMsg`) le reçoit. Jusqu'à 5 réessais en cas d'échec.
  3. **Validation + création des DataSets de suivi** — `objMessageFichierEntrant.Recevoir(...)` : il s'agit d'un **appel polymorphique** (voir Section 1.3.6). À l'exécution, .NET route vers `MessageFichierEntrantCH.Recevoir()` (surcharge HOA5). À l'intérieur de `Recevoir()`, le pipeline TDF appelle `TraitSpecPreCreatSuiviFich()` (validation du ZIP) et `TraitSpecPreInscrFichSuivi()` (validation XML par fichier). Le résultat : deux DataSets de suivi — **`SuiviMessage_DS`** (suivi au niveau de l'échange) et **`SuiviFichier_DS`** (suivi au niveau du fichier). Voir Section 1.3.6 pour les signatures complètes des méthodes et les codes d'erreur.
  4. **Envoi du MESSAGE 2 (suivi) à BizTalk** — `EnvoyerSuiviServCourtage()` : envoie les DataSets de suivi à TDF Integration via **proxy SOAP** (voir explication ci-dessous). Il s'agit d'un appel interne serveur-à-serveur du TDF Frontend vers TDF Integration — PAS l'application CH qui appelle le HOA5 Frontend.
     - **Classe proxy SOAP :** `TrnsmInfoSuiviEntrantBtWS`, méthode `opRecptInfoSuivi(dsSuiviFichBtWS)`
     - **Cible :** Point de terminaison SOAP de réception publié par BizTalk (port `poRecptInfoSuivi`). Clé de configuration : appSetting `UrlEnvoiFichEntrant`.
     - **Schéma du contenu :** `schInfoSuiviFichEntrant` — contient `SuiviMessage_DS` et `SuiviFichier_DS` sous forme de XML embarqué
     - **Résultat :** La **branche 3** de TDF Integration (`scopeSuivi`) le reçoit. Voir Section 1.5.
  5. **Génération et retour de l'accusé de réception** — `ObtenirAccRecep()` : génère un **accusé de réception** via `GenererAccRecep()` dans `Message.vb` : `IdEntIntvnEchg` + horodatage local `YYYYMMDDHHmmss` (p. ex., `AGS1234520260325143052`). L'accusé de réception est stocké dans `SuiviMessage_DS` (colonne `NO_ACC_RECEP`) et dans le contexte `EnteteRAMQ.EtatEchgFich` (Base64). Retourné à l'application CH dans `_strXmlSortie` (clé XML `no_accu_recept`). **Objectif :** L'accusé de réception est un **jeton de poignée de main** — l'application CH doit le renvoyer à l'étape 3 (`CorrellerEnvoyer`) où `ValiderAccRecep()` le compare à l'accusé de réception stocké dans le contexte. Ce n'est qu'après la validation de l'accusé de réception que le TDF Frontend envoie le message de corrélation à TDF Integration (BizTalk).

> **Ordre d'appel confirmé (vérifié dans le code source) :** Validation → **le fichier est envoyé EN PREMIER** → puis validation/création du DataSet → puis **le suivi est envoyé EN SECOND** → puis l'accusé de réception. Le message fichier atteint BizTalk AVANT que les enregistrements de validation et de suivi ne soient créés.

> **Pour les développeurs juniors — HTTP POST vs proxy SOAP (les deux mécanismes de transport utilisés par le TDF Frontend) :**
>
> Le TDF Frontend envoie 3 messages à TDF Integration en utilisant **deux mécanismes de transport différents**. Comprendre la différence est essentiel :
>
> **HTTP POST** (`EnvoyerMessageServCourtage()` — utilisé pour le message FICHIER) :
> - Utilise `HttpClient.PostAsync()` — une **requête HTTP brute** avec un corps XML
> - Il n'y a **pas d'enveloppe SOAP** — le contenu XML est envoyé directement comme corps HTTP
> - Content-Type : `application/x-www-form-urlencoded` (vérifié dans le code source de `Constante.vb` ligne 163)
> - Pensez-y comme : « J'envoie des données XML brutes via HTTP, comme le téléversement d'un document »
> - Cible : `BTSHTTPReceive.dll` — une extension IIS BizTalk (filtre ISAPI) qui accepte les HTTP POST et publie le XML reçu comme message BizTalk
> - Authentification : identifiants Windows (Kerberos) via un `HttpClientHandler` personnalisé avec `UseDefaultCredentials=True` + en-têtes personnalisés (`X-RAMQ-Nom-Ident`, `X-RAMQ-Type-Auth=Kerberos`)
>
> **Proxy SOAP** (`EnvoyerSuiviServCourtage()` et `EnvoyerCorrellerServCourtage()` — utilisé pour les messages SUIVI et CORRÉLATION) :
> - Utilise une **classe client .NET auto-générée** (créée par Visual Studio à partir du WSDL des points de terminaison publiés par BizTalk)
> - La classe proxy hérite de `SoapHttpClientProtocol` — elle encapsule l'appel dans une **enveloppe SOAP** (`<soap:Envelope><soap:Body>...</soap:Body></soap:Envelope>`) et l'envoie via HTTP
> - Pensez-y comme : « J'appelle une méthode distante en utilisant le standard SOAP — les données XML sont encapsulées dans une structure de message SOAP »
> - Cible : points de terminaison `.svc` publiés par BizTalk (auto-générés par BizTalk à partir des ports de réception de son orchestration ; les classes proxy côté client utilisent le style ASMX `SoapHttpClientProtocol` pour la rétrocompatibilité, mais les points de terminaison côté serveur sont WCF-BasicHttp `.svc`)
> - Authentification : Impersonation Windows + `SecuriserEnveloppe()`
>
> **Tableau comparatif :**
>
> | | HTTP POST (fichier) | Proxy SOAP (suivi, corrélation) |
> |--|--|--|
> | **Méthode** | `HttpClient.PostAsync()` | Auto-générée `.opRecptInfoSuivi()` / `.opRecptInfoCorln()` |
> | **Enveloppe** | Aucune — corps XML brut | Enveloppe SOAP encapsulant le corps XML |
> | **Content-Type** | `application/x-www-form-urlencoded` (vérifié dans le code source de `Constante.vb`) | `text/xml` (standard SOAP) |
> | **Cible** | `BTSHTTPReceive.dll` (ISAPI IIS) | Point de terminaison `.svc` (service WCF-BasicHttp publié par BizTalk) |
> | **Adaptateur BizTalk** | Adaptateur de réception HTTP | Adaptateur de réception WCF-BasicHttp |
> | **Auth** | Windows/Kerberos (HttpClient) + en-têtes personnalisés (`X-RAMQ-Nom-Ident`) | Impersonation Windows (client SOAP) |
> | **Taille du contenu** | Volumineuse (jusqu'à 20 Mo) | Petite (XML de suivi, ou simplement `NoEchg`) |
> | **Réessai** | Jusqu'à 5 réessais (boucle simple, sans backoff) | Aucun réessai au niveau du TDF Frontend |
>
> **Pour les développeurs juniors — POURQUOI deux protocoles de transport différents ? (vérifié dans le code source, 5 raisons) :**
>
> Le fait que le TDF Frontend utilise HTTP POST pour le fichier mais SOAP pour le suivi/la corrélation n'est pas un accident. Il y a 5 raisons techniques concrètes, confirmées par l'analyse du code source :
>
> **Raison 1 — Taille du contenu binaire.** Le message fichier embarque l'intégralité du ZIP compressé en Base64 à l'intérieur du corps XML. Pour HOA5, cela représente ~1 Mo compressé ; l'encodage Base64 gonfle ce volume d'environ 33 %, de sorte que le corps HTTP POST atteint ~1,3 Mo et plus. L'adaptateur HTTP de BizTalk (`BTSHTTPReceive.dll`) traite le corps HTTP brut directement comme un message BizTalk — sans surcharge SOAP. Une enveloppe SOAP ajouterait les balises `<Envelope>`, `<Header>`, `<Body>`, une pollution d'espaces de noms XML, et une surcharge de sérialisation/désérialisation pour le contenu volumineux en Base64.
>
> **Raison 2 — `ReturnCorrelationHandle` (fonctionnalité de l'adaptateur HTTP).** L'emplacement de réception HTTP possède `ReturnCorrelationHandle = -1` (true) dans le fichier de liaisons. Il s'agit d'une **fonctionnalité unique de l'adaptateur HTTP** : BizTalk génère un jeton de handle de corrélation et le retourne **dans le corps de la réponse HTTP**. Le code du TDF Frontend capture cette valeur à la ligne 815 : `_strAccRecepBizTalk = objReponse.Content.ReadAsStringAsync().Result`. Cette valeur devient l'**accusé de réception BizTalk**. Ce mécanisme est nativement pris en charge par l'adaptateur HTTP mais n'a **pas d'équivalent direct** dans l'adaptateur WCF-BasicHttp pour les ports de réception unidirectionnels.
>
> **Raison 3 — En-têtes HTTP personnalisés pour la propagation de l'identité.** La classe interne `HandlerSecurite` (lignes 654–667 de `DestinataireEntrant.svc.vb`) injecte `X-RAMQ-Nom-Ident` (identité de l'appelant depuis `HttpContext.Current.User.Identity.Name`) et `X-RAMQ-Type-Auth` = `Kerberos` comme en-têtes HTTP. Ces en-têtes propagent l'identité originale de l'appelant CH à BizTalk au niveau du transport. C'est naturel avec HTTP POST mais nécessiterait des en-têtes SOAP personnalisés ou des inspecteurs de messages WCF avec un proxy SOAP.
>
> **Raison 4 — Le suivi et la corrélation sont des métadonnées légères.** Le message de suivi transporte les DataSets de suivi (`SuiviMessage` + `SuiviFichier` en XML) et un drapeau d'erreur. Le message de corrélation transporte uniquement `NoEchg`. Ce sont des **contenus petits, structurés et typés** — idéaux pour SOAP. La fonctionnalité « publier l'orchestration comme service web » de BizTalk auto-génère des points de terminaison WCF avec des contrats WSDL/XSD appropriés, fournissant **gratuitement des classes proxy fortement typées** (`Root.NoEchg`, `Root.Erreur`, `Root.SuiviFichier`, etc.).
>
> **Raison 5 — Évolution historique depuis les pièces jointes DIME (preuve dans le code source).** Le code source contient des références DIME/WSE commentées qui révèlent que le système a eu **3 incarnations** :
> 1. **Original (~2004) :** Pièces jointes SOAP DIME via WSE pour le fichier — les 3 messages auraient utilisé SOAP. Preuve dans le code source : ligne 103 (`bytAttach` documenté comme *« Contenu binaire de l'attachement DIME »*), lignes 1104–1116 (code commenté `SoapContext`, `HttpSoapContext.RequestContext`).
> 2. **Intermédiaire :** HTTP POST direct vers `BTSHTTPReceive.dll` remplaçant DIME pour le fichier, en conservant SOAP pour le suivi/la corrélation. Preuve dans le code source : lignes 719–720 (commentaire : *« on doit transférer le fichier via un attachement DIME dans service web .NET au lieu d'un POST sur l'adaptateur HTTP de BizTalk »*).
> 3. **Refactorisation de 2018 :** Passage du client HTTP legacy à `HttpClient` + authentification intégrée Windows (ligne 65 : commentaire Historique).
>
> Lorsque le protocole DIME (WSE 2.0/3.0) a été déprécié, le transfert de fichiers a été migré vers du HTTP POST brut (plus simple, sans surcharge SOAP pour les gros binaires), tandis que le suivi/la corrélation sont restés sur SOAP parce qu'ils utilisaient déjà les points de terminaison publiés par BizTalk et que leurs contrats étaient stables.
>
> **Le message fichier aurait-il pu être envoyé via SOAP à la place ?** Techniquement oui, mais avec des compromis significatifs : perte de `ReturnCorrelationHandle` (nécessiterait de reconcevoir le mécanisme d'accusé de réception), perte des en-têtes HTTP personnalisés pour la propagation de l'identité, surcharge supplémentaire de l'enveloppe SOAP sur chaque contenu de ~1 Mo et plus, et nécessiterait de changer le port de réception BizTalk de l'adaptateur HTTP à WCF-BasicHttp. Aucun commentaire dans le code source ne dit explicitement « nous avons choisi HTTP parce que... » — la décision architecturale est antérieure au code actuel (décision DIME originale vers ~2004). Mais les contraintes techniques justifient clairement la conception actuelle.

- **Implication :** Après cette étape, BizTalk a reçu **deux messages** (fichier + suivi), tous deux étiquetés avec le même numéro d'échange (`NoEchgFich`). Le numéro d'échange est le seul lien de corrélation. Aucune session WCF n'existe — la corrélation est maintenue uniquement par la propriété promue `NoEchg` dans le Parallel Activating Convoy de BizTalk (voir Section 1.5.3).

#### 1.4.3 Étape 3 — `CorrellerEnvoyer` (Confirmer et corréler)

- **Rôle :** Ferme la transaction et confirme l'intégrité de la réception.
- **Actions (ordre d'appel vérifié dans le code source de `DestinataireEntrant.svc.vb`) :**
  1. **Lecture de l'entrée** — `LireValidXmlEntreeCorrellerEnvoyer()` : analyse et valide le numéro d'accusé de réception depuis l'entrée XML `_strXmlEntree`.
  2. **Validation** — `ValiderInfoTranfCorrellerEnvoyer()` : effectue 6 validations — en-tête présent, numéro d'échange correspondant au contexte, accusé de réception correspondant à l'accusé stocké dans le contexte (`ValiderAccRecep()` dans `Destinataire.vb`), identifiant d'entité valide, type d'entité valide, identifiants de sécurité valides. Si l'accusé de réception ne correspond pas → lève `ApplicationExceptionRAMQ` (rejette la corrélation).
  3. **Envoi du MESSAGE 3 (corrélation) à BizTalk** — `EnvoyerCorrellerServCourtage()` : envoie la confirmation de corrélation à TDF Integration via **proxy SOAP** (voir l'explication du proxy SOAP à l'étape 2 ci-dessus). Il s'agit d'un appel interne serveur-à-serveur du TDF Frontend vers TDF Integration — PAS l'application CH qui appelle le HOA5 Frontend.
     - **Classe proxy SOAP :** `TrnsmCorlnEntrantBtWS`, méthode `opRecptInfoCorln(objInfoCorln)`
     - **Cible :** Point de terminaison SOAP de réception publié par BizTalk (port `poRecptInfoCorln`). Clé de configuration : appSetting `UrlEnvoiCorlnEntrant`.
     - **Schéma du contenu :** `schInfoCorlnFichEntrant` — contient **uniquement `NoEchgFich`** (numéro d'échange). C'est un pur signal de confirmation — aucun contenu de fichier, aucune donnée de suivi.
     - **Résultat :** La **branche 1** de TDF Integration (`scopeCorln`) le reçoit. Voir Section 1.5.
  4. **Nettoyage du contexte** — `ViderContexte()` : supprime toutes les clés de `EnteteRAMQ.EtatEchgFich` (efface le numéro d'échange, l'accusé de réception, l'état) et retourne la **confirmation finale** à l'application CH.
- **Implication :** Ceci complète la poignée de main à 3 voies. L'orchestration TDF Integration attend ce message de corrélation avec un délai d'expiration de 9 minutes (`scopeCorln`). Si l'application CH n'appelle jamais `CorrellerEnvoyer`, la branche de corrélation atteint le délai d'expiration, `gblnCorreler` est mis à `false`, et l'orchestration enregistre l'échange comme **abandonné** via `InscrireSuiviFichAbandon` (voir Section 1.5.2, Règle 2 dans `decideTransaction`).

#### 1.4.4 Flux résumé

```
CH App
  │
  ├── 1. InitierEnvoi()        → [HOA5 Frontend] delegates → [TDF Frontend]
  │                                   validates credentials + access rules (YRD1_RechrRegleAcces_ws)
  │                                   creates exchange number (CreerNoEchange: IdEntIntvnEchg + UTC YYYYMMDDHHmmssfff)
  │                                           ← returns: numéro d'échange
  ├── 2. EnvoyerLotFichier()   → [HOA5 Frontend] delegates → [TDF Frontend]
  │                                   sends file to TDF Integration (BizTalk) via HttpClient.PostAsync → BTSHTTPReceive.dll
  │                                   creates tracking (SuiviMsg + SuiviFich DataSets for audit trail)
  │                                   generates ACK (handshake token for Step 3 validation)
  │                                           → [TDF Integration — BizTalk] → [HOA5 Integration — BizTalk]
  │                                                                              → [HOA5 Backend]
  │                                                                                  → DepoApli (ZIP saved)
  │                                                                                  → Oracle (transactional insert)
  │                                           ← returns: accusé de réception
  │
  └── 3. CorrellerEnvoyer()    → [HOA5 Frontend] delegates → [TDF Frontend]
                                      validates ACK from Step 2
                                      TDF Frontend sends CORRELATION MESSAGE to TDF Integration (BizTalk):
                                        WHO calls: TDF Frontend (DestinataireEntrant.EnvoyerCorrellerServCourtage())
                                        HOW: SOAP proxy class TrnsmCorlnEntrantBtWS, method opRecptInfoCorln()
                                        TARGET: BizTalk-published SOAP endpoint (port poRecptInfoCorln)
                                              ← final confirmation returned to CH App
```

#### 1.4.5 Contraintes clés et observations

- Le **protocole stateful en 3 étapes doit être préservé** — l'hypothèse du projet est de ne pas modifier l'application CH. L'interface du HOA5 Frontend est verrouillée. Tout remplacement doit exposer le même contrat WCF en 3 étapes.

  > **Couple verrouillé — Application CH et HOA5 Frontend :** L'application CH et le HOA5 Frontend partagent le même contrat WCF (`IDestinataireEntrant`, 3 opérations) — c'est un **couple fortement couplé**. Si l'application CH ne change pas (hypothèse du projet), alors l'interface du HOA5 Frontend **ne peut pas non plus changer**, par définition. Toute modification du contrat WCF du HOA5 Frontend (signatures d'opérations, espaces de noms, structure de l'enveloppe SOAP) nécessiterait un **changement coordonné du côté de l'application CH**. Comme ce projet suppose que l'application CH reste inchangée, le contrat du HOA5 Frontend est gelé. N'introduisez pas de changements à `IDestinataireEntrant`, ses paramètres d'opérations, ou l'espace de noms SOAP sans approbation de gouvernance explicite du propriétaire de l'application CH.
- Le numéro d'échange (`NoEchgFich`) est un **jeton de corrélation transmis en tant que donnée** entre les appels (`_strXmlSortie` à l'étape 1, `_strXmlEntree` aux étapes 2 et 3). Le HOA5 Frontend est `PerCall` — pas de session WCF. Le numéro d'échange doit être accepté et acheminé à travers 3 appels sans état.
- **La validation des règles d'accès** à l'étape 1 appelle `YRD1_RechrRegleAcces_ws` — ce service externe ASMX est une dépendance à l'exécution. S'il est indisponible, aucune transmission ne peut être initiée.
- **TDF Integration et HOA5 Integration s'exécutent à l'intérieur de BizTalk** — ils **doivent être replatformés** comme objectif principal de la migration. Le bus de messagerie d'entreprise (BizTalk) est remplacé par **Enterprise Message Transit (EMT)**.

---

### 1.5 TDF Integration — Architecture physique (BizTalk)

> **Source :** Vérifié dans le code source de `orcTrnsmFichEntrant.odx`, du fichier de liaisons de production `Prod.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml`, et des fichiers de schémas BizTalk dans `OOA2_SchemMsg_bt`. Tous les noms de variables, noms de shapes, valeurs de délai d'expiration et noms de ports sont tirés directement de l'ODX et du XML de liaisons.
>
> **Propriété de l'équipe :** TDF Integration est développé et maintenu par l'**équipe TDF** (dépôt `TDF-OOA2-Serv-Tranf-Fich`). C'est une **infrastructure partagée** utilisée par plusieurs flux de transmission. L'équipe HOA5 en dépend mais ne le possède ni ne le modifie.

#### 1.5.1 Comment l'orchestration est activée (Parallel Activating Convoy)

> **C'est le concept le plus important à comprendre dans l'architecture de TDF Integration.** Si vous ne comprenez pas ceci, le reste de cette section n'aura pas de sens.

L'orchestration TDF Integration utilise un patron intégré de BizTalk appelé **Parallel Activating Convoy**. Voici ce que cela signifie, étape par étape :

**Orchestration BizTalk normale (pour comparaison) :** Une orchestration typique possède UN SEUL shape de réception activant. Lorsqu'un message correspondant arrive dans le MessageBox BizTalk, BizTalk crée une nouvelle instance d'orchestration et délivre le message à ce shape de réception. L'orchestration exécute ensuite sa logique séquentiellement.

**TDF Integration (Parallel Activating Convoy) :** Cette orchestration possède **TROIS shapes de réception activants**, un dans chaque branche d'un shape Parallel. Cela signifie que N'IMPORTE LEQUEL des 3 messages (fichier, suivi ou corrélation) peut être le **premier** à arriver et **créer** l'instance d'orchestration.

```
Step-by-step: what happens when TDF Frontend sends 3 messages

1. TDF Frontend sends Message 1 (file) via HTTP POST to BTSHTTPReceive.dll
   → BizTalk receives it, promotes NoEchg="12345" from XML, publishes to MessageBox

2. BizTalk subscription engine checks:
   "Is there an orchestration instance waiting for a message with NoEchg=12345
    on port poRecptInfoFichMsg?"
   → NO instance exists yet
   → BUT rcvInfoFichMsg has Activate=True, so BizTalk CREATES a new instance
   → The orchestration starts, enters scopeRecvrFichEntrant (10-min timeout),
     enters the Parallel shape → all 3 branches start concurrently
   → Branch 2 (scopeFichMsg) receives the file message immediately
   → Branch 1 (scopeCorln) starts WAITING for correlation message with NoEchg=12345
   → Branch 3 (scopeSuivi) starts WAITING for suivi message with NoEchg=12345

3. TDF Frontend sends Message 2 (suivi) via SOAP proxy to BizTalk-published endpoint
   → BizTalk receives it, promotes NoEchg="12345", publishes to MessageBox
   → Subscription engine: "Is there an instance waiting for NoEchg=12345 on poRecptInfoSuivi?"
   → YES — the instance from step 2 has Branch 3 waiting
   → Message delivered to Branch 3 (scopeSuivi) → gblnSuiviPresent = true

4. TDF Frontend sends Message 3 (correlation) via SOAP proxy to BizTalk-published endpoint
   → BizTalk receives it, promotes NoEchg="12345", publishes to MessageBox
   → Subscription engine: "Is there an instance waiting for NoEchg=12345 on poRecptInfoCorln?"
   → YES — the instance from step 2 has Branch 1 waiting
   → Message delivered to Branch 1 (scopeCorln) → gblnCorreler = true

5. All 3 branches have completed → Parallel shape exits → Decision logic runs
```

> **Pour les développeurs juniors — points clés sur l'activation :**
> - L'orchestration N'attend PAS les 3 messages avant de démarrer. Elle démarre lorsque le PREMIER message (quel qu'il soit parmi les 3) arrive.
> - Le « Parallel » ne signifie pas 3 instances d'orchestration distinctes. Cela signifie 3 branches **au sein de la même instance unique**, s'exécutant de manière concurrente.
> - Quel que soit le message qui arrive en premier, il établit la valeur de `NoEchg` comme clé de corrélation. Les 2 autres messages doivent porter le MÊME `NoEchg` sinon ils ne seront pas acheminés vers cette instance (ils créeraient une instance différente ou seraient non livrables).
> - Si une branche ne reçoit pas son message dans les 9 minutes, BizTalk lance une `TimeoutException` dans le scope de cette branche. Le gestionnaire d'exception met le drapeau booléen correspondant à `false`.
> - Le shape Parallel bloque jusqu'à ce que les 3 branches soient terminées — soit en recevant un message, soit par expiration du délai.

#### 1.5.2 Flux complet de l'orchestration (shape par shape)

Voici la structure complète de `orcTrnsmFichEntrant.odx`, montrant chaque shape du début à la fin :

```
orcTrnsmFichEntrant (long-running orchestration)
│
└── scopeRecvrFichEntrant  [Transaction: Long-Running, Timeout: 10 minutes]
    │
    ├── parallActEtapeRecptFichEntrant  [Parallel shape — 3 concurrent branches]
    │   │
    │   ├── BRANCH 1: scopeCorln  [Transaction scope, Timeout: 9 min]
    │   │   │
    │   │   ├── rcvInfoCorln  [RECEIVE shape]
    │   │   │     Port: poRecptInfoCorln (Implements = receives, Activate = True)
    │   │   │     Message type: msgInfoCorln (schema: schInfoCorlnFichEntrant)
    │   │   │     Correlation: corNoEchg (Initializing = True)
    │   │   │     → When this message arrives: BizTalk extracts NoEchg from XML
    │   │   │
    │   │   ├── exprAffecterBlnCorrelerTrue  [Expression shape]
    │   │   │     Code: gblnCorreler = True
    │   │   │
    │   │   └── [On timeout — catch excpCorln (TimeoutException CorlnTimeOut):]
    │   │         ├── exprAffecterBlnCorrelerFalse: gblnCorreler = False
    │   │         └── constrMsgCorlnNull: creates dummy msgInfoCorln = <Root/>
    │   │
    │   ├── BRANCH 2: scopeFichMsg  [Transaction scope, Timeout: 9 min]
    │   │   │
    │   │   ├── rcvInfoFichMsg  [RECEIVE shape]
    │   │   │     Port: poRecptInfoFichMsg (Implements = receives, Activate = True)
    │   │   │     Message type: msgInfoFichMsg (schema: schInfoFichMsgFichEntrant)
    │   │   │     Correlation: corNoEchg (Initializing = True)
    │   │   │
    │   │   ├── exprAffecterBlnFichierTrue  [Expression shape]
    │   │   │     Code: gblnFichierPresent = True
    │   │   │
    │   │   └── [On timeout — catch excpFichMsg (TimeoutException FichMsgTimeOut):]
    │   │         ├── exprAffecterBlnFichierFalse: gblnFichierPresent = False
    │   │         └── constrMsgFichMsgNull: creates dummy msgInfoFichMsg = <Fichier/>
    │   │
    │   └── BRANCH 3: scopeSuivi  [Transaction scope, Timeout: 9 min]
    │       │
    │       ├── rcvInfoSuivi  [RECEIVE shape]
    │       │     Port: poRecptInfoSuivi (Implements = receives, Activate = True)
    │       │     Message type: msgInfoSuivi (schema: schInfoSuiviFichEntrant)
    │       │     Correlation: corNoEchg (Initializing = True)
    │       │
    │       ├── exprAffecterBlnSuiviTrue  [Expression shape]
    │       │     Code: gblnSuiviPresent = True
    │       │
    │       └── [On timeout — catch excpSuivi (TimeoutException SuiviTimeOut):]
    │             ├── exprAffecterBlnSuiviFalse: gblnSuiviPresent = False
    │             └── constrMsgInfoSuiviNull: creates dummy msgInfoSuivi = <Root/>
    │
    ├── ──── ALL 3 BRANCHES COMPLETE (received or timed out) ────
    │
    ├── decideTransaction  [Decision shape — 3 rules]
    │   │
    │   ├── ruleTrxComptCorln  (Rule 1 — HAPPY PATH: all 3 present)
    │   │   Condition: gblnCorreler AND gblnFichierPresent AND gblnSuiviPresent
    │   │   │
    │   │   ├── constrMsgInscrSuiviFichCorln  [Construct shape]
    │   │   │     Apply map: mapInscrSuiviFich
    │   │   │     Input: msgInfoFichMsg (file) + msgInfoSuivi (suivi)
    │   │   │     Output: msgReqInscrSuiviFichCorln (combined request)
    │   │   │
    │   │   ├── loopResilience  [WHILE loop: !gblnAppelServiceOK]
    │   │   │   │
    │   │   │   └── scopeResilience  [Atomic Transaction, Timeout: 60s]
    │   │   │       ├── sndReqInscrSuiviFichCorln  [SEND shape]
    │   │   │       │     Port: poInscrireSuiviFich
    │   │   │       │     Operation: InscrireSuiviFichCorln
    │   │   │       │     → SOAP call to OOA2_InscrireSuiviFich_Ws
    │   │   │       │
    │   │   │       ├── rcvRepInscrSuiviFichCorln  [RECEIVE shape]
    │   │   │       │     Receives response from SOAP service
    │   │   │       │
    │   │   │       ├── exprAppelServiceOK: gblnAppelServiceOK = True → exits loop
    │   │   │       │
    │   │   │       └── [On exception:]
    │   │   │             ├── exprJournaliserResilience: log to Windows Event Log
    │   │   │             └── suspResilience  [SUSPEND shape]
    │   │   │                   → Suspends orchestration for manual recovery
    │   │   │
    │   │   └── decideIndicExecOrcSpec  [Decision shape — 2 rules]
    │   │       │
    │   │       ├── If: msgInfoSuivi.Erreur == "0" AND msgInfoFichMsg.IndExecOrcSpec == "O"
    │   │       │   │  (file validation passed AND flow-specific processing requested)
    │   │       │   │
    │   │       │   ├── scopeConstrMsgSortie  [Atomic Transaction scope]
    │   │       │   │     Construct: msgGenSortie (output message)
    │   │       │   │     Content: ns0:ROOT with file content + NsMsgFichEntrant namespace
    │   │       │   │
    │   │       │   └── sndMsgGenSortie  [SEND shape]
    │   │       │         Port: poEnvoiMsgGenSortie
    │   │       │         → HTTP POST to BTSHTTPReceive.dll on BizTalk server
    │   │       │         → Published to MessageBox → HOA5 Integration subscribes and receives
    │   │       │
    │   │       └── Else: no send (validation error or tracking-only exchange)
    │   │
    │   ├── ruleTrxComptSuivi  (Rule 2 — ABANDONED: file + suivi, but NO correlation)
    │   │   Condition: gblnFichierPresent AND gblnSuiviPresent (gblnCorreler = False)
    │   │   │
    │   │   ├── constrMsgInscrSuiviFichAbandon  [Construct shape]
    │   │   │     Apply map → InscrireSuiviFichAbandon request
    │   │   │
    │   │   └── loopResilience  [Same retry loop as Rule 1]
    │   │         → SOAP call: InscrireSuiviFichAbandon (marks exchange as abandoned)
    │   │
    │   └── Else  (Rule 3 — PARTIAL: missing file or suivi)
    │       │
    │       └── decidePresenceEntrants  [Decision shape]
    │           ├── If gblnFichierPresent: send file msg to rejection FILE share
    │           ├── Elif gblnSuiviPresent: send suivi msg to rejection FILE share
    │           └── Else: set gstrNoEchg from correlation msg
    │
    └── exprJournaliser  [Expression shape — always runs]
          Log to Windows Event Log:
          "orcTrnsmFichEntrant: Fichier={gblnFichierPresent}, Suivi={gblnSuiviPresent},
           Correlation={gblnCorreler}, NoEchg={gstrNoEchg}"
```

> **Pour les développeurs juniors — ce que signifie « abandon » :** Lorsque l'application CH envoie le fichier (étape 2) et que les données de suivi arrivent, mais que l'application CH n'appelle jamais `CorrellerEnvoyer` (étape 3 — peut-être que l'utilisateur a fermé l'application, ou que le réseau a coupé), la branche de corrélation atteint le délai d'expiration après 9 minutes. L'orchestration enregistre cela comme un **échange abandonné** — le fichier a été reçu mais jamais confirmé. La méthode `InscrireSuiviFichAbandon` écrit les enregistrements de suivi dans Oracle avec un statut « abandonné » afin que l'équipe d'exploitation puisse investiguer.

> **Pour les développeurs juniors — ce que fait le patron « boucle de réessai + suspension » :** Le patron `loopResilience` / `scopeResilience` est un mécanisme de récupération d'erreur BizTalk. La boucle while continue d'appeler le service SOAP jusqu'à ce qu'il réussisse (`gblnAppelServiceOK = True`). Si l'appel SOAP lève une exception (service indisponible, erreur réseau), le gestionnaire d'exception (1) journalise dans le journal d'événements Windows, puis (2) **suspend** l'instance d'orchestration. Une orchestration suspendue reste dans la base de données MessageBox de BizTalk, conservant tous ses messages. Un opérateur peut ensuite **reprendre** l'instance (après la restauration du service SOAP), et la boucle réessaie. C'est la version BizTalk d'une file de messages non livrables avec récupération manuelle.

#### 1.5.3 Mécanisme de corrélation BizTalk (détaillé)

> **Cette section explique comment BizTalk achemine 3 messages distincts vers la même instance d'orchestration.**
**Le problème :** TDF Frontend envoie 3 requêtes HTTP/SOAP distinctes à 3 points d'entrée BizTalk différents (ports de réception). Chaque requête crée un message BizTalk indépendant dans le MessageBox. Comment BizTalk sait-il que ces 3 messages appartiennent au **même échange** et doivent être livrés à la **même** instance d'orchestration en cours d'exécution ?

**La solution : propriété promue + ensemble de corrélation**

```
Step 1: Property Promotion (defined in PropertySchema.xsd)
─────────────────────────────────────────────────────────────
   Each of the 3 message schemas (file, suivi, correlation) has an attribute named "NoEchg".
   BizTalk promotes this attribute into the message context (metadata) when the message
   enters the MessageBox. "Promoting" means: BizTalk extracts the value from the XML body
   and copies it into a special metadata header that the subscription engine can query.
   
   Example: message XML = <Root NoEchg="12345" .../>
            → BizTalk context property: PropertySchema.NoEchg = "12345"

Step 2: Correlation Set (defined in orchestration ODX)
─────────────────────────────────────────────────────────────
   Correlation Type:  corTypNoEchg
   Property:          OOA2_SchemMsg_bt.PropertySchema.NoEchg
   Correlation Set:   corNoEchg

   All 3 receive shapes declare: Correlation Set = corNoEchg, Initializes = True.
   
   "Initializes = True" means: when this receive gets a message, the correlation set is
   established with the NoEchg value from that message. BizTalk then uses this value
   to match future messages to this orchestration instance.

Step 3: How the convoy works at runtime
─────────────────────────────────────────────────────────────
   1. First message arrives (e.g., file message, NoEchg=12345)
      → No instance exists → Activate=True triggers new instance creation
      → Correlation set corNoEchg initialized with value "12345"
      → Instance creates subscriptions: "I want messages with NoEchg=12345
         on ports poRecptInfoSuivi and poRecptInfoCorln"
   
   2. Second message arrives (e.g., suivi message, NoEchg=12345)
      → BizTalk checks MessageBox subscriptions
      → Matches the subscription from step 1 → delivered to Branch 3 of that instance
   
   3. Third message arrives (e.g., correlation message, NoEchg=12345)
      → Matches the subscription from step 1 → delivered to Branch 1 of that instance
```

> **Important — il s'agit uniquement d'une corrélation d'initialisation (Initializing), PAS de suivi (Following) :**
> Dans BizTalk, il existe deux types de corrélation :
> - **Initializing** = l'ensemble de corrélation est établi lorsque le message arrive. Utilisé sur le premier receive d'une conversation.
> - **Following** = l'ensemble de corrélation a déjà été établi par un send ou receive précédent. Utilisé pour poursuivre une conversation existante.
>
> Cette orchestration utilise **uniquement** la corrélation Initializing sur les 3 receives. Il n'y a aucun send préalable qui établit une corrélation Following. Cela fonctionne parce que l'orchestration est un **convoy** — les 3 receives sont des receives activants concurrents dans un shape Parallel. L'implémentation du convoy de BizTalk gère le routage en interne, en utilisant la valeur de corrélation du premier message pour créer les abonnements pour les messages restants.

#### 1.5.4 Comment les étapes du protocole TDF correspondent aux branches de TDF Integration

Ce diagramme montre la relation précise entre les opérations de TDF Frontend et les branches de TDF Integration :

```
TDF Frontend (DestinataireEntrant.svc.vb)             TDF Integration (orcTrnsmFichEntrant.odx)
─────────────────────────────────────────             ──────────────────────────────────────────

Step 2: EnvoyerLotFichier()
│
├─ EnvoyerMessageServCourtage()                       BRANCH 2: scopeFichMsg
│   Transport: HttpClient.PostAsync()                   Port: poRecptInfoFichMsg  
│   Target: BTSHTTPReceive.dll (appSetting              Receive: rcvInfoFichMsg (Activate=True)
│           "UrlServCourtageEnvoyLot")                  Schema: schInfoFichMsgFichEntrant
│   Payload: <ns0:Fichier NoEchg="12345"                Promoted: NoEchg → corNoEchg
│              IndExecOrcSpec="O" ...>                   On OK: gblnFichierPresent = True
│              <FichMsg><Contenu>[ZIP]</Contenu>        On timeout (9 min): = False
│              </FichMsg></ns0:Fichier>    ──────────►
│
├─ objMessageFichierEntrant.Recevoir()
│   (HOA5 strategy: validates ZIP + XML,
│    builds SuiviMessage_DS + SuiviFichier_DS)
│
├─ EnvoyerSuiviServCourtage()                         BRANCH 3: scopeSuivi
│   Transport: SOAP proxy (auto-generated)              Port: poRecptInfoSuivi
│     Class: TrnsmInfoSuiviEntrantBtWS                  Receive: rcvInfoSuivi (Activate=True)
│     Method: opRecptInfoSuivi(dsSuiviFichBtWS)         Schema: schInfoSuiviFichEntrant
│   Target: BizTalk-published SOAP endpoint             Promoted: NoEchg → corNoEchg
│     (appSetting "UrlEnvoiFichEntrant")                On OK: gblnSuiviPresent = True
│   Payload: <Root NoEchg="12345" Erreur="0">          On timeout (9 min): = False
│              <SuiviFichier>[DS XML]</SuiviFichier>
│              <SuiviMessage>[DS XML]</SuiviMessage>
│            </Root>                       ──────────►
│
└─ Return accusé de réception to CH App

Step 3: CorrellerEnvoyer()
│
├─ EnvoyerCorrellerServCourtage()                     BRANCH 1: scopeCorln
│   Transport: SOAP proxy (auto-generated)              Port: poRecptInfoCorln
│     Class: TrnsmCorlnEntrantBtWS                      Receive: rcvInfoCorln (Activate=True)
│     Method: opRecptInfoCorln(objInfoCorln)            Schema: schInfoCorlnFichEntrant
│   Target: BizTalk-published SOAP endpoint             Promoted: NoEchg → corNoEchg
│     (appSetting "UrlEnvoiCorlnEntrant")               On OK: gblnCorreler = True
│   Payload: <Root NoEchg="12345" />                    On timeout (9 min): = False
│                                          ──────────►
│
└─ Return final confirmation to CH App

                                                      ─── AFTER ALL 3 BRANCHES ───
                                                      Decision: decideTransaction
                                                      (see §1.5.2 for full logic)
```

> **Point clé :** TDF Frontend envoie **2 messages durant l'étape 2** et **1 message durant l'étape 3**. Les messages utilisent deux mécanismes de transport différents : HTTP POST (pour le fichier — voir la section 1.4.2 pour les 5 raisons vérifiées dans le code source) et un proxy SOAP (pour le suivi et la corrélation, qui sont de petits payloads XML). Les appels du proxy SOAP ciblent des points d'accès WCF-BasicHttp publiés par BizTalk (fichiers `.svc`) — générés automatiquement par BizTalk à partir de ses ports de réception d'orchestration. Les classes proxy côté client utilisent le style ASMX `SoapHttpClientProtocol` pour la rétrocompatibilité, mais côté serveur c'est du WCF-BasicHttp. Voir l'explication « HTTP POST vs proxy SOAP » à la section 1.4.2.

#### 1.5.5 Configuration des ports physiques (Production)

> **Source :** `Prod.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml`

**Ports de réception (3 — comment les messages entrent dans BizTalk) :**

> **AVERTISSEMENT — Nom de port physique trompeur (vérifié dans le code source depuis le fichier de liaisons) :**
> Le port de réception physique `rcvPoOOA2_PoRecptInfoSuiviMsg` est lié au port logique `poRecptInfoFichMsg` — le port du **message fichier** (branche 2), **PAS** le port de suivi. Le nom indique « SuiviMsg » mais il reçoit en réalité le message fichier. Il s'agit d'une incohérence de nommage dans le fichier de liaisons BizTalk. Ne pas le confondre avec le véritable port de suivi (`rcvPoOOA2_poRecptInfoSuivi_Entrant` → port logique `poRecptInfoSuivi`).

> **Pourquoi 2 emplacements de réception par port de réception (c'est une architecture BizTalk normale) :**
> Chaque port de réception ci-dessous possède **2 emplacements de réception** — cela ne signifie PAS 2 ports séparés ni 2 branches. Un **port de réception** BizTalk est un conteneur logique qui peut contenir plusieurs **emplacements de réception** (adaptateurs). En production, un emplacement de réception est l'**adaptateur d'exécution** (HTTP ou WCF-BasicHttp) utilisé par TDF Frontend durant le fonctionnement normal. Le second est un **adaptateur FILE** qui interroge un dossier de reprise (`ReprisesBt2009\...`) — un opérateur peut y déposer un fichier XML pour réinjecter manuellement un message perdu ou corrompu. Les deux emplacements de réception alimentent le **même** port logique et donc la **même** branche d'orchestration.

| Port logique | Nom du port physique | Adaptateur d'exécution (fonctionnement normal) | Adresse d'exécution | Adaptateur de reprise (rejeu manuel) | Dossier de reprise |
|-|-|-|-|-|-|
| `poRecptInfoCorln` | `rcvPoOOA2_poRecptInfoCorln_Entrant` | **WCF-BasicHttp** (.svc — point d'accès SOAP publié par BizTalk) | URL publiée par BizTalk | FILE (`*.xml`, interrogation 60s) | `\\adprod.ramq.gov\...\RecptInfoCorln_Entrant\` |
| `poRecptInfoFichMsg` | `rcvPoOOA2_PoRecptInfoSuiviMsg` ⚠️ | **HTTP** (BTSHTTPReceive.dll) | chemin TDFAPP sur le serveur BizTalk | FILE (`*.xml`, interrogation 60s) | `\\adprod.ramq.gov\...\RecptInfoFichMsg_Entrant\` |
| `poRecptInfoSuivi` | `rcvPoOOA2_poRecptInfoSuivi_Entrant` | **WCF-BasicHttp** (.svc — point d'accès SOAP publié par BizTalk) | URL publiée par BizTalk | FILE (`*.xml`, interrogation 60s) | `\\adprod.ramq.gov\...\RecptInfoSuivi_Entrant\` |

> ⚠️ Le nom physique `rcvPoOOA2_PoRecptInfoSuiviMsg` est trompeur — il gère le message **fichier** (branche 2), PAS le message de suivi.

> **Pourquoi deux adaptateurs par port de réception ?** En fonctionnement normal, TDF Frontend envoie les messages via les adaptateurs WCF-BasicHttp ou HTTP (appels SOAP/HTTP en temps réel). L'adaptateur FILE est un **mécanisme de reprise manuelle** : un opérateur peut déposer un fichier XML dans le dossier partagé pour réinjecter un message qui a échoué ou été perdu. C'est un patron opérationnel BizTalk courant — une sauvegarde par fichier pour les emplacements de réception automatisés.

**Configuration des adaptateurs de réception (vérifié dans le code source) :**
- Interrogation FILE : intervalle 60s, taille de lot 20, lot maximum 100 Ko, 5 réessais en cas de défaillance réseau
- WCF-BasicHttp : sécurité `TransportCredentialOnly`, `clientCredentialType=Windows`, message maximum 10 Mo (`MaxReceivedMessageSize=10024000`), appels concurrents maximum 200, délai d'expiration 2 min

**Ports d'envoi (4 — messages sortants de l'orchestration) :**

| Port logique | Nom du port physique | Adaptateur | Adresse | Réessai | Objectif |
|-|-|-|-|-|-|
| `poInscrireSuiviFich` | `sndPoOOA2_poInscrireSuiviFich_Entrant` | **SOAP** | `http://srv-prod-seltrt01/.../FichEntrant.asmx` | Primaire : 3×5min, Secondaire : 1000×60min | Persister les enregistrements de suivi dans la BD Oracle TDF via `OOA2_InscrireSuiviFich_Ws` |
| `poEnvoiMsgGenSortie` | `sndPoOOA2_poEnvoiMsgGenSortie_Entrant` | **HTTP** | `http://srv-prod-biztrt01/.../BTSHTTPReceive.dll` | 3×5min | Acheminer le fichier vers HOA5 Integration (via MessageBox) |
| `poEnvoiInfoFichMsgRejet` | `sndPoOOA2_poEnvoiInfoFichMsgRejet_Entrant` | **FILE** | `\\adprod.ramq.gov\...\Rejet\Entrant\Entrant_InfoFichMsgRejet_%MessageID%.xml` | 3×5min | Déposer les messages fichier rejetés pour examen manuel |
| `poEnvoiInfoSuiviRejet` | `sndPoOOA2_poEnvoiInfoSuiviRejet_Entrant` | **FILE** | `\\adprod.ramq.gov\...\Rejet\Entrant\Entrant_InfoSuiviRejet_%MessageID%.xml` | 3×5min | Déposer les messages de suivi rejetés pour examen manuel |

> **Critique : le port d'envoi de suivi possède un réessai secondaire agressif** — 1000 réessais à intervalles de 60 minutes (~41 jours). C'est un choix de conception délibéré : les enregistrements de suivi constituent la **piste d'audit** de chaque transmission. La conception privilégie l'exhaustivité de l'audit plutôt que la résolution rapide — l'instance d'orchestration reste active et réessaie jusqu'à ce qu'elle puisse persister l'enregistrement de suivi, même si cela prend des semaines.

#### 1.5.6 Inventaire des fonctionnalités BizTalk

Ce tableau répertorie chaque fonctionnalité de BizTalk Server utilisée par l'orchestration TDF Integration, avec une description vérifiée dans le code source de son utilisation et de ses implications pour la migration :

| # | Fonctionnalité BizTalk | Utilisation dans TDF Integration | Ce que ça fait (pour les développeurs juniors) | Risque |
|-|-|-|-|-|
| 1 | **Orchestration** | `orcTrnsmFichEntrant.odx` — toute la logique de routage de messages, de corrélation et de décision | Un moteur de flux de travail visuel (comme un organigramme) que BizTalk exécute. Chaque shape dans le fichier ODX est une étape. L'orchestration est compilée en assemblage .NET et exécutée par le processus hôte BizTalk. | ~25 shapes — modérément complexe ; toute la logique de routage est compilée dans cet unique artefact. |
| 2 | **MessageBox** | Base de données centrale de routage de messages. Le message fichier entre via le HTTP receive → publié dans le MessageBox → routage par abonnement → la branche 2 le reçoit. Le fichier de sortie est publié dans le MessageBox → HOA5 Integration s'y abonne. | Le cœur de BizTalk : une base de données SQL Server où TOUS les messages sont stockés. Les orchestrations ne reçoivent pas directement les messages — elles les reçoivent du MessageBox via des abonnements (comme un modèle sujet/abonnement). | Dépendance centrale — fournit la persistance des messages (récupération après panne) et le filtrage par abonnement. Tout le routage de messages dépend de ce composant unique. |
| 3 | **Parallel Activating Convoy** | 3 shapes receive avec `Activate=True` dans un shape Parallel, tous initialisant le même ensemble de corrélation `corNoEchg` | Un patron BizTalk spécifique où plusieurs receives concurrents peuvent chacun démarrer l'orchestration. Le premier message crée l'instance ; les autres branches attendent leurs messages. Intégré à BizTalk — aucun code personnalisé requis. | C'est le patron le plus complexe de la solution. Doit gérer la sémantique « n'importe lequel des 3 peut arriver en premier » et corréler 3 messages indépendants en un seul échange. |
| 4 | **Ensemble de corrélation** | `corTypNoEchg` basé sur la propriété promue `PropertySchema.NoEchg`. Initializing=True sur les 3 receives. | Achemine les messages vers la bonne instance d'orchestration en se basant sur une valeur extraite du XML. Comme un « identifiant de session » qui regroupe les messages connexes. | Mécanisme de routage central — sans lui, les messages ne peuvent pas être associés aux échanges. |
| 5 | **Promotion de propriété** | `PropertySchema.xsd` déclare `NoEchg` comme propriété promue. BizTalk l'extrait des attributs XML des 3 schémas et l'écrit dans le contexte du message. | Rend une valeur contenue dans le corps XML disponible en tant que métadonnée interrogeable. Sans la promotion, BizTalk devrait analyser le corps de chaque message pour trouver des correspondances — la promotion rend la correspondance O(1). | Essentiel pour la corrélation — `NoEchg` doit être extractible des 3 types de messages. |
| 6 | **Transaction longue durée** | scope externe `scopeRecvrFichEntrant`, délai d'expiration 10 minutes. Permet à l'orchestration de se déshydrater (persister en BD) et de se réhydrater au redémarrage de l'hôte. | BizTalk peut sauvegarder l'état d'une orchestration en cours d'exécution dans SQL Server et le restaurer ultérieurement. Si le serveur BizTalk redémarre, l'orchestration reprend là où elle s'était arrêtée. | Fournit la récupération après panne — les échanges en cours survivent aux redémarrages de serveur. Le délai d'expiration de 10 minutes est le temps maximum d'horloge murale pour l'ensemble de l'échange. |
| 7 | **Délai d'expiration au niveau du scope** | Chacune des 3 branches : `scopeCorln` (9 min), `scopeFichMsg` (9 min), `scopeSuivi` (9 min). Le délai d'expiration lève une `TimeoutException`. | Chaque branche a sa propre échéance. Si le message attendu n'arrive pas dans les 9 minutes, BizTalk lève une exception de délai d'expiration dans cette branche. Le gestionnaire d'exception met l'indicateur booléen à `false`. | Définit le couplage temporel — si un message est en retard de plus de 9 min, l'échange passe à l'état abandonné ou partiel. |
| 8 | **Shape Decision** | `decideTransaction` avec 3 règles (tout-vrai, fichier+suivi, sinon). `decideIndicExecOrcSpec` avec 2 règles (Erreur="0" ET IndExecOrcSpec="O"). `decidePresenceEntrants` pour le routage de rejet. | Branchement conditionnel standard de type si/sinon_si/sinon sur des conditions booléennes et chaînes de caractères. | Faible complexité — branchement conditionnel standard. |
| 9 | **Transform (Map)** | `mapInscrSuiviFich.btm` — fusionne le message fichier + le message de suivi en une seule requête `InscrireSuiviFichCorln` pour le service SOAP de suivi. | Prend 2 messages en entrée et produit 1 message en sortie en faisant correspondre les champs. Le fichier `.btm` se compile en XSLT. | Format `.btm` spécifique à BizTalk — compilé en XSLT. Le mappage exact des champs doit être examiné. |
| 10 | **Transaction atomique** | `scopeConstrMsgSortie` (scope interne, délai d'expiration 60s). `scopeResilience` (scope interne pour le réessai SOAP). | Atomique = isolation sérialisable + support des transactions compensatoires. En pratique, utilisé ici pour s'assurer que la construction du message n'est pas interrompue. | Risque pratique faible — utilisé pour la construction de messages, qui est intrinsèquement atomique. |
| 11 | **Boucle de réessai** | `loopResilience` : tant que `!gblnAppelServiceOK` → appeler SOAP → en cas de succès : sortir. En cas d'exception : journaliser + suspendre. | Une boucle « tant que » qui continue d'essayer jusqu'à ce que l'appel SOAP réussisse. En cas d'échec, elle suspend (met en pause) l'orchestration et attend qu'un opérateur la reprenne. | Le comportement de « suspension » signifie qu'une intervention manuelle est nécessaire en cas d'échec du service SOAP. Aucune récupération automatisée — repose sur l'opérateur. |
| 12 | **Shape Suspend** | `suspResilience` — suspend l'instance d'orchestration après un échec d'appel SOAP. L'instance demeure dans la base de données MessageBox avec son état intact. | « Suspendre » = mettre en pause le flux de travail et alerter un opérateur. L'instance d'orchestration n'est PAS supprimée — elle peut être reprise manuellement. C'est la façon dont BizTalk gère les défaillances transitoires irrécupérables. | Comportement clé : l'échange ne doit PAS être perdu en cas d'échec répété ; il doit être récupérable. Les instances suspendues nécessitent une intervention de l'opérateur. |
| 13 | **Adaptateur FILE** | Les ports de rejet déposent les messages sous forme de fichiers XML dans `\\adprod.ramq.gov\...\Rejet\Entrant\`. Les emplacements de réception FILE servent à la reprise/rejeu manuelle. | L'adaptateur FILE lit et écrit sur des partages de fichiers Windows. Utilisé tant pour le rejet (sortie) que pour la reprise (entrée). | Dépend des partages de fichiers Windows (SMB) — dépendance d'infrastructure. Processus de reprise manuelle. |
| 14 | **Adaptateur SOAP** | Le port d'envoi `poInscrireSuiviFich` appelle `OOA2_InscrireSuiviFich_Ws.InscrireSuiviFichCorln` via SOAP. Liaison : BasicAuth, réessai primaire 3×5min, réessai secondaire 1000×60min. | L'adaptateur SOAP intégré de BizTalk pour appeler les services web ASMX. Gère la construction de l'enveloppe SOAP, l'authentification et les réessais. | Le réessai secondaire agressif (1000×60min ≈ 41 jours) garantit l'exhaustivité de la piste d'audit mais maintient l'instance d'orchestration active pendant très longtemps. |
| 15 | **Adaptateur HTTP** | `poEnvoiMsgGenSortie` envoie le fichier à `BTSHTTPReceive.dll` sur un autre serveur BizTalk, qui publie dans le MessageBox pour HOA5 Integration. Également utilisé pour la réception entrante du message fichier. | L'adaptateur HTTP de BizTalk pour l'envoi et la réception de messages HTTP. À la réception, `BTSHTTPReceive.dll` est un filtre ISAPI IIS qui publie les messages dans le MessageBox. | Routage interne via `BTSHTTPReceive.dll` — un intermédiaire qui publie dans le MessageBox pour le routage inter-applications. |
| 16 | **Adaptateur WCF-BasicHttp** | 2 emplacements de réception pour les messages de suivi et de corrélation. Sécurité : `TransportCredentialOnly`, `clientCredentialType=Windows`. | Reçoit des messages SOAP via HTTP avec authentification Windows. Utilisé pour les points d'accès SOAP publiés par BizTalk que TDF Frontend appelle. | Authentification Windows uniquement (Kerberos/NTLM) — nécessite une infrastructure de domaine Windows. |
| 17 | **Journal d'événements Windows** | `exprJournaliser` écrit des messages de diagnostic dans le journal d'événements Windows (source « TDF »). Journalise NoEchg, valeurs des indicateurs, résultat du traitement. | Mécanisme de journalisation simple. BizTalk écrit dans le journal d'événements Windows visible dans l'Observateur d'événements. | Observabilité limitée — les journaux sont locaux à chaque serveur BizTalk, non centralisés. Pas de journalisation structurée, pas de corrélation entre les composants. |

#### 1.5.7 Schémas de messages

> **Source :** projet `OOA2_SchemMsg_bt`

| Schéma | Espace de noms | Utilisé par | Éléments clés |
|-|-|-|-|
| `schInfoFichMsgFichEntrant.xsd` | `http://OOA2_SchemMsg_bt.schInfoFichMsgFichEntrant` | Branche 2 (message fichier) | `NoEchg`, `NsMsgFichEntrant`, `IndExecOrcSpec`, `IndValContFichEntre`, `FichMsg/Contenu` (ZIP en Base64) |
| `schInfoSuiviFichEntrant.xsd` | `http://OOA2_SchemMsg_bt.schInfoSuiviFichEntrant` | Branche 3 (message de suivi) | `NoEchg`, `Erreur` ("0"=OK, "1"=erreur), `SuiviFichier` (XML embarqué), `SuiviMessage` (XML embarqué) |
| `schInfoCorlnFichEntrant.xsd` | `http://OOA2_SchemMsg_bt.schInfoCorlnFichEntrant` | Branche 1 (message de corrélation) | `NoEchg` (seul élément — jeton de corrélation pur) |
| `PropertySchema.xsd` | `http://OOA2_SchemMsg_bt.PropertySchema.PropertySchema` | Moteur de corrélation | `NoEchg` (propriété promue — extraite du XML du message dans le contexte BizTalk pour le routage par abonnement) |

#### 1.5.8 Variables de l'orchestration

| Variable | Type | Valeur initiale | Objectif |
|-|-|-|-|
| `gblnCorreler` | Boolean | `true` | Résultat de la branche de corrélation. Mis à `false` UNIQUEMENT en cas de délai d'expiration. |
| `gblnFichierPresent` | Boolean | `true` | Résultat de la branche fichier. Mis à `false` UNIQUEMENT en cas de délai d'expiration. |
| `gblnSuiviPresent` | Boolean | `true` | Résultat de la branche de suivi. Mis à `false` UNIQUEMENT en cas de délai d'expiration. |
| `gblnAppelServiceOK` | Boolean | `false` | Indicateur de sortie de la boucle de réessai. Mis à `true` lorsque l'appel SOAP réussit. |
| `gstrNoEchg` | String | `"0000"` | Numéro d'échange — utilisé uniquement pour la journalisation/diagnostic. |
| `gstrJournalisation` | String | — | Tampon de texte du message du journal d'événements. |

> **Note sur les valeurs initiales (vérifié dans le code source) :** Les 3 indicateurs d'arrivée sont initialisés à `true`. Ils ne sont mis à `false` **que** dans le gestionnaire d'exception de délai d'expiration. Si un message arrive normalement, le shape Expression met l'indicateur à `true` — confirmant la valeur initiale. Ce patron « vrai par défaut, remplacé en cas de délai d'expiration » signifie que le shape Decision fonctionne correctement quel que soit l'ordre d'arrivée des branches.

#### 1.5.9 Condition de routage — TDF Integration vers HOA5 Integration

> **Cette section documente le mécanisme de routage qui détermine comment un message traité par TDF atteint le traitement spécifique à HOA5.** C'est une préoccupation critique pour la migration — l'état cible doit remplacer ce routage implicite à 2 sauts par un mécanisme explicite et déclaratif. Voir la Section 3.4 de l'état cible pour le remplacement Azure.

Le routage de TDF Integration vers HOA5 Integration est un **mécanisme implicite à 2 sauts** — il n'y a pas de propriété ou configuration unique « router vers HOA5 ». Le routage est divisé entre deux mécanismes indépendants :

**Saut 1 — Envoi conditionnel par l'orchestration TDF Integration :**

Après que les 3 branches du convoy se complètent avec succès (Règle 1 — chemin nominal), l'orchestration TDF Integration (`orcTrnsmFichEntrant`) évalue un shape Decision (`decideIndicExecOrcSpec`) :

```
IF msgInfoSuivi.Erreur == "0" AND msgInfoFichMsg.IndExecOrcSpec == "O"
    → Construire le message de sortie (msgGenSortie : ns0:ROOT avec contenu fichier + NsMsgFichEntrant)
    → Envoyer via HTTP Adapter → BTSHTTPReceive.dll (port : poEnvoiMsgGenSortie → sndPoOOA2_poEnvoiMsgGenSortie_Entrant)
    → Message publié dans le MessageBox BizTalk
SINON
    → Pas d'envoi (échange de suivi uniquement ou erreur de validation — le fichier N'EST PAS routé vers HOA5)
```

Le discriminateur de routage à ce saut est **`IndExecOrcSpec`** — un champ dans le schéma de message fichier (`schInfoFichMsgFichEntrant.xsd`) :

| Champ | Valeur | Signification |
|-------|--------|---------------|
| `IndExecOrcSpec` | `"O"` | Exécuter l'orchestration spécifique au système (router le fichier vers HOA5 Integration) |
| `IndExecOrcSpec` | Autre | NE PAS router — échange de suivi uniquement ou flux ne nécessitant pas de traitement spécifique au système |

Cet indicateur est défini par le TDF Frontend lors de l'Étape 2 (`EnvoyerLotFichier`) lors de la construction du XML du message fichier. Ce n'est **pas une propriété promue BizTalk** — il est évalué dans le shape Decision via une expression XPath sur le message reçu.

**Saut 2 — Abonnement MessageBox par HOA5 Integration :**

L'orchestration HOA5 Integration (`OrcServTransmPrel`) possède un port de réception à liaison directe `poRcvFichierCH` avec `Activate=True`. Lorsque BizTalk déploie cette orchestration, il enregistre automatiquement un **filtre d'abonnement** dans la base de données MessageBox :

- **`BTS.MessageType`** correspond au schéma du port : `http://RAMQ.HO.HOA5_ServTransmPrel_bt.SchFichCHSpec` (le `targetNamespace` de `SchFichCHSpec.xsd` — le schéma de message fichier spécifique à HOA5)
- **Portée d'application BizTalk** = `MEC_HOA5` — l'abonnement est enregistré dans le contexte de l'application BizTalk HOA5

Tout message correspondant à ce type de schéma publié dans le MessageBox dans la portée de l'application `MEC_HOA5` est automatiquement livré à une nouvelle instance de l'orchestration HOA5 Integration.

> **Pourquoi ce mécanisme est implicite et fragile :**
> - La condition de routage est **répartie sur deux couches** : un indicateur conditionnel dans le code de TDF Integration et un abonnement basé sur le schéma dans le MessageBox BizTalk.
> - Le filtre d'abonnement n'est **jamais visible dans le code applicatif** — il est généré par BizTalk au moment du déploiement en fonction de la configuration de liaison du port de l'orchestration.
> - Il n'y a **pas de propriété explicite « Consumer = HOA5 »** — le routage est implicite via le type de schéma du message et la frontière de l'application BizTalk.
> - Pour comprendre le routage, un développeur doit corréler : (1) la vérification `IndExecOrcSpec` dans l'orchestration TDF, (2) l'envoi HTTP Adapter vers `BTSHTTPReceive.dll`, (3) l'abonnement MessageBox pour `SchFichCHSpec` dans `MEC_HOA5`, et (4) le port de réception à liaison directe `poRcvFichierCH` dans HOA5 Integration.
> - Ce mécanisme implicite est **remplacé dans l'état cible** par un filtre SQL explicite (`Consumer = 'SSFU'`) sur l'abonnement de rubrique Service Bus `sub-hoa5`, avec `ForwardTo` vers `hoa5-queue`. Voir la Section 3.4 de l'état cible.

---

### 1.6 Connecteurs et adaptateurs

La solution est composée de cinq composants déployables indépendamment, appartenant à deux équipes.

Les cinq composants applicatifs legacy de cette section — **HOA5 Frontend, TDF Frontend, TDF Integration, HOA5 Integration et HOA5 Backend** — sont **déployés sur des machines virtuelles Azure IaaS** dans l'état actuel. La colonne d'hébergement ci-dessous distingue l'**emplacement logique d'exécution** (hébergé sur IIS versus hébergé dans BizTalk), et non une plateforme d'infrastructure différente.

| Composant | Équipe | Hébergement | Dépôt | Notes |
|-----------|--------|-------------|-------|-------|
| **HOA5 Frontend** | HOA5 | **Hors BizTalk** — service WCF (IIS sur VM Azure IaaS) | `MEC-AlimenterBanque` | **Service WCF qui héberge TDF Frontend** via héritage de classe (`FichDestinataireEntrant` hérite de `DestinataireEntrant`). **Contrat verrouillé** — l'application CH ne change pas. |
| **TDF Frontend** | TDF | **Hors BizTalk** — classe de base hébergée dans le service WCF HOA5 Frontend (IIS sur VM Azure IaaS) | `TDF-OOA2-Serv-Tranf-Fich` | Classe de base `DestinataireEntrant` — toute la logique du protocole y réside. Envoie 3 messages à TDF Integration (BizTalk) : fichier (HTTP POST), suivi (proxy SOAP), corrélation (proxy SOAP). Partagé entre tous les flux. |
| **TDF Integration** | TDF | **Dans BizTalk** — projet BizTalk partagé sur VM Azure IaaS | `TDF-OOA2-Serv-Tranf-Fich` | Orchestration `orcTrnsmFichEntrant`. Parallel Activating Convoy (3 branches). 3 ports de réception + 4 ports d'envoi. Achemine vers HOA5 Integration. |
| **HOA5 Integration** | HOA5 | **Dans BizTalk** — projet BizTalk spécifique à HOA5 sur VM Azure IaaS | `MEC-AlimenterBanque` | Orchestration `OrcServTransmPrel`. Map `transformerMsgFichCHToMsgSVC.btm`. Port d'envoi WCF-Custom avec `TranfBasic2IntegBehaviorExtn`. |
| **HOA5 Backend** | HOA5 | **Hors BizTalk** — point d'accès WCF (IIS sur VM Azure IaaS) | `MEC-AlimenterBanque` | Classe `ServTransmPrel` → `TraiterTransmPrel`. Traitement : (1) ZIP→DepoApli, (2) décompression, (3) XML→DataSets, (4) INSERT Oracle avec transaction explicite. **Peut être modifié si justifié.** |

#### 1.6.1 Ports BizTalk — HOA5 Integration (confirmé depuis le fichier de liaisons)

- **Réception :** 1 port de réception (`poTypRcvFichierCH`, unidirectionnel) — reçoit le message fichier de TDF Integration via le MessageBox BizTalk.
- **Envoi :** 1 port d'envoi (`sndPoHOA5_poSvcTrnsPrelCH`, bidirectionnel, adaptateur WCF-Custom) ciblant `http://srv-prod-seltrt01/MECAPP/.../HOA5_ServTransmPrel_svc/ServTransmPrel.svc`.
  - Transport : `basicHttpBinding`, `clientCredentialType=Windows`.
  - Comportement de point d'accès personnalisé : **`TranfBasic2IntegBehaviorExtn`** — une extension de comportement WCF personnalisée (bibliothèque : `COD3_V2InteropWsdl_cpo`, déployée dans le GAC, source HORS des dépôts en portée) qui agit comme un **credential bridge** : l'envoi sortant BizTalk utilise `clientCredentialType="Windows"` (identité du compte de service BizTalk), tandis que HOA5 Backend attend `clientCredentialType="Basic"`. Le comportement intercepte et transforme les informations d'identification de manière transparente. C'est pourquoi l'adaptateur **WCF-Custom** a été choisi plutôt que l'adaptateur standard WCF-BasicHttp — WCF-BasicHttp ne possède pas de propriété `EndpointBehaviorConfiguration` pour les comportements personnalisés.
  - Pipeline : `PassThruTransmit` (envoi) / `XMLReceive` (réponse).
  - Réessai : 3 réessais, intervalle de 5 minutes.
  - Hôte BizTalk : `HOST_ORCHESTRATION_MEC` (orchestration), `HOST_MEC_SEND` (gestionnaire d'envoi).
  - Nom de l'application BizTalk : `MEC_HOA5`.

#### 1.6.2 Ports BizTalk — TDF Integration (confirmé depuis le fichier de liaisons)

- **3 ports de réception :** `rcvPoOOA2_poRecptInfoCorln_Entrant`, `rcvPoOOA2_PoRecptInfoSuiviMsg`, `rcvPoOOA2_poRecptInfoSuivi_Entrant`.
- **4 ports d'envoi :** persistance du suivi (`sndPoOOA2_poInscrireSuiviFich_Entrant`, adaptateur SOAP → `OOA2_InscrireSuiviFich_Ws`), routage du fichier (`sndPoOOA2_poEnvoiMsgGenSortie_Entrant`, adaptateur HTTP → `BTSHTTPReceive.dll`), 2 ports de rejet.
- Hôte BizTalk : `HOST_ORCHESTRATION_TDF`, gestionnaire d'envoi : `HOST_TDF_SEND`.
- Nom de l'application BizTalk : `TDF_OOA2`.

#### 1.6.3 Inventaire combiné des ports — TDF Integration + HOA5 Integration

> **Source :** `Prod.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` (TDF Integration) et `Prod.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` (HOA5 Integration).

**Ports de réception (entrants — comment les messages entrent dans BizTalk) :**

| Composant | Port logique | Nom du port physique | Adaptateur | Source | Objectif |
|-----------|-------------|---------------------|------------|--------|----------|
| TDF Integration | `poRecptInfoFichMsg` | `rcvPoOOA2_PoRecptInfoSuiviMsg` ⚠️ | **HTTP** (`BTSHTTPReceive.dll`) + FILE (reprise) | TDF Frontend `EnvoyerMessageServCourtage()` | Reçoit le **message fichier** (XML compressé, jusqu'à 20 Mo) — branche 2. ⚠️ Le nom physique indique « SuiviMsg » mais gère le message **fichier** — incohérence de nommage dans le fichier de liaisons. |
| TDF Integration | `poRecptInfoSuivi` | `rcvPoOOA2_poRecptInfoSuivi_Entrant` | **WCF-BasicHttp** (.svc) + FILE (reprise) | TDF Frontend `EnvoyerSuiviServCourtage()` | Reçoit le **message de suivi** — branche 3 |
| TDF Integration | `poRecptInfoCorln` | `rcvPoOOA2_poRecptInfoCorln_Entrant` | **WCF-BasicHttp** (.svc) + FILE (reprise) | TDF Frontend `EnvoyerCorrellerServCourtage()` | Reçoit le **message de corrélation** — branche 1 |
| HOA5 Integration | `poRcvFichierCH` | `poTypRcvFichierCH` | **MessageBox** (liaison directe, unidirectionnel) | TDF Integration (via pub/sub du MessageBox) | Reçoit le **message fichier** acheminé depuis TDF Integration après la règle 1 (chemin nominal) |

**Ports d'envoi (sortants — comment les messages sortent de BizTalk) :**

| Composant | Port logique | Nom du port physique | Adaptateur | Cible | Réessai | Objectif |
|-----------|-------------|---------------------|------------|-------|---------|----------|
| TDF Integration | `poInscrireSuiviFich` | `sndPoOOA2_poInscrireSuiviFich_Entrant` | **SOAP** | `OOA2_InscrireSuiviFich_Ws` sur `srv-prod-seltrt01` | 3×5min (primaire), 1000×60min (secondaire) | Persister les enregistrements de suivi dans la BD Oracle TDF |
| TDF Integration | `poEnvoiMsgGenSortie` | `sndPoOOA2_poEnvoiMsgGenSortie_Entrant` | **HTTP** | `BTSHTTPReceive.dll` sur `srv-prod-biztrt01` | 3×5min | Acheminer le fichier validé vers HOA5 Integration (via MessageBox) |
| TDF Integration | `poEnvoiInfoFichMsgRejet` | `sndPoOOA2_poEnvoiInfoFichMsgRejet_Entrant` | **FILE** | `\\adprod.ramq.gov\...\Rejet\Entrant\` | 3×5min | Déposer les messages **fichier** rejetés pour examen manuel |
| TDF Integration | `poEnvoiInfoSuiviRejet` | `sndPoOOA2_poEnvoiInfoSuiviRejet_Entrant` | **FILE** | `\\adprod.ramq.gov\...\Rejet\Entrant\` | 3×5min | Déposer les messages de **suivi** rejetés pour examen manuel |
| HOA5 Integration | `poSndFichierCH` | `sndPoHOA5_poSvcTrnsPrelCH` | **WCF-Custom** (basicHttpBinding) | `ServTransmPrel.svc` sur `srv-prod-seltrt01` | 3×5min | Appeler HOA5 Backend `EnregistrerDonneesTrnsmCH` (requête-réponse) |

> **Pour les développeurs juniors — types d'adaptateurs expliqués :**
> - **HTTP** (`BTSHTTPReceive.dll`) : filtre ISAPI IIS ; accepte les POST HTTP bruts, publie le XML dans le MessageBox. Pas de SOAP.
> - **WCF-BasicHttp** (`.svc`) : point d'accès SOAP publié par BizTalk ; accepte les messages SOAP via HTTP avec authentification Windows.
> - **WCF-Custom** : adaptateur BizTalk standard avec extensibilité complète (liaisons personnalisées + comportements de point d'accès personnalisés). Utilisé ici pour le credential bridge.
> - **SOAP** : adaptateur intégré de BizTalk pour appeler des services web ASMX externes.
> - **FILE** : lit et écrit sur des partages de fichiers Windows. Utilisé pour le rejet (sortie) et la reprise manuelle (entrée).
> - **MessageBox** (liaison directe) : routage interne BizTalk — le port lit depuis le MessageBox BizTalk via un filtre d'abonnement, pas depuis un point d'accès externe.

#### 1.6.4 Implications clés pour la migration

- **HOA5 Frontend — contrat verrouillé :** L'hypothèse du projet est de ne pas modifier l'application CH. Tout remplacement doit exposer l'interface `IDestinataireEntrant` identique (mêmes 3 opérations, même contrat WCF).
- **TDF Integration et HOA5 Integration — doivent être replateformés :** Les deux s'exécutent dans BizTalk. La suppression de BizTalk est l'**objectif principal de migration** de ce projet. Ils ne peuvent pas simplement être « modifiés » — ils doivent être réimplémentés sur Azure (patron Enterprise Message Transit / EMT). Les changements à TDF Integration nécessitent une coordination avec l'équipe `TDF-OOA2-Serv-Tranf-Fich` car il est partagé entre les flux.
- **TDF Frontend et HOA5 Backend — peuvent être modifiés si bien justifié :** Les deux s'exécutent hors BizTalk. Ils peuvent être refactorisés, réimplémentés ou remplacés lorsqu'il existe une justification solide et fondée sur des preuves (obsolescence, risque de sécurité/conformité, impact de migration disproportionné, ou valeur claire à long terme) — approuvée par le Comité d'architecture.
- Le **HOA5 Backend** est la frontière de persistance — il effectue l'écriture DepoApli (première étape) et l'insertion Oracle transactionnelle. Les deux comportements doivent être préservés lors de la migration, mais l'implémentation peut changer.
- Le **TDF Frontend** est une infrastructure partagée réutilisée dans tous les flux PPP/SFU — les changements nécessitent une coordination avec l'équipe `TDF-OOA2-Serv-Tranf-Fich`.

#### 1.6.5 Comportement transactionnel — HOA5 Backend — Confirmé depuis le code source

> **Source :** Vérifié dans le code (`TraiterTransmPrel.EnregistrerDonneesTrnsmCH`, `HOA5_ServTransmPrel_cpo`).

HOA5 Backend traite une seule opération : `EnregistrerDonneesTrnsmCH`. Lorsqu'elle est appelée, elle exécute la séquence suivante :

1. **Sauvegarde vers DepoApli** — `SauvegarderFichierRecu` : décode le contenu du fichier en Base64 et écrit le fichier ZIP binaire vers le chemin UNC DepoApli (configuré comme `RprtDepoFich` dans `web.config` : `\\adprod.ramq.gov\appli\DonneAppli\{Envir}\Mec\MEC_LIV00\Recu\HOA5_RecvrTrnsmPrel`). Le fichier est nommé `{NoEchgFich}.zip`.
2. **Décompression** — `ObtenirFichierXmlRecu` : décompresse le ZIP et extrait le document XML.
3. **Chargement du XML dans les DataSets** — `RemplirInfosFichierXmlRecu` : analyse le XML en 3 DataSets typés correspondant aux 3 entités de données HOA5 : `MEC_TRANSM_PRELIM_ADMISSION`, `MEC_TRANSM_PRELIM_SOIN_ALTRN`, `MEC_TRANSM_PRELIM_SOIN_INTSF`.
4. **Insertion dans Oracle** — `InsererDonneesFich` : ouvre une connexion via `ODP.NET` en utilisant le fichier UDL (`MEC_ORA.UDL`), démarre une transaction explicite (`cnnOra.DebuterTransaction()`), et insère toutes les lignes des 3 DataSets. **COMMIT** (`cnnOra.TerminerTransaction()`) est appelé **uniquement si le nombre total de lignes insérées est égal au total des lignes dans les 3 DataSets**. Si une ligne échoue ou que le décompte est insuffisant, la transaction n'est jamais confirmée — la connexion Oracle est fermée dans le bloc `Finally`, provoquant un **rollback implicite**. Retourne un code de retour (`CodRetou` : `OUI` en cas de succès, `NON` sinon).

> **Ordre de traitement — vérifié dans le code source (confirmé depuis `TraiterTransmPrel.vb`) :** Le fichier est écrit vers DepoApli **en premier** (étape 1), bien avant que la transaction Oracle ne soit ouverte (étape 4). C'est l'ordre vérifié. Toute affirmation prétendant que l'écriture vers DepoApli se produit après le commit Oracle est **incorrecte** — le code source ne fait pas cela.

> **Ce que signifie « non couplé transactionnellement » — expliqué pour les développeurs juniors :**
> Imaginez que vous avez deux verrous distincts : un sur votre classeur (DepoApli) et un sur votre base de données (Oracle). HOA5 Backend ouvre le classeur, y dépose le fichier ZIP, et ferme le verrou — **fait, pas de retour en arrière**. Ce n'est qu'après cela qu'il ouvre le verrou de la base de données (transaction Oracle), insère les lignes, et le referme. Il n'y a **aucun verrou englobant** les deux. Si l'étape de la base de données échoue, le classeur est déjà fermé — le fichier ZIP y reste. En termes techniques : il n'y a pas de transaction distribuée (pas de MSDTC, pas de protocole XA) coordonnant les deux écritures comme une seule unité atomique. Ce sont deux opérations d'E/S indépendantes exécutées en séquence.

> **Comportement transactionnel de l'état actuel — observations clés :**
> 1. **L'écriture vers DepoApli se produit AVANT Oracle.** Le fichier est écrit vers le partage de fichiers en premier, puis l'insertion Oracle s'exécute. Ces deux étapes ne sont pas couplées de manière atomique.
> 2. **Aucun rollback de l'écriture DepoApli n'est possible.** Si Oracle échoue, le fichier ZIP est déjà sur DepoApli. Le nom de fichier (`{NoEchgFich}.zip`) serait écrasé lors d'un réessai (idempotent par le nom), mais aucun processus de nettoyage automatisé n'a été identifié.
> 3. **Le commit Oracle est tout-ou-rien au niveau du fichier.** `TerminerTransaction()` est appelé uniquement lorsque le décompte de lignes dans les 3 DataSets est complet. Les insertions partielles ne sont jamais confirmées — la connexion se ferme avec un rollback implicite.
> 4. **Ces deux comportements réunis signifient** : un seul appel à `EnregistrerDonneesTrnsmCH` se termine soit par (a) écriture ZIP + commit Oracle = succès complet, (b) écriture ZIP + échec Oracle = fichier orphelin, ou (c) échec DepoApli = rien de persisté, opération échouée. Le scénario (b) est le cas limite dangereux — le fichier existe mais Oracle n'en a aucune trace. **Risque :** Aucun mécanisme automatisé identifié pour détecter ou gérer les fichiers orphelins.

---

### 1.7 Contexte de la solution et moteur de migration

HOA5 est une **solution d'intégration basée sur BizTalk** responsable de l'orchestration et de la médiation des échanges de données entre les centres hospitaliers (CH), la base de données opérationnelle de la RAMQ (Oracle) et les services de traitement en aval. Elle gère les **transmissions préliminaires** (code HOA5) — des fichiers XML envoyés quotidiennement pendant que le patient est encore en soins. Le flux de transmission régulière partage des composants d'infrastructure TDF communs mais est hors de la portée de ce document.

La solution s'intègre avec :
- Des services WCF (unidirectionnels et requête/réponse)
- Des pipelines de traitement basés sur des fichiers (XML compressé)
- Une base de données Oracle (entrepôt de données opérationnelles)

La solution manque actuellement de **documentation architecturale formelle**. Les connaissances opérationnelles sont concentrées chez quelques experts, et la configuration diffère selon l'environnement.

**Moteur de migration :** Microsoft BizTalk Server approche de sa fin de support, créant des risques opérationnels, de sécurité et de supportabilité.
**Intention stratégique :** Migrer HOA5 vers **Azure** en utilisant **Enterprise Message Transit (EMT)** comme modèle d'intégration stratégique.
**Focus de la phase :** Établir une **architecture d'état actuel solide, précise et fiable** — aucune décision d'architecture cible dans cette phase.
**Justification du projet pilote :** HOA5 est un **projet pilote à faible complexité** — c'est-à-dire un premier projet de migration réel (et non une preuve de concept) volontairement choisi pour sa portée réduite (un seul flux entrant, une seule orchestration BizTalk, un seul backend) afin de valider l'**approche d'évaluation assistée par IA** (GitHub Copilot) et le modèle d'intégration cible (Enterprise Message Transit) sous forte pression de délai de mise en marché, avant de l'appliquer aux flux plus complexes (HOA1, HOB1, etc.).

---

## 2. Objectifs (Phase actuelle)

- Produire une **architecture de l'état actuel complète et validée** pour HOA5.
- Reconstituer les connaissances architecturales (en l'absence de documentation fiable).
- Documenter entièrement les intégrations externes (API, WCF, basées sur fichiers).
- Identifier les dépendances, risques, contraintes et caractéristiques opérationnelles.
- Tirer parti de l'**analyse assistée par IA et de GitHub Copilot** pour accélérer la découverte.
- Fournir une **base factuelle** pour la planification future de la migration Azure.

> Remarque : Cette phase ne définit **pas** l'architecture cible et ne sélectionne pas de services Azure.

---

## 3. Portée

### 3.1 En portée — Artefacts applicatifs et d'intégration

**Artefacts de l'application BizTalk**

> **Source :** Vérifié dans le code source à partir du système de fichiers du dépôt et des fichiers de liaisons BizTalk (mars 2026).

**HOA5 Integration** (application BizTalk `MEC_HOA5`, dépôt `MEC-AlimenterBanque`) :
- Orchestration : `OrcServTransmPrel.odx` (spécifique à HOA5, appelle le HOA5 Backend)
- Map : `transformerMsgFichCHToMsgSVC.btm` (transforme le format de message TDF vers le contrat du HOA5 Backend)
- Schémas : schémas d'entrée/sortie de paramètres provenant de `HOA5_ServTransmPrel_cpo` (ex. : `ParamEntree.xsd`, `ParamSorti.xsd`)
- Fichier de liaisons : `Prod.RAMQ.HO.HOA5_ServTransmPrel_bt.xml`
- Pipelines : `PassThruTransmit` (envoi), `XMLReceive` (réponse) — pipelines BizTalk standard uniquement
- Aucun moteur de règles métier (BRE) identifié pour HOA5

**TDF Integration** (application BizTalk `TDF_OOA2`, dépôt `TDF-OOA2-Serv-Tranf-Fich`) :
- Orchestration : `orcTrnsmFichEntrant.odx` (partagée entre les flux, route vers HOA5 Integration)
- Fichier de liaisons : `Prod.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml`
- Pipelines : `PassThruReceive`, `PassThruTransmit`, `XMLReceive` — pipelines BizTalk standard uniquement

**Dépendances d'exécution externes (confirmées à partir du code source et des fichiers de liaisons)**

> **Source :** Vérifié dans le code source à partir des fichiers `Web.config` et des fichiers XML de liaisons BizTalk (mars 2026). Voir la section 11.1 pour les détails complets.

| Service | Protocole | URL confirmée | Appelé par |
|---------|-----------|---------------|------------|
| `YRD1_RechrRegleAcces_ws` (vérification des règles d'accès) | ASMX / SOAP | `http://nlb-fonct-app4.ramq.gov/Agsapp/YR/YRD_ModulComunExptn/YRD1_RechrRegleAcces_ws/RechrRegleAcces.asmx` | TDF Frontend (pendant `InitierEnvoi`) |
| `OOA2_InscrireSuiviFich_Ws` (persistance du suivi) | ASMX / SOAP | `http://srv-prod-seltrt01/TDFAPP/OO/OOA_GereEchg/OOA2_InscrireSuiviFich_Ws/FichEntrant.asmx` | TDF Integration (port d'envoi SOAP BizTalk) |
| Base de données opérationnelle Oracle | ODP.NET (direct) | `MEC_ORA.UDL` (fichier UDL sur l'hôte du HOA5 Backend) | HOA5 Backend (`TraiterTransmPrel.InsererDonneesFich`) |
| Partage de fichiers DepoApli | SMB / UNC | `\\{server}\DonneAppli\{Envir}\Mec\MEC_LIV00\Recu\HOA5_RecvrTrnsmPrel` | HOA5 Backend (`TraiterTransmPrel.SauvegarderFichierRecu`) |

**Code source des services ASMX** (les deux services sont dans les dépôts en portée — voir la section 3.2)

> **Source :** Vérifié dans le code source à partir du système de fichiers du dépôt (mars 2026).

| Service | Dépôt source | Projet source | Classe principale | Méthode principale |
|---------|-------------|----------------|------------|-------------|
| `YRD1_RechrRegleAcces_ws` | `AGS-ControleAcces` | `YRD1_RechrRegleAcces\src\YRD1_RechrRegleAcces_ws` | `RechrRegleAcces` | `ObtnRegleAcces` (recherche de règles d'accès, cache de 90 s) |
| `OOA2_InscrireSuiviFich_Ws` | `TDF-OOA2-Serv-Tranf-Fich` | `src\RAMQ.TDF.OOA2_InscrireSuiviFich_ws` | `FichEntrant` | `InscrireSuiviFichCorln` (persistance des enregistrements de suivi) |

**Artefacts opérationnels**
- Ressources de déploiement : `deploy.ps1`, `export-bindings.ps1`, `README-deploy.md`
- Configurations par environnement : `/env/dev.settings.json`, `/env/qa.settings.json`, `/env/prod.settings.json`
- Surveillance/Suivi : instantanés de la base de données de suivi BizTalk, exportation des alertes SCOM

---

### 3.2 En portée — Dépôts de code source

Les trois dépôts source confirmés couvrant tous les composants de la solution HOA5 et ses dépendances d'exécution sont :

| Dépôt | Chemin local | Composants |
|-------|------------|------------|
| `MEC-AlimenterBanque` | `D:\Source\MEC-AlimenterBanque` | HOA5 Frontend, HOA5 Integration, HOA5 Backend |
| `TDF-OOA2-Serv-Tranf-Fich` | `D:\Source\TDF-OOA2-Serv-Tranf-Fich` | TDF Frontend, TDF Integration, `OOA2_InscrireSuiviFich_Ws` |
| `AGS-ControleAcces` | `D:\Source\AGS-ControleAcces` | `YRD1_RechrRegleAcces_ws` (service de règles d'accès) |

#### 3.2.1 Correspondance Composant — Dépôt — Projet

> **Source :** Vérifié dans le code source à partir du système de fichiers du dépôt (mars 2026).

| Composant | Dépôt | Projets | Dépendances |
|-----------|-------|---------|-------------|
| Application CH | S/O | S/O | S/O |
| **HOA5 Frontend** | `MEC-AlimenterBanque` | `HOA5_RecvrTrnsmPrelimCH\src\HOA5_RecvrTrnsmPrelimCH_svc`<br>`HOA5_RecvrTrnsmPrelimCH\src\HOA5_RecvrTrnsmPrelimCH_cpo` | `TDF-OOA2-Serv-Tranf-Fich` : `OOA2_ServTranfMsgFich_svc`, `OOA2_V4StrucMsgFich_cpo`, `OOA2_V4TraitMsgFich_cpo` |
| **TDF Frontend** | `TDF-OOA2-Serv-Tranf-Fich` | `src\OOA2_ServTranfMsgFich_svc`<br>`src\OOA2_V4StrucMsgFich_cpo`<br>`src\OOA2_V4TraitMsgFich_cpo` | — |
| **TDF Integration** | `TDF-OOA2-Serv-Tranf-Fich` | `src\OOA2_TrnsmFichEntrant_bt` | Point de terminaison de HOA5 Integration |
| **`OOA2_InscrireSuiviFich_Ws`** | `TDF-OOA2-Serv-Tranf-Fich` | `src\RAMQ.TDF.OOA2_InscrireSuiviFich_ws` | Base de données Oracle TDF (enregistrements de suivi) |
| **`YRD1_RechrRegleAcces_ws`** | `AGS-ControleAcces` | `YRD1_RechrRegleAcces\src\YRD1_RechrRegleAcces_ws` | Base de données des règles d'accès AGS |
| **HOA5 Integration** | `MEC-AlimenterBanque` | `Biztalk\HOA5_ServTransmFichCHEntrantSpec\src\HOA5_ServTransmPrel_bt` | Point de terminaison du HOA5 Backend |
| **HOA5 Backend** | `MEC-AlimenterBanque` | `HOA5_ServTransmFichCHEntrantSpec\src\HOA5_ServTransmPrel_svc`<br>`HOA5_ServTransmFichCHEntrantSpec\src\HOA5_ServTransmPrel_cpo` | Oracle, DepoApli |

---

### 3.3 Découverte et documentation de l'architecture

Cette phase comprend :

- **Découverte et documentation**
  - Flux de messages et chorégraphie de bout en bout
  - Dépendances système et couplage (Oracle, DepoApli, AD RAMQ, et toute intégration additionnelle confirmée par l'inspection des fichiers de liaisons BizTalk)
  - Patrons : traitement basé sur fichier, protocole TDF en 3 étapes, corrélation (numéro d'échange), réessais
  - Gestion des erreurs, routage vers les messages non livrables (dead-letter), comportement de compensation
- **Analyse assistée par IA**
  - Reconstituer l'architecture non documentée à partir des dépôts/configurations
  - Identifier la complexité, la dette technique et les risques
  - Corréler les artefacts BizTalk avec les comportements des services backend
- **Livrables (état actuel)**
  - Diagramme de contexte : HOA5 + systèmes externes + frontières de confiance
  - Vue logique : domaines, orchestrations, contrats, patrons
  - Vue physique : hôtes/instances, adaptateurs, points de terminaison, planifications
  - Catalogue d'interfaces : matrice des points de terminaison (direction, protocole, contrat)
  - Carte des dépendances : services, entrepôts de données, planifications, traitements par lots
  - Instantané opérationnel : surveillance, journalisation, procédures de reprise
  - Base de référence des exigences non fonctionnelles : débit (msgs/h), latence (p50/p95), disponibilité, taux d'erreurs

---

## 4. Hors portée (Phase actuelle)

- Définition de l'architecture cible Azure (patrons, services, coûts)
- Travaux d'implémentation ou de refactorisation
- Améliorations fonctionnelles ou refonte des processus
- Activités de décommissionnement ou de consolidation

> L'objectif est de **comprendre et documenter** ce qui existe aujourd'hui.

---

## 5. Principes architecturaux

- Aucune documentation fiable de l'existant n'est disponible ; **la source de vérité = le code source, les fichiers de liaisons et le comportement à l'exécution**.
- **Préserver** les systèmes backend et les intégrations externes **par défaut** afin d'optimiser le délai de mise en marché.
- **La refactorisation ou la ré-architecture du backend est permise** lorsqu'elle est **bien justifiée**, par exemple :
  - Obsolescence / plateforme non supportée
  - Risque de sécurité ou de conformité
  - Impact disproportionné sur la complexité ou le coût de migration
  - Valeur claire à long terme
- Toute proposition de modification du backend doit être **documentée**, **fondée sur des preuves** et **gouvernée** (Comité d'architecture).
- Le comportement fonctionnel et les contrats de messages restent **inchangés** sauf approbation formelle.
- Azure est le nuage stratégique ; **Enterprise Message Transit (EMT)** est le modèle d'intégration privilégié.
- **État actuel uniquement** dans cette phase — aucune décision de conception de la cible.
- **Limites de mutabilité des services WCF (hypothèses du projet) :**
  - **HOA5 Frontend** (hors BizTalk) : le contrat de service est **verrouillé** — l'hypothèse du projet est de **ne pas modifier l'application CH**. HOA5 Frontend doit exposer la même interface `IDestinataireEntrant` dans toute cible de migration.
  - **TDF Integration et HOA5 Integration** (dans BizTalk) : **doivent être replateformées vers Azure** — c'est l'**objectif principal de la migration** (retirer BizTalk ; remplacer par Enterprise Message Transit sur Azure). Ceci est obligatoire, pas optionnel.
  - **TDF Frontend et HOA5 Backend** (hors BizTalk) : **peuvent être modifiés** lorsque techniquement justifié, fondé sur des preuves et approuvé par le Comité d'architecture.

---

## 6. Critères de succès (Phase actuelle)

- **Architecture de l'état actuel** approuvée (contexte, logique, physique, opérationnel).
- 100 % des interfaces HOA5 **cataloguées** (API, WCF, fichier).
- Tous les dépôts pertinents **indexés et corrélés** aux flux/contrats.
- Les résultats produits par l'IA (Copilot) génèrent des analyses **traçables et exploitables**, validées par les experts métier.
- Réduction de la dépendance au savoir tribal ; source de vérité unique dans `/docs/`.

---

## 7. Sécurité applicative

Les mécanismes de sécurité suivants sont en place aux frontières d'intégration :

| Point d'intégration | Mécanisme de sécurité | Notes |
|---------------------|----------------------|-------|
| Application CH → HOA5 Frontend | **HTTPS + authentification Basic** (actuel) | Les utilisateurs sont dans l'Active Directory RAMQ. L'interface est verrouillée — hypothèse du projet : l'application CH ne change pas. |
| HOA5 Integration (BizTalk) → HOA5 Backend | **Sécurité Windows** | Authentification intégrée Windows pour l'appel WCF interne |
| HOA5 Backend → Oracle | **Fichier UDL** (Universal Data Link) | Identifiants de connexion stockés dans un fichier `.udl` sur l'hôte du HOA5 Backend |
| HOA5 Backend → DepoApli | **Sécurité Windows** | Authentification intégrée Windows pour l'accès au dépôt de fichiers |

### 7.1 Observations et risques de sécurité

- **Authentification Basic sur HTTPS** (Application CH → HOA5 Frontend) : Mécanisme de sécurité au niveau du transport actuel. Les identifiants sont encodés en base64 et reposent entièrement sur TLS pour la confidentialité — aucune authentification multifacteur (MFA) ni à base de jetons dans l'état actuel. L'interface de HOA5 Frontend est verrouillée selon l'hypothèse du projet. Aucune modification de ce mécanisme n'est prévue du côté de l'application CH.
  - **Observation :** Le fichier `packages.config` de HOA5 Backend référence des bibliothèques JWT (`Princ.RAMQ.COM.COD1_JetonAuthn`, `Microsoft.IdentityModel.JsonWebTokens`) — cela suggère qu'une capacité d'authentification par jeton est déjà disponible dans le code du backend, bien qu'elle ne soit pas activement utilisée dans l'état actuel (l'authentification Basic est le mécanisme actif).
- **Fichier UDL pour les identifiants Oracle** : Identifiants stockés dans un fichier accessible en texte clair sur l'**hôte de HOA5 Backend** — un **risque connu de gestion des identifiants**. Les fichiers d'identifiants en texte clair constituent un point de défaillance unique et une préoccupation lors d'audits de sécurité.
- **Sécurité Windows** (Kerberos/NTLM) pour la communication interne : Tous les appels internes entre composants reposent sur l'infrastructure du domaine Windows. Ceci crée une dépendance forte envers l'environnement du domaine Windows.
- **Active Directory RAMQ** comme fournisseur d'identité : Tous les utilisateurs des centres hospitaliers s'authentifient via l'AD RAMQ. C'est l'autorité d'identité unique pour l'ensemble des ~140 centres hospitaliers.

---

## 8. Base de référence des exigences non fonctionnelles et hypothèses de travail

> **Remarque :** Les valeurs ci-dessous sont des estimations de travail utilisées comme référence pour l'évaluation. Elles doivent être validées avec les métriques réelles de production et les connaissances des experts métier avant d'être utilisées dans la planification de la migration.

| Paramètre | Valeur estimée | Notes |
|-----------|----------------|-------|
| Volume de messages | ~12k msgs/jour (p95) ; pointe à ~1,2k msgs/h | À valider avec la base de données de suivi BizTalk |
| Fenêtres critiques | 07 h 00 – 19 h 00 HE (heures ouvrables) ; traitement par lots à 23 h 30 HE | À confirmer avec les opérations |
| Cible de SLA | Latence p95 < 5 s par étape du protocole TDF (chacun des 3 appels WCF individuellement) ; récupération de fichiers toutes les 10 min | Aucune application de SLA confirmée actuellement |
| Gestion des erreurs | 3 réessais, **intervalle fixe de 5 minutes** (confirmé à partir du fichier de liaisons BizTalk de HOA5 Integration) ; comportement de type poison/dead-letter pour TDF Integration à confirmer avec les experts métier | L'intervalle de réessai est fixe, pas à croissance exponentielle (exponential backoff) |
| Reprise après sinistre | Basculement manuel ; RTO 4 h ; RPO 1 h (sauvegardes BizTalk nocturnes + journaux horaires) | Aucune reprise automatisée confirmée |

---

## 9. Surveillance et opérations

> **Évaluation : La solution de surveillance est immature.** Plusieurs capacités sont absentes ou non fonctionnelles.

| Capacité | Statut | Détails |
|----------|--------|---------|
| Alertes et notifications des services WCF | Partiel | Alertes configurées pour les services WCF |
| Alertes de suspension des orchestrations BizTalk | Configuré mais défaillant | Le déclencheur d'alerte existe, mais **les notifications ne se déclenchent pas** |
| Solution de surveillance BizTalk Server (BTS) | **Non fonctionnelle** | La configuration actuelle de surveillance BTS n'est pas opérationnelle |
| Business Activity Monitoring (BAM) | **Non déployé** | Le BAM n'est pas utilisé — **aucun suivi des métriques métier** |
| AppInsights | **Non disponible** | Non déployé dans cette application patrimoniale. Aucun SDK Application Insights ni télémétrie configuré dans aucun composant. |

### 9.1 Principaux risques opérationnels

- **Défaillances silencieuses :** Les orchestrations suspendues ne génèrent pas d'alertes efficaces — les défaillances peuvent passer inaperçues.
- **Aucune observabilité métier :** L'absence de déploiement du BAM signifie qu'il n'y a aucune visibilité de bout en bout sur les flux de transactions métier (ex. : nombre de fichiers reçus par centre hospitalier, taux de succès du traitement).
- **Angles morts au niveau BizTalk :** La solution de surveillance BTS étant non fonctionnelle, les opérations reposent sur des vérifications manuelles ou la détection d'erreurs en aval.
- **Impact sur la migration :** Il n'existe aucune base de référence de surveillance à reporter — toute architecture de remplacement doit inclure une stratégie d'observabilité complète dès le départ.

---

## 10. Lacunes connues et questions ouvertes

### 10.1 Faits confirmés (validés par les experts métier)

| # | Sujet | Réponse confirmée | Référence |
|---|-------|--------------------|-----------|
| 1 | HOA5 Backend — ordre de traitement de la persistance | Séquence de traitement confirmée à partir du code source : (1) sauvegarder le ZIP vers le chemin UNC DepoApli, (2) décompresser, (3) charger le XML dans des DataSets, (4) insertion Oracle via ODP.NET. L'écriture dans DepoApli a lieu **avant** l'insertion Oracle — elles ne sont pas couplées de manière atomique. | Section 1.6, `TraiterTransmPrel.vb` |
| 2 | Surveillance — statut du BAM et de BTS | BAM non déployé (aucune métrique métier). Surveillance BTS non fonctionnelle. AppInsights non disponible (non déployé). | Section 9 |
| 3 | `MEC_V_FICH_ECHG_ENTRE.VAL_VAR_3_FICH_ENTRE` — contenu de la colonne | Cette colonne stocke le **nom et la version du logiciel client** de l'hôpital (ex. : le logiciel utilisé pour la validation des abrégés et la transmission à la RAMQ) et non la version du schéma XML. Vérifié dans le code source : pour **HOA1** (transmission régulière), la valeur est extraite du XPath `/transmission_sejour/nom_version_logiciel` dans `MsgLotFichMec.ObtnValVarFichEchgEntre()`. Pour **HOA5** (transmission préliminaire), cette colonne **reste vide** car `MessageFichierEntrantCH` ne redéfinit pas la méthode `ObtnValVarFichEchgEntre()` de la classe de base TDF. Les colonnes `VAL_VAR_1` à `VAL_VAR_10` sont des emplacements génériques dont le contenu est défini par chaque flux via redéfinition de méthode. | `MsgLotFichMec.vb` (HOA1), `MessageFichierEntrantCH.vb` (HOA5), `MessageFichierEntrant.vb` (TDF base) |
| 4 | Fichiers dans DepoApli — rôle opérationnel et cycle de vie confirmés | Les fichiers ZIP dans DepoApli ne sont **pas des fichiers orphelins au sens d'un défaut** — ils servent de **mécanisme de reprise manuelle pour le pilotage**. Lorsqu'un problème survient et que la reprise via la console BizTalk Administration n'est pas possible, l'équipe de pilotage utilise le fichier ZIP déjà présent dans DepoApli pour **rejouer le message** sans avoir à redemander le fichier original à l'établissement hospitalier (ce qui prendrait plus de temps). **Cycle de vie opérationnel :** les fichiers sont **déplacés de DepoApli vers `\\corpoprodque\donne\` (S:\)** par une **tâche planifiée Control-M**. C'est le répertoire S:\ (accès plus rapide pour les pilotes) qui accumule les fichiers — DepoApli est **généralement vide** après le transfert. Le pilote a confirmé que des fichiers **datant de 2019** existent dans le répertoire S:\. **Aucun nettoyage ni archivage** n'est présentement effectué. Le pilote a également mentionné une exigence de **conservation minimale de 5 ans** (à confirmer formellement). **Implication pour la migration :** ce mécanisme de reprise manuelle et le cycle de vie Control-M → S:\ doivent être préservés ou remplacés par un équivalent fonctionnel dans l'architecture cible (ex. : Azure Blob Storage avec rétention configurable de 5 ans minimum, politique de cycle de vie automatisée). | Confirmé par l'équipe HOA5 (revue d'équipe 2026-04-01, complété 2026-04-02) |
| 5 | Source de l'accusé de réception de `CorrellerEnvoyer` — contrat de confirmation en boucle fermée | L'accusé de réception comparé à l'étape 3 (`CorrellerEnvoyer`) est **généré par le TDF Frontend lui-même** à l'étape 2 — ce n'est **ni** la réponse de HOA5 Backend, **ni** un accusé BizTalk. Mécanisme vérifié dans le code source : (1) `GenererAccRecep()` dans `Message.vb` génère un jeton = `IdEntIntvnEchg` + horodatage local `YYYYMMDDHHmmss` (ex. : `AGS1234520260325143052`). (2) Le jeton est stocké dans `SuiviMessage_DS` (colonne `NO_ACC_RECEP`) et dans le contexte `EnteteRAMQ.EtatEchgFich` (sérialisé en Base64 via `ColctContxEchgFich`). (3) Le jeton est retourné à l'application CH dans `_strXmlSortie` (clé XML `no_accu_recept`). (4) À l'étape 3, `ValiderAccRecep()` dans `Destinataire.vb` compare la valeur reçue du CH App à la valeur stockée dans le contexte WCF (`objContexteWS.Donnees`). Si la comparaison échoue ou si la valeur est vide, une `ApplicationExceptionRAMQ` est levée (`NoErrAccuseReceptionInvalide`). Ce n'est qu'après la validation de l'accusé de réception que le TDF Frontend envoie le message de corrélation à BizTalk (`EnvoyerCorrellerServCourtage()`). | `Message.vb` (GenererAccRecep), `Destinataire.vb` (ValiderAccRecep), `DestinataireEntrant.svc.vb` (CorrellerEnvoyer), `ColctContxEchgFich.vb`, `Constante.vb` (no_accu_recept) |
| 6 | SLA et disponibilité de `YRD1_RechrRegleAcces_ws` | **Pas de mécanisme de repli. Échec immédiat.** Si le service `YRD1_RechrRegleAcces_ws` est indisponible pendant `InitierEnvoi`, l'appel SOAP échoue et l'exception se propage directement à l'application CH — la transmission ne peut pas être initiée. Vérifié dans le code source de `Destinataire.vb` : l'appel `ObtnDroitContxRegleAcces` n'est entouré d'aucune boucle de réessai ni d'aucun mécanisme de repli au niveau du TDF Frontend. Seule la mise en cache applicative côté serveur (90 secondes, `CacheDuration:=90` dans `RechrRegleAcces.asmx.vb`) offre une protection partielle contre les pannes brèves — mais cette mise en cache est au niveau du service AGS, pas du consommateur. **Risque :** dépendance unique pour le contrôle d'accès de ~140 établissements hospitaliers. | Confirmé par l'équipe HOA5 (revue d'équipe 2026-04-02), vérifié dans `Destinataire.vb`, `RechrRegleAcces.asmx.vb` |
| 7 | Mode de défaillance de `OOA2_InscrireSuiviFich_Ws` — configuration de réessai du port d'envoi | La résilience de l'appel au service de suivi repose sur **deux niveaux** : (1) **Boucle d'orchestration** (`loopResilience` dans `orcTrnsmFichEntrant.odx`) — réessaie l'appel SOAP tant que `gblnAppelServiceOK` reste à `false`, avec suspension de l'instance d'orchestration en cas d'exception (reprise manuelle par un opérateur). (2) **Configuration du port d'envoi BizTalk** (`sndPoOOA2_poInscrireSuiviFich_Entrant`) — vérifié dans les fichiers de liaison de **tous les 9 environnements** : transport primaire `RetryCount=3`, `RetryInterval=5` (3 tentatives à intervalles de 5 minutes) ; transport secondaire `RetryCount=1000`, `RetryInterval=60` (1000 tentatives à intervalles de 60 minutes). Cela signifie qu'en cas d'indisponibilité prolongée, BizTalk peut réessayer pendant **plus de 41 jours** via le transport secondaire avant d'abandonner. Le scope transactionnel de 10 minutes (`scopeRecvrFichEntrant`) limite la durée de **l'orchestration elle-même**, mais les réessais du port d'envoi sont gérés indépendamment par le moteur de transport BizTalk. | Fichiers de liaison BizTalk (Prod, Acptn, Fonct, etc.), `orcTrnsmFichEntrant.odx` |
| 8 | Données personnelles dans les environnements hors production | Les environnements de développement (hors production) doivent utiliser des **données de test (non réelles)**. HOA5 traite des données de santé personnelles (NAM, nom, date de naissance, date de départ, diagnostics) — ces données ne doivent **jamais** être utilisées telles quelles dans les environnements de développement. **Implication pour la migration :** les jeux de données de test pour les Azure Functions doivent être construits avec des valeurs fictives évidentes. Toute manipulation de données réelles hors production nécessite une validation par l'équipe Sécurité et Conformité. | Confirmé par l'équipe HOA5 (revue d'équipe 2026-04-02) |
| 9 | Contrats d'intégration des centres hospitaliers — versionnement et compatibilité | Les contrats d'intégration des centres hospitaliers ne sont **pas versionnés** et ne sont **pas incompatibles** d'un environnement à l'autre. Les ~140 établissements hospitaliers utilisent le même contrat `IDestinataireEntrant` (3 opérations WCF : `InitierEnvoi`, `EnvoyerLotFichier`, `CorrellerEnvoyer`) de manière uniforme dans tous les environnements. **Implication pour la migration :** le contrat de service est stable et uniforme — aucun risque de rupture de contrat entre environnements. Le contrat reste verrouillé (hypothèse du projet). | Confirmé par l'équipe HOA5 (revue d'équipe 2026-04-02) |

### 10.2 Questions ouvertes (à valider avec les experts métier)

- ~~**Contrats d'intégration des centres hospitaliers :** Les contrats d'intégration sont-ils versionnés, ou sont-ils **incompatibles** d'un environnement à l'autre ?~~ **Confirmé : non versionnés et non incompatibles.** Les contrats sont uniformes dans tous les environnements. Voir fait confirmé \#9 dans la section 10.1.
- ~~Y a-t-il des **champs contenant des renseignements personnels** dans les charges utiles qui nécessitent un masquage ou une anonymisation dans les environnements hors production ?~~ **Confirmé : données de test obligatoires hors production.** Les environnements de développement doivent utiliser des données de test (non réelles). Voir fait confirmé \#8 dans la section 10.1.
- ~~**AppInsights** est-il activé dans un quelconque composant ?~~ **Confirmé : Non disponible.** AppInsights n'est déployé dans aucun composant de cette application patrimoniale. La surveillance repose sur la base de données de suivi BizTalk (non fonctionnelle), le journal d'événements Windows et SCOM (partiel).
- ~~**Source de l'accusé de réception de `CorrellerEnvoyer` :** Quel accusé de réception est comparé à l'étape 3 ? Est-ce la réponse de **HOA5 Backend** (résultat du commit Oracle), un accusé de réception de traitement BizTalk, ou autre chose ?~~ **Confirmé : jeton de poignée de main généré par le TDF Frontend.** L'accusé de réception est généré par `GenererAccRecep()` dans `Message.vb` (IdEntIntvnEchg + horodatage local), stocké dans le contexte Base64 et retourné au CH App. À l'étape 3, `ValiderAccRecep()` compare la valeur reçue à la valeur stockée. Voir fait confirmé \#5 dans la section 10.1.
- ~~**SLA et disponibilité de `YRD1_RechrRegleAcces_ws` :** Quel est le mode de défaillance si ce service externe est indisponible ? `InitierEnvoi` échoue-t-il immédiatement, ou existe-t-il un mécanisme de repli ?~~ **Confirmé : pas de mécanisme de repli, échec immédiat.** Si le service est indisponible, l'exception se propage directement à l'application CH. Voir fait confirmé \#6 dans la section 10.1.
- ~~**Mode de défaillance de `OOA2_InscrireSuiviFich_Ws` :** Si le service SOAP de suivi est temporairement indisponible, TDF Integration réessaie l'appel en boucle.~~ **Confirmé : résilience à deux niveaux (orchestration + port d'envoi).** La boucle d'orchestration (`loopResilience`) réessaie avec suspension manuelle. Le port d'envoi BizTalk ajoute un second niveau : transport primaire 3×5 min, transport secondaire 1000×60 min (~41 jours). Le scope de 10 minutes limite l'orchestration, pas les réessais du port d'envoi. Voir fait confirmé \#7 dans la section 10.1.
- ~~**Fichiers orphelins dans DepoApli :** Existe-t-il un mécanisme automatisé de nettoyage ou de retraitement pour les fichiers ZIP laissés dans DepoApli lorsque l'insertion Oracle subséquente échoue ?~~ **Confirmé : reprise manuelle intentionnelle.** Les fichiers ZIP servent de mécanisme de reprise pour le pilotage — pas de nettoyage automatisé, conservation intentionnelle. Voir fait confirmé \#4 dans la section 10.1.

---

## 11. Dépendances des composants et découverte

> Toutes les constatations de cette section sont vérifiées dans le code source à partir des fichiers source, des fichiers de configuration et des fichiers de liaisons BizTalk du dépôt.

### 11.1 Dépendances de services à l'exécution

Ces services sont appelés à l'exécution par les composants de la solution HOA5. **Deux des trois sont dans les dépôts en portée** — leur code source est sur disque et disponible pour l'analyse.

#### 11.1.1 Services ASMX en portée (code source disponible)

| Service | Dépôt source | Projet source | Consommateur | Protocole | URL (confirmée) |
|---------|-------------|----------------|-------------|----------|-----------------|
| `YRD1_RechrRegleAcces_ws` | `AGS-ControleAcces` | `YRD1_RechrRegleAcces/src/YRD1_RechrRegleAcces_ws` | TDF Frontend (pendant `InitierEnvoi`) | HTTP / ASMX (SOAP) | `http://nlb-fonct-app4.ramq.gov/Agsapp/YR/YRD_ModulComunExptn/YRD1_RechrRegleAcces_ws/RechrRegleAcces.asmx` |
| `OOA2_InscrireSuiviFich_Ws` | `TDF-OOA2-Serv-Tranf-Fich` | `src/RAMQ.TDF.OOA2_InscrireSuiviFich_ws` | TDF Integration (port d'envoi SOAP BizTalk) | Adaptateur SOAP + authentification Basic | `http://srv-prod-seltrt01/TDFAPP/OO/OOA_GereEchg/OOA2_InscrireSuiviFich_Ws/FichEntrant.asmx` |

**`YRD1_RechrRegleAcces_ws` — Description vérifiée dans le code source** (`AGS-ControleAcces\YRD1_RechrRegleAcces\src\YRD1_RechrRegleAcces_ws\RechrRegleAcces.asmx.vb`) :

- **Classe :** `RechrRegleAcces` (hérite de `System.Web.Services.WebService`)
- **Méthode principale :** `ObtnRegleAcces(_dsInfoRegleAcces As InfoRegleAccesDS, ByRef _dsRegleAcces As RegleAccesDS)` — prend un identifiant d'utilisateur (`id_util`), un code de contexte (`cod_contx_droit_acces`) et un code d'application (`cod_appli`) ; retourne la liste des règles d'accès dans `_dsRegleAcces`. Résultats mis en cache pendant **90 secondes** (`CacheDuration:=90`).
- **Méthode secondaire :** `ObtnDroitAcces(_objCritrRechrDroitAcces) As ParamSortiDroitAcces` — retourne des informations descriptives sur les droits d'accès.
- **Délègue à :** le composant de logique métier `CnsulRegleAcces` (`YRD1_RechrRegleAcces_cpo`).
- **Risque :** Si ce service est indisponible pendant `InitierEnvoi`, l'application CH ne peut pas initier une transmission. **Pas de mécanisme de repli, échec immédiat** (confirmé par l'équipe HOA5). Aucune boucle de réessai au niveau du TDF Frontend (vérifié dans le code source). La mise en cache des règles d'accès (90 s) est au niveau applicatif du service AGS — pas du consommateur. Dépendance unique pour le contrôle d'accès de tous les centres hospitaliers.

**`OOA2_InscrireSuiviFich_Ws` — Description vérifiée dans le code source** (`TDF-OOA2-Serv-Tranf-Fich\src\RAMQ.TDF.OOA2_InscrireSuiviFich_ws\FichEntrant.asmx.vb`) :

- **Classe :** `FichEntrant` (hérite de `System.Web.Services.WebService`)
- **Méthode principale :** `InscrireSuiviFichCorln(_objInfoSuivi As ParamInfoFichMsg) As ParamCntin` — reçoit deux documents XML (informations fichier/message `xmlInfoFichMsg` + informations de suivi `xmlInfoSuivi`), reconstitue les DataSets `SuiviMessage_DS` et `SuiviFich_DS`, et appelle `OOA2_TraitMsgBiztalk_cpo.FichEntrant.Correller(dsSuiviMsg, dsSuiviFich)` pour écrire les lignes de suivi d'échange dans la base de données Oracle TDF. Retourne `blnCntin = True` en cas de succès.
- **Appelé par :** L'orchestration BizTalk de TDF Integration (`orcTrnsmFichEntrant.odx`) via le port d'envoi SOAP `poInscrireSuiviFich`, à l'intérieur d'une **boucle de réessai** (`loopResilience`) — BizTalk réessaie l'appel tant que `gblnAppelServiceOK` reste à `false`. **Configuration de réessai du port d'envoi** (vérifié dans les fichiers de liaison de tous les 9 environnements) : transport primaire `RetryCount=3`, `RetryInterval=5` min; transport secondaire `RetryCount=1000`, `RetryInterval=60` min (~41 jours de réessais potentiels).
- **Risque :** Requis pour l'intégrité de la piste d'audit. Si indisponible, la boucle d'orchestration suspend l'instance (reprise manuelle par un opérateur). Les réessais du port d'envoi BizTalk (3×5 min primaire, 1000×60 min secondaire) permettent une récupération automatique prolongée avant abandon définitif. Aucun mécanisme de suivi alternatif n'existe.

#### 11.1.2 Composant d'exécution externe (hors des dépôts en portée)

| Dépendance | Consommateur | Protocole | Emplacement | Notes |
|------------|-------------|----------|-------------|-------|
| `BTSHTTPReceive.dll` (récepteur HTTP BizTalk) | TDF Integration (port d'envoi `sndPoOOA2_poEnvoiMsgGenSortie_Entrant`) | HTTP | Hébergé sur `srv-prod-biztrt01` | Composant BizTalk standard ; reçoit le message de fichier de TDF Integration et le publie dans le MessageBox BizTalk pour HOA5 Integration. Sera retiré lors du replateformage de BizTalk. |

### 11.2 Composants personnalisés et non standard

Ces composants **ne sont pas des composants standard BizTalk ou WCF** et représentent des artefacts de déploiement additionnels ou des risques opérationnels.

| Composant | Type | Source | Emplacement | Utilisé par | Objectif | Risque |
|-----------|------|--------|-------------|-------------|----------|--------|
| `TranfBasic2IntegBehaviorExtn` | Extension de comportement de point de terminaison WCF personnalisée | DLL `RAMQ.CO.COD3_V2InteropWsdl_cpo` (déployée dans le GAC) | Enregistré dans le `machine.config` de BizTalk comme `behaviorExtensions` ; configuré dans le port d'envoi WCF-Custom de HOA5 Integration `EndpointBehaviorConfiguration` | **HOA5 Integration** (port d'envoi WCF-Custom BizTalk) | **Credential bridge** — la liaison sortante BizTalk utilise `clientCredentialType="Windows"` (compte de service BizTalk), mais le web.config de HOA5 Backend déclare `clientCredentialType="Basic"`. Ce comportement intercepte la requête sortante et transforme les identifiants Windows/Kerberos vers le format attendu par le point de terminaison cible. Raison pour laquelle WCF-Custom a été choisi plutôt que WCF-BasicHttp : l'adaptateur standard WCF-BasicHttp n'a pas de propriété `EndpointBehaviorConfiguration` — WCF-Custom était nécessaire pour ce point d'extensibilité. La liaison WCF-BasicHttp originale est préservée dans le source `ServTransmPrel.BindingInfo.xml` comme preuve. | **Élevé** — non standard, déployé dans le GAC, source ABSENTE de tout dépôt sur disque (réside dans un dépôt d'infrastructure de sécurité séparé de l'équipe CO/Commun). Aucune documentation, aucun test unitaire, aucun source disponible pour audit. Point de défaillance unique pour toute l'authentification BizTalk-vers-Backend. |
| `TrnsfWsdlExtn` / `TrnsfAliasWsdlExtn` | Comportement de point de terminaison WCF personnalisé | Même DLL : `RAMQ.CO.COD3_V2InteropWsdl_cpo` (déployée dans le GAC) | Enregistré dans le `Web.config` de HOA5 Frontend/HOA5 Backend comme comportement de point de terminaison (`<TrnsfAliasWsdlExtn SSL="True" />`) | **HOA5 Frontend**, **HOA5 Backend**, **HOA1 sortant** | Transformation d'alias WSDL + paramètre SSL pour les points de terminaison de services entrants. Confirmé dans `Forma.Web.config` de `HOA5_ServTransmPrel_svc` et `HOA5_RecvrTrnsmPrelimCH_svc`. Également utilisé dans les configurations sortantes de HOA1. | **Moyen** — même bibliothèque que `TranfBasic2IntegBehaviorExtn` ; source confirmé absent des dépôts en portée. Partagé entre plusieurs applications — l'impact d'une défaillance est large. |
| `Polly-Signed` (v5.9.0) | Bibliothèque NuGet de réessai | Référencé dans le `packages.config` de HOA5 Backend (`HOA5_ServTransmPrel_cpo` et `HOA5_ServTransmPrel_svc`) | HOA5 Backend | **HOA5 Backend** | **Dépendance fantôme** — le package NuGet est référencé mais **aucun import (`Imports Polly`, `using Polly`) ni aucune utilisation** (`Policy.`, `RetryPolicy`, `CircuitBreakerPolicy`) n'a été identifié dans le code source VB.NET ou C# de HOA5 Backend. Aucune configuration liée à Polly dans `Web.config` non plus. Le package est du poids mort — probablement ajouté de façon spéculative ou comme dépendance transitive qui n'a jamais été consommée. Peut être retiré en toute sécurité lors de la migration. | **Aucun** — dépendance non utilisée ; aucun risque opérationnel. |
| `Princ.RAMQ.COM.COD1_JetonAuthn` (v1.3.3) | Bibliothèque d'authentification JWT RAMQ | NuGet (`HOA5_ServTransmPrel_svc/packages.config`) | HOA5 Backend | **HOA5 Backend** | Capacité d'authentification par jeton ; pas activement utilisée dans l'état actuel (authentification Basic). L'infrastructure d'authentification JWT est présente mais dormante. | **Faible** — capacité dormante ; aucun risque opérationnel dans l'état actuel. Note : l'infrastructure JWT est déjà en place. |

### 11.3 Pipelines et adaptateurs standard confirmés

| Pipeline / Adaptateur | Utilisation | Notes |
|-----------------------|-------------|-------|
| `PassThruTransmit` | Port d'envoi de HOA5 Integration | Aucune transformation au niveau du pipeline — le map est appliqué dans l'orchestration |
| `PassThruReceive` | Ports de réception de TDF Integration | Passage direct du message brut |
| `XMLReceive` | Port de réception de confirmation de HOA5 Integration | Analyse XML standard pour la réponse |
| Adaptateur `WCF-Custom` | Port d'envoi de HOA5 Integration (`sndPoHOA5_poSvcTrnsPrelCH`) | Adaptateur BizTalk standard configuré avec une liaison personnalisée et un comportement de point de terminaison |
| Adaptateur `SOAP` | Port d'envoi de TDF Integration (`sndPoOOA2_poInscrireSuiviFich_Entrant`) | Authentification Basic, appel SOAP vers le service de suivi |
| Adaptateur `HTTP` | Port d'envoi de TDF Integration (`sndPoOOA2_poEnvoiMsgGenSortie_Entrant`) | POST vers `BTSHTTPReceive.dll` |

> **Remarque :** Aucun **adaptateur BizTalk personnalisé** pour Oracle n'a été identifié dans HOA5. L'intégration Oracle est effectuée directement dans le service de **HOA5 Backend** via `ODP.NET` (`Oracle.DataAccess.Client`). BizTalk lui-même ne se connecte pas à Oracle pour HOA5.

### 11.4 Infrastructure — Hôtes et applications BizTalk

| Élément | Valeur | Composant |
|---------|--------|-----------|
| Application BizTalk | `MEC_HOA5` | HOA5 Integration |
| Application BizTalk | `TDF_OOA2` | TDF Integration |
| Hôte d'orchestration BizTalk | `HOST_ORCHESTRATION_MEC` | HOA5 Integration |
| Hôte du gestionnaire d'envoi BizTalk | `HOST_MEC_SEND` | HOA5 Integration |
| Hôte d'orchestration BizTalk | `HOST_ORCHESTRATION_TDF` | TDF Integration |
| Hôte du gestionnaire d'envoi BizTalk | `HOST_TDF_SEND` | TDF Integration |
| Serveur de HOA5 Backend | `srv-prod-seltrt01` | Hôte de HOA5 Backend |
| Serveur BizTalk | `srv-prod-biztrt01` | Récepteur HTTP BizTalk |

### 11.5 Stockage de fichiers et de données — Chemins confirmés

| Stockage | Type | Chemin (production) | Mécanisme | Notes |
|----------|------|---------------------|-----------|-------|
| DepoApli (HOA5) | Partage de fichiers SMB/UNC | `\\adprod.ramq.gov\appli\DonneAppli\{Envir}\Mec\MEC_LIV00\Recu\HOA5_RecvrTrnsmPrel` | `System.IO.FileStream` (écriture directe dans le code CPO de HOA5 Backend) | Écrit **avant** l'insertion Oracle ; pas un adaptateur BizTalk ; non couplé transactionnellement à Oracle. Fichiers déplacés vers `\\corpoprodque\donne\` (S:\) par **Control-M**. Rétention intentionnelle (~5 ans) pour reprise manuelle. |
| Connexion Oracle | Fichier UDL | `MEC_ORA.UDL` (sur l'hôte de HOA5 Backend) | `Oracle.DataAccess.Client` (ODP.NET) | Risque lié aux identifiants — le UDL est un stockage d'identifiants basé sur fichier ; pas une identité gérée ni un Key Vault |

---

## 12. Aides à l'analyse pour GitHub Copilot

> Ces prompts aident Copilot à produire des résultats utiles et délimités.

### 12.1 Inventaire des artefacts BizTalk (vérifié dans le code source)

> **Source :** Balayage du système de fichiers des dépôts `MEC-AlimenterBanque` et `TDF-OOA2-Serv-Tranf-Fich` (mars 2026).

#### 12.1.1 Résumé

| Type d'artefact | MEC-AlimenterBanque (HOA5) | TDF-OOA2-Serv-Tranf-Fich (TDF) | Total |
|-----------------|---------------------------|---------------------------------|-------|
| Projets BizTalk (.btproj) | 1 | 3 | **4** |
| Orchestrations (.odx) | 1 | 2 (+2 auto-générées) | **3** |
| Schémas (.xsd) dans les projets BT | 8 | 8 | **16** |
| Maps (.btm) | 1 | 3 | **4** |
| Pipelines (.btp) | 0 | 0 | **0** |
| Schémas de propriétés | 0 | 1 | **1** |
| Fichiers de liaisons (par environnement) | 9 + 2 générés par le projet | 18 (9 entrant + 9 sortant) | **29** |

> **Remarque :** Aucun pipeline personnalisé (`.btp`) ni aucune politique du moteur de règles métier (BRE) n'a été identifié. Tous les composants utilisent les pipelines BizTalk standard (`PassThruReceive`, `PassThruTransmit`, `XMLReceive`).

#### 12.1.2 HOA5 Integration — `MEC-AlimenterBanque` (Application BizTalk : `MEC_HOA5`)

Tous les artefacts BizTalk dans un seul projet : `HOA5_ServTransmPrel_bt` (`Biztalk\HOA5_ServTransmFichCHEntrantSpec\src\HOA5_ServTransmPrel_bt\`).

**Orchestrations :**

| Fichier | Objectif |
|---------|----------|
| `OrcServTransmPrel.odx` | Orchestration séquentielle spécifique à HOA5 : reçoit le fichier de TDF Integration, applique le map, envoie au HOA5 Backend, vérifie le code de retour. 6 shapes. |

**Schémas :**

| Fichier | Rôle |
|---------|------|
| `SchFichCHSpec.xsd` | Schéma de fichier/message entrant spécifique à HOA5 (format TDF) |
| `ServTransmPrel_hoa5_servtransmprel_cpo_ParamEntree.xsd` | Paramètres d'entrée pour l'appel WCF à HOA5 Backend |
| `ServTransmPrel_hoa5_servtransmprel_cpo_ParamSorti.xsd` | Paramètres de sortie de la réponse WCF de HOA5 Backend |
| `ServTransmPrel_hoa5_servtransmprel_svc_1.xsd` | Schéma du contrat de service WCF (HOA5 Backend) |
| `ServTransmPrel_coc6_v2classebase_cpo_1.xsd` | Schéma de la classe de base commune (bibliothèque partagée RAMQ) |
| `ServTransmPrel_coc1_v2gestionmsg_cpo_1.xsd` | Schéma de gestion des messages (bibliothèque partagée RAMQ) |
| `ServTransmPrel_cod1_v2erreurwcf_cpo_1.xsd` | Schéma d'erreur/faute WCF (bibliothèque partagée RAMQ) |
| `ServTransmPrel_schemas_microsoft_com_2003_10_Serialization.xsd` | Primitives de sérialisation DataContract .NET |

**Maps :**

| Fichier | Schéma source | Schéma cible | Objectif |
|---------|--------------|--------------|----------|
| `transformerMsgFichCHToMsgSVC.btm` | `SchFichCHSpec` (format TDF) | `ParamEntree` (contrat du HOA5 Backend) | Transforme le message de fichier générique TDF vers le format d'entrée du HOA5 Backend |

**Fichiers de liaisons (par environnement) :**

| Fichier | Environnement |
|---------|---------------|
| `Prod.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | **Production** |
| `Acptn.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Acceptation |
| `Fonct.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Fonctionnel |
| `Forma.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Formation |
| `Integ.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Intégration |
| `Parte.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Partenaire |
| `Prdev.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Pré-dev |
| `Unit.RAMQ.HO.HOA5_ServTransmPrel_bt.xml` | Unitaire |
| `ServTransmPrel.BindingInfo.xml` | Généré par le projet (WCF-BasicHttp original) |
| `ServTransmPrel_Custom.BindingInfo.xml` | Généré par le projet (WCF-Custom avec credential bridge) |

#### 12.1.3 TDF Integration — `TDF-OOA2-Serv-Tranf-Fich` (Application BizTalk : `TDF_OOA2`)

Contient 3 projets BizTalk : schémas partagés, orchestration entrante et orchestration sortante.

**Projets BizTalk :**

| Projet | Chemin | Rôle |
|--------|--------|------|
| `OOA2_SchemMsg_bt.btproj` | `src\OOA2_SchemMsg_bt\` | Schémas partagés + schéma de propriétés (utilisés par Entrant et Sortant) |
| `OOA2_TrnsmFichEntrant_bt.btproj` | `src\OOA2_TrnsmFichEntrant_bt\` | Transmission de fichiers **entrante** (flux HOA5) |
| `OOA2_TrnsmFichSortant_bt.btproj` | `src\OOA2_TrnsmFichSortant_bt\` | Transmission de fichiers sortante (hors portée pour HOA5) |

**Orchestrations :**

| Fichier | Projet | Objectif |
|---------|--------|----------|
| `orcTrnsmFichEntrant.odx` | OOA2_TrnsmFichEntrant_bt | **En portée HOA5** — Parallel Activating Convoy (3 branches). Reçoit le fichier, le suivi et les messages de corrélation. Persiste le suivi, route vers HOA5 Integration. ~25 shapes. |
| `orcTrnsmFichSortant.odx` | OOA2_TrnsmFichSortant_bt | Transmission de fichiers sortante (hors portée). |

**Schémas (`OOA2_SchemMsg_bt` — partagés) :**

| Fichier | Rôle | Utilisé par |
|---------|------|-------------|
| `PropertySchema.xsd` | **Schéma de propriétés** — propriété promue `NoEchg` pour le routage par corrélation | Toutes les branches (fichier, suivi, corrélation) |
| `schInfoFichMsgFichEntrant.xsd` | Schéma de message de fichier entrant (`NoEchg`, `IndExecOrcSpec`, `FichMsg/Contenu`) | Branche 2 (scopeFichMsg) |
| `schInfoSuiviFichEntrant.xsd` | Schéma de suivi (suivi) entrant (`NoEchg`, `Erreur`, `SuiviFichier`, `SuiviMessage`) | Branche 3 (scopeSuivi) |
| `schInfoCorlnFichEntrant.xsd` | Schéma de corrélation entrant (`NoEchg` uniquement) | Branche 1 (scopeCorln) |
| `schInfoCorlnFichSortant.xsd` | Informations de corrélation sortante (hors portée) | Orchestration sortante |
| `schInfoSuiviFichSortant.xsd` | Informations de suivi sortant (hors portée) | Orchestration sortante |

**Maps :**

| Fichier | Projet | Objectif |
|---------|--------|----------|
| `mapInscrSuiviFich.btm` | OOA2_TrnsmFichEntrant_bt | **En portée HOA5** — Fusionne le msg fichier + le msg suivi → requête `InscrireSuiviFichCorln` pour le service SOAP de suivi |
| `mapInscrSuiviFich.btm` | OOA2_TrnsmFichSortant_bt | Variante sortante (hors portée) |
| `mapSchMapInscrSuiviFich.btm` | OOA2_TrnsmFichSortant_bt | Map intermédiaire secondaire (hors portée) |

**Fichiers de liaisons (entrant — en portée HOA5) :**

| Fichier | Environnement |
|---------|---------------|
| `Prod.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | **Production** |
| `Acptn.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Acceptation |
| `Fonct.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Fonctionnel |
| `Forma.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Formation |
| `Integ.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Intégration |
| `Parte.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Partenaire |
| `Prdev.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Pré-dev |
| `Unit.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Unitaire |
| `Urg.RAMQ.OO.OOA2_TrnsmFichEntrant_bt.xml` | Urgence |
