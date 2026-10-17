# Phase 2 stage 1 — final audit and sequential merges (2026-10-17)

Repository: ModernFormsNext.WebView only. The framework checkout was read-only; its
untracked configuration was preserved. Pin `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`
was not changed. Initial local/remote main was `79eaff579755578ce5a1faf19c861ffba86719d1`.

## Review and acceptance

All incremental source, tests, samples, documentation and workflow differences were reviewed.
No blocking issue was found. The shared changes are additive/platform-neutral; baseline
constructors, script results and string operations remain compatible. Native hosting,
focus/layout/DPI/Activity ownership remain in the framework. No C# handler or origin
authentication is inferred from Source, request IDs, transport snapshots or RPC replies.

- #33 / PR #36: original callback identity, untrusted source/frame/redirect matrix,
  lifecycle rejection, reserved unavailable authentication capability. No new dependencies.
- #11 / PR #37: borrowed transport, independent cancellation/disposal, canonical context,
  stale/reentrant forwarding, real JSON/string validation. Includes Android POST load-state
  ordering and Windows sample control disposal before profile cleanup. Retry limits unchanged.
- #12 / PR #38: v1 typed JSON, IDs/documents, bounded size/depth/pending state, deadline
  across submission/response, advisory cancellation, safe errors, duplicates/out-of-order and
  strict parsing. Pagehide removes endpoint state; cancellation wins over converter failure.

This meets the three scoped issue criteria. Release, privileged platform transport, physical
qualification and C# handlers are separate gates. Issues #33, #11 and #12 were closed only
after the merges and final main CI succeeded; no umbrella/release issue was closed.

## Exact-head evidence and history

| PR | Audited final head | Successful CI | Normal merge commit |
| --- | --- | --- | --- |
| [#36](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/36) | `5466e712ed7bd276420a8374c105f3a878293f39` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39043213480), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39043209196) | `8486ee30c8bff5c366ad37bc69e077efa9e9e741` |
| [#37](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/37) | `56e086c384515d1cdc734e9305ab6b71ffc10c10` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39048374182), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39048370513) | `98dc22f3306c99ea3cb5822a4271ef0748cf2d0f` |
| [#38](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/38) | `4d4c587a693bcd8e1ebcf074ac0cf0b82c2947f8` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39048584052), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39048577566) | `f4017e7be66eeebf235e9e296caf1e5e7855ad19` |

After each merge, local main was fast-forwarded. #37/#38 were retargeted to main; ordinary
integration merges incorporated the preceding merge without changing their file trees.
Their new heads passed all three jobs before merging. No squash, rebase or force push.
The first-parent chain is #36 → #37 → #38, every merge has two parents, all reviewed heads
are ancestors and final main's tree equals `91bf3dd7fbc7feafcdc04a7a50c23100d27b825d`.
Local/remote main matched `f4017e7`; [post-merge main CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39048806054)
is terminal SUCCESS (shared/Windows/Android). Earlier main CI after #36 and #37 also succeeded.

## Fresh local validation

On the exact combined tree now merged into main: restore, full sequential Debug/Release
solution builds, 0 warnings/errors; 166/166 tests each (139 shared, 15 Windows, 12 Android),
zero skips. Both Windows WebView2 native and Android signed APK/emulator native suites:
RUNTIME PASS in Debug and Release. All resource/security/transport/RPC/navigation/lifecycle
fixtures ran, including 50 leases, POST reentrancy and Windows reparent/replay.

Commands followed [testing](testing.md): pinned bootstrap/source root, solution restore,
build `--no-restore -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false`,
test `--no-build --no-restore -m:1`, Windows sample `--validate`, Android sample
`-t:SignAndroidPackage` with JDK21, explicit `adb -s emulator-5554` installation/validation
Intent and PID-filtered logs. Local artifacts: `artifacts/merge36-38-audit/` (ignored).
No new source fix was required in this final audit.

Windows: WebView2 155.0.4283.60. Android: API34 x86_64, provider
com.google.android.webview/113.0.5672.136. This is automated native execution, not manual UIA
or human visual qualification. API23 runtime, physical device, foreground keyboard injection,
interactive VS Designer, full IME/DPI/accessibility/retained-heap soak and published trimming/
AOT remain NOT EXECUTED. Templates unchanged; no template verification needed. No publication.

Next: #16 executable AndroidX research and decision, then independent Windows #15 hardening
and security qualification; #13 stays gated on supported, verified platform authentication.
