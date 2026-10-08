# Réveil musical

Service qui réveille chaque utilisateur avec un morceau choisi selon **le jour** et **la météo**, puis le prévient sur **le canal de son choix** (email, SMS, push).

Le TP porte sur l'appel déclenché à l'heure du réveil : il reçoit `userId`, le jour de la semaine et la météo (`SOLEIL`, `PLUIE`, `NEIGE`, `NUAGEUX`) et renvoie un compte rendu. L'ordonnancement et l'appel météo ne sont pas codés.

- .NET 10 (SDK 10.0.112), C#
- 101 tests unitaires, **100 % des lignes** et **93 % des branches** couvertes

---

## Démarrage

```bash
dotnet build ReveilMusical.slnx
dotnet run --project src/ReveilMusical.Cli -- alice lundi PLUIE
```

```
info: …FakeMailClient[0]
      [EMAIL] à alice@example.com | Bon lundi ! | <p>Il pleut aujourd&#39;hui, pensez au parapluie. Pour bien commencer la journ&#233;e : Purple Rain — Prince &amp; The Revolution.</p>

Utilisateur : alice
Morceau     : Purple Rain — Prince & The Revolution (source : itunes)
Canal       : email
Délivré     : oui
```

Utilisateurs de démonstration (mock du service de préférences) : `alice` (email), `bob` (SMS), `chloe` (push).
Le jour s'écrit en français ou en anglais (`lundi`, `Monday`). Une météo inconnue ne bloque rien : le morceau de secours de l'utilisateur est utilisé.
Code de sortie : `0` = délivré, `1` = non délivré, `2` = arguments invalides.

### Rejouer les pannes

