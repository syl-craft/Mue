# NIN-348 T3 — corrections avant Mue 0.2.0

Les points 2, 4 et 5 sont corrigés sur `sylvain/t3-processus-mac`, sans changer les contrats publics 0.1.0.

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

**À brancher en 0.2.0 (points 1 et 3 différés).** `MacBlockerScan` conserve les PID non inspectables,
leur état et `Incomplete`. `MacCloseResult` distingue déjà `Closed`, `Gone`, `IdentityChanged`,
`StillRunning`, `AccessDenied`. Remplacer `MacUpdateBlockerAdapter` par l'adaptation vers
`UpdateBlockerQuery`, `UpdateBlockerScan` et `UpdateBlockerCloseResult` définitifs de DockPad,
puis exposer les états et libellés à l'hôte. Actuellement, l'adaptateur omet les PID sans appartenance
connue au bundle, conserve les membres non inspectables avec `Started = 0`, perd `Incomplete`,
et réduit les trois premiers résultats à `true`, les deux derniers à `false`.
Les objets recréés ou transmis à une autre instance ne permettent pas de signaler un processus vivant.

**Vérifications Windows :** `dotnet build Mue.sln -m:4` et `dotnet test -m:4`, zéro avertissement ;
100 tests réussis, 9 natifs ignorés. Les tests natifs portent `MacNative` **et** `RealMachine`.
Ils couvrent notamment le rejet noyau d'un token de génération périmée ; les tests purs injectent
une réutilisation du PID entre la dernière lecture et le signal. Aucune connexion au Mac tentée.
Les signatures, permissions effectives et scénarios natifs restent à valider sur macOS 15+ avec
`dotnet test tests/Mue.Tests/Mue.Tests.csproj -m:4 --filter Category=RealMachine`.
