# Execute one backlog task

## Scope and plan

Owner selects one issue. Read AgentStart and its brief before editing. Provide the six-line
Codex setup required by AGENTS.md. Create a dedicated branch from the agreed integration
state. If dependency code is absent, report the prerequisite; do not silently merge or
cherry-pick other work. A closed issue alone is not evidence its code exists here.

Keep a short current milestone at the top of ImplementationPlan.md: issue, source commit,
scope, ordered checklist, decisions, verification and blockers. Preserve history below.
Do not create empty runtime classes/TODO stubs just to match proposed filenames.

## Cycle

1. Reconfirm the problem in current code. For behavior changes, add meaningful regression
   coverage, not a test merely repeating an implementation constant.
2. Apply only the selected patch and necessary generation/validation changes.
3. Compile/run targeted tests, resolve failures, then perform the full AGENTS.md runtime
   completion matrix before declaring implementation done.
4. Inspect git diff --check and git diff --stat; stage explicit task paths only.
5. Create a focused local commit after applicable validation passes. No push, PR, merge,
   tag, deploy or issue closure without corresponding authorization.
6. Keep the brief and GitHub body synchronized if scope changes. Report unvalidated gates.

Documentation-only scaffolding uses path/symbol/link checks, dependency validation,
brief/issue consistency and absence of runtime/generated diffs. It does not substitute
for runtime validation when implementation begins.

## Unity commands and gates

Operations.md owns procedures: sections 2 (prerequisites), 6 (Asset Database), 7 (Existing
Build), 8 (tests/validator), 9a (WebGL), 12 (remote). Before batch work close an interactive
editor using this checkout; never kill it automatically.

Unity is a GUI executable on Windows. Wait for the process and inspect exit code AND XML/log.
A filtered example, from repository root:

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
$repoPath = (Get-Location).Path
$null = New-Item -ItemType Directory -Force -Path (Join-Path $repoPath 'Logs')
$unityArgs = @(
    '-batchmode', '-nographics',
    '-projectPath', ('"{0}"' -f $repoPath),
    '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'AddressablesSample.Game.Tests.EditMode.GameControllerTests',
    '-testResults', 'Logs/Issue-EditMode.xml', '-logFile', 'Logs/Issue-EditMode.log'
)
$unityProcess = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -Wait -PassThru -WindowStyle Hidden
if ($unityProcess.ExitCode -ne 0) { throw 'Unity test process failed; inspect the log.' }
# Inspect XML for a completed run, expected test count and failures/skips.
```

Do not add -quit to -runTests. For -executeMethod use -quit and a unique log.
For long execution, wait on its session while communicating; do not start a second Unity
process in the same checkout.

| Order | Runtime completion check | Evidence |
|---|---|---|
| 1 | Local + Asset Database; generated verification | Structural log; fingerprints after P03 |
| 2 | All EditMode tests | XML/log and source commit |
| 3 | All PlayMode tests, Asset Database | Separate XML/log |
| 4 | Fresh desktop local Addressables build | Build log |
| 5 | All PlayMode tests, Existing Build | Separate XML/log |
| 6 | Restore Local + Asset Database; structural validation | Restore/validation logs |
| 7 | Audit changed ownership and stale results | Short written assessment |
| 8 | If WebGL/UI affected, local browser player | Build, browser, viewport and recovery evidence |

Explicit local WebGL destination (requires installed Web Build Support):

```powershell
.\Tools\Build-WebGlDemo.ps1 -OutputPath (Join-Path (Get-Location).Path 'Builds/WebGL')
```

Do not deploy output. Restore/verify desktop target before desktop tests after WebGL.
Inspect incidental generated changes; never reset unrelated edits blindly.

## Handoff

- Issue, brief, resulting local commit.
- Observable behavior changed.
- Commands/Operations routes, platform/workflow, XML/log paths and results.
- Unvalidated checks and exact blocker.
- Ownership/cancellation implications and remaining decisions.
- Continue current task for unfinished gates; start a new task for the next independent issue.

Logs/builds stay ignored. No credentials, tokens or full local logs in documentation.