Tout se pilote par configuration (`appsettings.json` ou variables d'environnement) :

```bash
# Canal SMS en panne : bob est réveillé par push
Notifications__Fakes__SmsOutage=true dotnet run --project src/ReveilMusical.Cli -- bob mercredi NUAGEUX

# MusicBrainz en premier
Music__ProviderOrder__0=musicbrainz Music__ProviderOrder__1=itunes dotnet run --project src/ReveilMusical.Cli -- alice lundi SOLEIL

# Plus aucune source musicale : la liste locale prend le relais
Music__Itunes__BaseUrl=http://localhost:9/ Music__MusicBrainz__BaseUrl=http://localhost:9/ \
  dotnet run --project src/ReveilMusical.Cli -- chloe dimanche TORNADE
```

```
Utilisateur : chloe
Morceau     : Good Morning — Gene Kelly (source : local-fallback)
Canal       : push
Délivré     : oui — mode dégradé
  ! itunes : iTunes indisponible (Connection refused (localhost:9))
  ! musicbrainz : MusicBrainz indisponible (Connection refused (localhost:9))
  ! Liste locale utilisée
```

### Tests et couverture

```bash
dotnet test --solution ReveilMusical.slnx --coverlet --coverlet-output-format cobertura
```

Le projet utilise le nouveau runner **Microsoft.Testing.Platform** (activé dans `global.json`), d'où `coverlet.MTP` à la place de `coverlet.collector`.

---

## Architecture

```
src/
├── ReveilMusical.Domain          Modèle + ports (interfaces). Aucune dépendance externe.
├── ReveilMusical.Application     Règles métier : orchestration, bascules, message. Ne connaît que les ports.
├── ReveilMusical.Infrastructure  Adaptateurs iTunes / MusicBrainz, cache, quota, faux SDK + adaptateurs,
│                                 mock des préférences, racine de composition (DI).
└── ReveilMusical.Cli             Point d'entrée appelé par l'ordonnanceur.
tests/
└── ReveilMusical.UnitTests       Tests par couche + tests d'architecture.
```

Les dépendances vont toujours vers le domaine : `Cli → Infrastructure → Application → Domain`.

```
                    WakeUpService (Application)
     ┌───────────────────┼─────────────────────────┐
     ▼                   ▼                         ▼
IUserPreferencesProvider ITrackResolver      INotificationDispatcher
     │                   │ FailoverTrackResolver   │ FailoverNotificationDispatcher
     │                   ▼                         ▼
     │            IMusicProvider ×N            INotificationChannel ×N
     │             Cache(Quota(iTunes))         EmailNotificationChannel → IMailClient   (faux SDK)
     │             Cache(Quota(MusicBrainz))    SmsNotificationChannel   → ISmsGateway   (faux SDK)
     │            ILocalTrackFallback          PushNotificationChannel  → IPushService  (faux SDK)
     ▼             HardcodedTrackFallback
LastKnown(InMemoryUserPreferencesProvider)
```

### Déroulé d'un réveil

1. **Préférences** : lecture via `IUserPreferencesProvider`.
2. **Choix du morceau** : `UserPreferences.TrackQueryFor(météo)` retourne le morceau choisi pour cette météo, sinon le morceau de secours.
3. **Résolution** : `FailoverTrackResolver` interroge les sources dans l'ordre de `Music:ProviderOrder`. En cas de panne, de quota atteint ou de résultat vide, il passe à la suivante. Si toutes échouent, il prend un morceau dans la liste locale, choisi selon la météo et le jour.
4. **Message** : titre selon le jour (« Bon mercredi ! »), corps selon la météo et le morceau.
5. **Envoi** : `FailoverNotificationDispatcher` essaie le canal préféré, puis `Notifications:FallbackOrder`, puis tout autre canal pour lequel l'utilisateur a une adresse.
6. **Compte rendu** (`WakeUpReport`) : morceau, source, canal, délivré ou non, mode dégradé, liste des incidents. Le service ne lève jamais d'exception vers l'ordonnanceur.

---

## Comment le code répond aux quatre exigences

### 1. Changer de source musicale rapidement

- Le métier ne voit que `IMusicProvider` (`Name` + `FindTrackAsync` → `Track(Title, Artist)`).
- Les DTO iTunes et MusicBrainz sont `internal` à l'infrastructure. `trackViewUrl` est désérialisé puis ignoré : il ne sort pas de l'adaptateur.
- **Ordre et activation par configuration** (`Music:ProviderOrder`) : on change de source, ou on en retire une, sans recompiler.
- **Ajouter une source** = une classe `IMusicProvider` + une ligne dans `ServiceCollectionExtensions` + son nom dans la configuration. Le métier ne bouge pas.
- **Quota iTunes (~20 req/min)** : chaque source est enveloppée par deux décorateurs :
  - `CachingMusicProvider` : `IMemoryCache`, durée de vie 12 h configurable. Une même recherche n'appelle la source qu'une fois. Seuls les succès sont mis en cache.
  - `RateLimitedMusicProvider` : fenêtre glissante d'une minute (20 pour iTunes, 50 pour MusicBrainz qui demande ~1 req/s). Au-delà, il échoue tout de suite au lieu d'attendre, car le réveil est à heure fixe. Le résolveur bascule alors sur la source suivante.
- **User-Agent MusicBrainz** : posé sur le client HTTP nommé (`ReveilMusical/1.0 ( contact )`), à partir de `Music:MusicBrainz`.

### 2. Plusieurs canaux de notification, et d'autres à venir

- Trois **faux SDK**, chacun avec une interface volontairement différente, comme le seraient de vrais SDK :

  | Faux SDK | Forme de l'API | Signalement d'échec |
  |---|---|---|
  | `IMailClient.SendMail(MailEnvelope)` | synchrone, objet message, HTML | `bool` |
  | `ISmsGateway.TransmitAsync(phone, text)` | asynchrone, paramètres à plat, 160 caractères max | code de statut (`200`, `413`, `503`) |
  | `IPushService.PushAsync(PushPayload)` | asynchrone, payload avec données libres | exception propriétaire `PushServiceException` |

- Trois **adaptateurs** (`Email/Sms/PushNotificationChannel`) les ramènent à `INotificationChannel`. Ils traduisent le format (HTML encodé, troncature SMS, data push) et uniformisent l'échec en `NotificationDeliveryException`.
- Les faux SDK n'envoient rien : ils écrivent dans les logs de la console. Une panne se simule par configuration (`Notifications:Fakes:*Outage`).
- Le canal est un `ChannelKey` (une chaîne normalisée), pas un `enum`. **Ajouter WhatsApp** = un adaptateur et une ligne d'enregistrement, sans toucher au domaine ni au métier. Un utilisateur qui préfère un canal non encore installé est quand même réveillé par un autre (c'est testé).

