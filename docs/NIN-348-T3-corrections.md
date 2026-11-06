# NIN-348 T3 — intégration au contrat Mue 0.2.0

Les points 1 à 5 sont traités sur `sylvain/t3-processus-mac`, déjà rebasée sur
`sylvain/b2-contrat-0-2-0` (`9fbd25e`). Le contrat 0.2.0, `Mue.Wpf`, le finder Windows
et le backend Velopack restent inchangés. Aucun ajout au contrat n'est nécessaire.

- **Signal lié à l'identité.** Le token complet vient de `task_name_for_pid` puis
  `task_info(TASK_AUDIT_TOKEN)`, dès la détection. Ces API publiques évitent de fabriquer un
  token à partir des champs privés de `proc_pidinfo` ; aucun hypothétique `proc_pidtoken` n'est utilisé.
  Le port Mach est libéré. Le chemin est lu avec `proc_pidpath_audittoken`, entouré de contrôles
  d'identité. Le token est attaché à l'objet détecté et n'est jamais remplacé par un nouveau scan.
  Voir les déclarations Apple de [task_info](https://github.com/apple-oss-distributions/xnu/blob/xnu-11215.1.10/osfmk/mach/task.defs)
  et [TASK_AUDIT_TOKEN](https://github.com/apple-oss-distributions/xnu/blob/xnu-11215.1.10/osfmk/mach/task_info.h).
- **Disponibilité et permissions.** Cible macOS 15+. La déclaration de
  [libproc](https://github.com/apple-oss-distributions/xnu/blob/xnu-11215.1.10/libsyscall/wrappers/libproc/libproc.h)
  expose `proc_signal_with_audittoken`. L'analyse du
  [noyau XNU 11215](https://github.com/apple-oss-distributions/xnu/blob/xnu-11215.1.10/bsd/kern/proc_info.c)
  montre que `_proc_signal_send` conserve une référence au processus, vérifie sa génération,
  puis les permissions `cansignal`, avant de remettre le signal à cette même référence.
  La protection contre la réutilisation du PID repose sur cette opération native, pas sur une
  atomicité prétendue de nos lectures. Un refus d'accès au token ou l'absence du symbole de signal
  interdit la fermeture : aucun repli `kill(pid, ...)`, même pour SIGTERM.
  `task_name_for_pid` reste soumis aux permissions utilisateur et à la politique système
  ([implémentation Apple](https://github.com/apple-oss-distributions/xnu/blob/xnu-11215.1.10/bsd/kern/kern_proc.c)).
- **Chemins et délai.** Le chemin original validé arrive intact à `realpath`. Tests pur et natif
  du parent d'un lien symbolique : `/tmp/<fixture>/alias/../Test.app`, avec un bundle lexical
  concurrent. Délai par défaut de cinq secondes, configurable par constructeur ; horloge interne
  `TimeProvider`. Test pur d'une sortie à quatre secondes, délai configurable et absence d'escalade ;
  test natif d'un SIGTERM traité pendant trois secondes.

**Contrat 0.2.0 (points 1 et 3).** `Find(query, classifier, enrich)` retourne directement
`UpdateBlockerScan`. Les PID dont le chemin est illisible sont conservés avec `Unavailable`,
même si leur appartenance aux chemins demandés reste inconnue. Les processus disparus sont exclus,
ainsi que ceux dont le chemin connu est hors de la requête ou explicitement ignoré par l'hôte.
Une identité illisible pour un exécutable retenu donne également `Unavailable` : pas de
classification ni de présélection, `Started = 0`, `Incomplete = true` et fermeture `AccessDenied`.
Les tests couvrent ces états avec et sans enrichissement, y compris le libellé d'indisponibilité.

`UpdateBlockerQuery.Paths` accepte plusieurs dossiers et fichiers absolus, résolus sans réduction
lexicale préalable de `..`. Le filtrage Mac reste sensible à la casse, sans modifier `Contains`
du contrat partagé. Les chemins qui se recouvrent ne dupliquent pas les processus ; une requête
vide ne les énumère pas. Le classifieur reçoit la requête résolue. `IncludeCurrentProcess` permet
de lister le processus courant, sans autoriser sa fermeture.

`CloseAsync(blocker, force, token)` retourne directement `UpdateBlockerCloseResult` :
`Closed`, `Gone`, `IdentityChanged`, `StillRunning`, `AccessDenied`. L'adaptateur 0.1.0 et les
types intermédiaires de scan et de fermeture sont retirés. L'annulation est vérifiée avant
l'inspection, avant le signal et pendant l'attente ; elle n'entraîne ni nouveau signal ni escalade.
Les objets recréés ou transmis à une autre instance ne permettent pas de signaler un processus vivant.

**Vérifications Windows :** `dotnet build Mue.sln -m:4` et `dotnet test -m:4`, zéro avertissement ;
147 tests réussis, 9 natifs ignorés. Les tests natifs portent `MacNative` **et** `RealMachine`.
Ils couvrent notamment le rejet noyau d'un token de génération périmée ; les tests purs injectent
une réutilisation du PID entre la dernière lecture et le signal. Aucune connexion au Mac tentée.
Les signatures, permissions effectives et scénarios natifs restent à valider sur macOS 15+ avec
`dotnet test tests/Mue.Tests/Mue.Tests.csproj -m:4 --filter Category=MacNative`.
La recette DockPad et ses rendus n'ont pas été rejoués ; aucune version ni aucun paquet n'est publié.