### 3. Vérification légale et fraîcheur des dépendances

- **Central Package Management** (`Directory.Packages.props`) : toutes les versions sont dans un seul fichier, à revoir à chaque ajout.
- **`scripts/check_dependencies.py`** passe en revue chaque paquet, direct ou transitif. Le script échoue si :
  - la licence n'est pas dans la liste blanche de `allowed-licenses.json` ;
  - le paquet a plus de 3 ans ;
  - le paquet est déprécié ;
  - …sauf si `allowed-licenses.json` contient une justification pour ce paquet.
- **NuGetAudit** (`Directory.Build.props`) : chaque restore vérifie les vulnérabilités connues, transitives comprises. Les avertissements sont traités comme des erreurs.
- **CI** (`.github/workflows/ci.yml`) : build, tests avec seuil de couverture de 90 %, contrôle des licences, contrôle des vulnérabilités. Elle tourne aussi chaque lundi, pour repérer une dépendance devenue obsolète sans qu'on ait touché au code.
- Le domaine ne dépend d'**aucun** paquet. L'application ne dépend que de deux paquets d'abstractions (`Logging.Abstractions`, `Options`).

### 4. Fiabilité : jamais de silence

| Panne | Réponse |
|---|---|
| Source musicale en erreur, lente (timeout de 3 s) ou sans résultat | bascule sur la source suivante |
| Quota d'une source atteint | refus immédiat, puis bascule |
| Toutes les sources en panne | liste locale codée en dur (`HardcodedTrackFallback`), qui ne peut pas échouer |
| Météo inconnue ou non couverte | morceau de secours de l'utilisateur |
| Canal préféré en panne, absent ou sans adresse | canaux de secours dans l'ordre configuré, puis tout autre canal connu |
| Service de préférences en panne | `LastKnownPreferencesProvider` ressert les dernières préférences connues |
| Configuration avec des entrées vides | ignorées, pas de plantage |
| Tout échoue | `WakeUpReport.Delivered = false`, log `Critical` et code de sortie `1` : l'échec est visible, jamais silencieux |

---

## Isolation et IoC / DI

- **Aucune implémentation concrète n'est créée avec `new`** dans le code métier ni dans les adaptateurs. Toutes les dépendances arrivent par le constructeur, et le conteneur `Microsoft.Extensions.DependencyInjection` les assemble dans `ServiceCollectionExtensions.AddReveilMusical`.
- Les décorateurs (cache, quota, dernières préférences connues) sont construits par `ActivatorUtilities.CreateInstance`, avec des services *keyed* pour la source décorée.
- Les `HttpClient` viennent de `IHttpClientFactory`.
- Les seuls `new` restants concernent des **valeurs** (records `Track`, `WakeUpNotification`, DTO, collections, exceptions), qui ne sont pas des dépendances.
- Les **tests d'architecture** (`ArchitectureTests`) vérifient automatiquement que :
  - le domaine ne référence que la BCL ;
  - l'application ne référence ni l'infrastructure, ni `System.Net`, ni d'autres paquets que des abstractions ;
  - aucun type ni membre du métier ne contient un mot propre à un fournisseur ou à un canal (`itunes`, `musicbrainz`, `trackview`, `http`, `mail`, `sms`, `push`, `whatsapp`) ;
  - les services métier ne reçoivent que des interfaces dans leur constructeur.
- `CompositionRootTests` construit le vrai conteneur (`ValidateOnBuild`) et joue des réveils de bout en bout, sans réseau.

---

## Dépendances : licences, versions et fraîcheur

Relevé du 2026-10-08 sur nuget.org, avec `python3 scripts/check_dependencies.py --markdown`. `dotnet list package --outdated` ne signale aucune mise à jour pour les paquets directs. `--vulnerable --include-transitive` ne signale aucune vulnérabilité.

### Paquets directs

| Paquet | Rôle | Projet | Version installée | Dernière stable | Licence |
|---|---|---|---|---|---|
| Microsoft.Extensions.Logging.Abstractions | `ILogger<T>` | Application, Infrastructure | 10.0.12 (2026-09-08) | 10.0.12 | MIT |
| Microsoft.Extensions.Options | `IOptions<T>` | Application | 10.0.12 (2026-09-08) | 10.0.12 | MIT |
| Microsoft.Extensions.Options.ConfigurationExtensions | liaison configuration → options | Infrastructure | 10.0.12 (2026-09-08) | 10.0.12 | MIT |
| Microsoft.Extensions.Http | `IHttpClientFactory` | Infrastructure | 10.0.12 (2026-09-08) | 10.0.12 | MIT |
| Microsoft.Extensions.Caching.Memory | cache des recherches musicales | Infrastructure | 10.0.12 (2026-09-08) | 10.0.12 | MIT |
| Microsoft.Extensions.Hosting | hôte, conteneur DI, configuration, logs console | Cli | 10.0.12 (2026-09-08) | 10.0.12 | MIT |
| xunit.v3 | framework de tests | UnitTests | 4.0.1 (2026-09-12) | 4.0.1 | Apache-2.0 |
| NSubstitute | doublures de test | UnitTests | 6.2.0 (2026-08-11) | 6.2.0 | BSD-3-Clause |
| coverlet.MTP | couverture de code | UnitTests | 10.1.0 (2026-09-27) | 10.1.0 | MIT |

Toutes les licences sont **permissives** (MIT, Apache-2.0, BSD-3-Clause). Il n'y a **aucun copyleft** (GPL, LGPL, AGPL), donc aucune obligation de publier le code et aucun frein à une levée de fonds. Apache-2.0 demande seulement de conserver les mentions NOTICE si on redistribue, ce qui ne concerne de toute façon que les outils de test.

Runtime : .NET 10 (LTS, supporté jusqu'en novembre 2028), SDK 10.0.112, licence MIT.

### Points d'attention et justifications

| Composant | Point d'attention | Décision |
|---|---|---|
| **Castle.Core 5.1.1** (transitive, tests) | Publiée en 2022, alors que la 5.2.1 existe | Version minimale exigée par NSubstitute 6.2.0. Licence Apache-2.0, aucune CVE connue, utilisée uniquement dans les tests. **Acceptée et documentée** dans `allowed-licenses.json`. On pourrait forcer la 5.2.1, mais ce ne serait pas la version testée par NSubstitute. |
| **Microsoft.Bcl.AsyncInterfaces 6.0.0** (transitive, tests) | Publiée en 2021 | Tirée par Castle.Core. C'est un polyfill d'API déjà présentes dans .NET 10, donc sans effet à l'exécution. **Acceptée.** |
| **Microsoft.Win32.Registry 5.0.0** (transitive, tests) | Publiée en 2020 | Dernière version publiée : le paquet n'évolue plus car l'API est intégrée au runtime. Il est tiré par Microsoft.Testing.Platform. **Acceptée.** |
| **Microsoft.ApplicationInsights 2.23.0** (transitive, tests) | Ce n'est pas la dernière majeure (3.1.2). Surtout, elle sert à la **télémétrie** du runner de tests. | La télémétrie est désactivée en CI (`TESTINGPLATFORM_TELEMETRY_OPTOUT=1`). Rien de cela n'est livré en production. |
| **Microsoft.Testing.Platform 2.4.x** (transitive, tests) | La 2.5.1 est sortie le 2026-10-07 | Version fixée par xunit.v3 4.0.1. Elle suivra à la prochaine version de xunit. |
| **xunit.v3** plutôt que xunit 2.9.3 | xunit 2 ne reçoit plus que des correctifs | On prend la branche maintenue. |
| **NSubstitute** plutôt que Moq | Moq 4.20 avait embarqué SponsorLink, qui collectait des emails de développeurs (2023) | NSubstitute, sous licence BSD-3, n'a pas cet historique. |
| **Pas de Polly ni de System.Threading.RateLimiting** | — | La bascule et le quota tiennent en quelques dizaines de lignes testées. Ne pas les ajouter, c'est deux dépendances de moins à auditer. |

### APIs externes (pas des paquets, mais elles engagent aussi l'entreprise)

| Service | Accès | Conditions |
|---|---|---|
| iTunes Search API | gratuit, sans clé, ~20 req/min | Conditions d'utilisation d'Apple : usage destiné à promouvoir le contenu iTunes. **À valider par le service juridique avant la production.** C'est une raison de plus pour pouvoir changer de source. |
| MusicBrainz API | gratuit, sans clé, ~1 req/s, User-Agent obligatoire | Données de base sous licence CC0. Un usage commercial intensif doit passer par un accord avec MetaBrainz. |

<details>
<summary>Inventaire complet (directes + transitives), généré par le script</summary>

| Paquet | Installée | Publiée | Dernière stable | Licence | Type | Statut |
|---|---|---|---|---|---|---|
| Castle.Core | 5.1.1 | 2022-12-30 | 5.2.1 (2025-03-09) | Apache-2.0 | transitive | revue |
| coverlet.MTP | 10.1.0 | 2026-09-27 | 10.1.0 (2026-09-27) | MIT | directe | OK |
| Microsoft.ApplicationInsights | 2.23.0 | 2025-02-19 | 3.1.2 (2026-05-28) | MIT | transitive | OK |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | 2021-11-08 | 10.0.12 (2026-09-08) | MIT | transitive | revue |
| Microsoft.Extensions.Caching.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Caching.Memory | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | directe | OK |
| Microsoft.Extensions.Configuration | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.Binder | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.FileExtensions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.Json | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Configuration.UserSecrets | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Diagnostics | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Diagnostics.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.FileProviders.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.FileProviders.Physical | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.FileSystemGlobbing | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Hosting | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | directe | OK |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Http | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | directe | OK |
| Microsoft.Extensions.Logging | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | directe | OK |
| Microsoft.Extensions.Logging.Configuration | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Logging.Console | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Logging.Debug | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Logging.EventLog | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Logging.EventSource | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Extensions.Options | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | directe | OK |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | directe | OK |
| Microsoft.Extensions.Primitives | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| Microsoft.Testing.Extensions.Telemetry | 2.4.0 | 2026-09-02 | 2.5.1 (2026-10-07) | MIT | transitive | OK |
| Microsoft.Testing.Extensions.TrxReport.Abstractions | 2.4.0 | 2026-09-02 | 2.5.1 (2026-10-07) | MIT | transitive | OK |
| Microsoft.Testing.Platform | 2.4.1 | 2026-09-16 | 2.5.1 (2026-10-07) | MIT | transitive | OK |
| Microsoft.Testing.Platform.MSBuild | 2.4.0 | 2026-09-02 | 2.5.1 (2026-10-07) | MIT | transitive | OK |
| Microsoft.Win32.Registry | 5.0.0 | 2020-11-09 | 5.0.0 (2020-11-09) | MIT | transitive | revue |
| NSubstitute | 6.2.0 | 2026-08-11 | 6.2.0 (2026-08-11) | BSD-3-Clause | directe | OK |
| System.Diagnostics.EventLog | 10.0.12 | 2026-09-08 | 10.0.12 (2026-09-08) | MIT | transitive | OK |
| System.Security.AccessControl | 6.0.1 | 2024-04-09 | 6.0.1 (2024-04-09) | MIT | transitive | OK |
| xunit.analyzers | 2.1.0 | 2026-09-12 | 2.1.0 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3 | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | directe | OK |
| xunit.v3.assert | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3.common | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3.core.mtp-v2 | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3.extensibility.core | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3.mtp-v2 | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3.runner.common | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |
| xunit.v3.runner.inproc.console | 4.0.1 | 2026-09-12 | 4.0.1 (2026-09-12) | Apache-2.0 | transitive | OK |

</details>

---

## Limites et pistes

- Les préférences viennent d'un mock en mémoire. Le vrai service sera un nouvel `IUserPreferencesProvider`, qui reste enveloppé par `LastKnownPreferencesProvider`.
- Le cache et la mémoire du quota sont propres à chaque processus. Si le service tourne sur plusieurs instances, il faudra un cache distribué derrière le même décorateur.
- Il n'y a pas de disjoncteur (*circuit breaker*) : une source en panne est réessayée à chaque réveil, ce qui coûte au plus 3 s de timeout. Si le volume grossit, un décorateur supplémentaire pourra la court-circuiter.
