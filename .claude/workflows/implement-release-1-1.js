export const meta = {
  name: 'implement-release-1-1',
  description: 'Implement plans/release-1.1-spec.md phases B0 to B5 sequentially, each followed by an independent verify-and-fix pass',
  phases: [
    { title: 'B0', detail: 'baselines, draft PR, agent-rule edits' },
    { title: 'B1', detail: 'parser and directives' },
    { title: 'B2', detail: 'class writer, #encode removal, culture, snapshots' },
    { title: 'B3', detail: 'runtime tests and package consumers' },
    { title: 'B4', detail: 'docs, versions, prose' },
    { title: 'B5', detail: 'audit, measurements, PR' },
  ],
}

const SPEC = 'plans/release-1.1-spec.md'
const MAP = `Spec section map (line numbers in ${SPEC}, verify before relying; read ranges with Read offset/limit, never the whole 350KB file):
How to use 22-38; checklist 69-168; goals/non-goals 170-205; decisions 207-366 (2.3 owner decisions O11-O20 at 252, 2.6 spec decisions S33-S54 at 332); 4 consumer design 382-652 (4.11 culture ~586); 5 diagnostics 653; 6 generator internals 688-808 (6.7 simplification targets 779); 7 parser 809-902; 8 emitter 903-1008; 9 performance 1009; 10 AOT 1046; 11 testing 1062-1289 (11.2 generator tests 1074, 11.3 runtime tests 1214, 11.4 package validation 1255); 12 packaging 1290; 13 CI/publishing 1324 (13.4 guardrails 1361); 15 docs 1461-1657 (15.4 at 1494, 15.7 at 1625); 16 hygiene 1658; 17 file dispositions 1692; 18 execution plan 1786-1919 (phases B0-B6); 19 risks 1920; appendices A (parser sketch) 1995, B (DelimiterLevels) 2105.`

const PREAMBLE = `You are implementing part of the AnglePlates 1.1 release in C:\\GitHub\\SlusserLabs\\AnglePlates on branch release-1.1 (shared working tree; other stages run one after another, never concurrently). The spec is ${SPEC}.
Before anything else: read AGENTS.md fully (it binds you: skill routing, emitted-code global:: rule, testing conventions, guardrails) and spec "How to use this spec" (lines 22-38). Invoke the skills AGENTS.md routes you to (/csharp for any C# including emitted strings, /dotnet-tunit for tests, /docs for docs/, /humanizer for human-facing prose and code comments but never AGENTS.md/plans/README AI Disclosure) via the Skill tool before writing in those areas. Use the Edit tool for single-file edits (user rule); respect .editorconfig/.gitattributes line endings per file (this spec file and *.md/*.cs are CRLF). Use semicolons, not em dashes, in prose and comments. Never persist helper scripts/reports in the repo. Scratch files go in C:\\Users\\jacob\\AppData\\Local\\Temp\\claude\\C--GitHub-SlusserLabs-AnglePlates\\6a22c703-bdce-4a0c-85da-edb23600347f\\scratchpad. Python only via uv run.
Commit rules: the spec and AGENTS.md authorize commits and pushes on release-1.1 (git credentials already on the machine). Small commits, every commit must build; tick finished checklist items (- [x]) in ${SPEC} in the same commit as the work, with short notes for deviations/measurements, keeping CRLF. Never merge the PR, never change repo settings, environments, variables, rulesets, never approve deployments, never dispatch release.yml in these phases, never publish to any feed. Co-author trailer for commits: "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>".
If a STOP condition in the spec triggers, or the spec conflicts with the repository in a way that needs a guess, do NOT guess: set stop=true and explain.
${MAP}
Be exhaustive and exact: the spec is detailed and authoritative; read every section your unit references (including cross-referenced S/O decisions) before editing. Report honestly what was verified and what wasn't.`

const IMPL = {
  type: 'object',
  properties: {
    done: { type: 'boolean' },
    stop: { type: 'boolean' },
    summary: { type: 'string' },
    commits: { type: 'array', items: { type: 'string' } },
    deviations: { type: 'array', items: { type: 'string' } },
  },
  required: ['done', 'stop', 'summary'],
}
const VERIFY = {
  type: 'object',
  properties: {
    ok: { type: 'boolean' },
    stop: { type: 'boolean' },
    issuesFound: { type: 'array', items: { type: 'string' } },
    issuesFixed: { type: 'array', items: { type: 'string' } },
    remaining: { type: 'array', items: { type: 'string' } },
  },
  required: ['ok', 'stop', 'remaining'],
}

async function unit(phaseName, id, task, accept) {
  phase(phaseName)
  log(`Starting ${id}`)
  const impl = await agent(`${PREAMBLE}\n\nYOUR UNIT: ${id}\n${task}\n\nWhen finished, make sure everything is committed (and pushed if the unit says so) and return the structured summary.`, { label: `impl:${id}`, phase: phaseName, schema: IMPL })
  if (!impl) return { ok: false, reason: `${id}: implementer failed` }
  if (impl.stop) return { ok: false, stop: true, reason: `${id}: ${impl.summary}` }
  let last = null
  for (let round = 1; round <= 3; round++) {
    const v = await agent(`${PREAMBLE}\n\nYOU ARE AN INDEPENDENT VERIFIER (skeptical; default to "not ok" if unproven) for unit ${id}. The implementer claims: ${impl.summary}\nDo not trust that claim; inspect git log/diff and the working tree yourself, run the build (-warnaserror) and the relevant tests, run the spec's greps/acceptance checks, and compare against the spec sections the unit covers. Fix every defect you find (small commits, tick/untick the checklist honestly in the spec). Anything you cannot fix goes in remaining; set stop=true only for a real STOP condition.\nUnit task that was assigned:\n${task}\nAcceptance to verify:\n${accept}`, { label: `verify:${id}#${round}`, phase: phaseName, schema: VERIFY })
    last = v
    if (!v) return { ok: false, reason: `${id}: verifier failed` }
    if (v.stop) return { ok: false, stop: true, reason: `${id}: verifier stop: ${JSON.stringify(v.remaining)}` }
    if (v.ok) return { ok: true, impl, verify: v }
    log(`${id} verify round ${round}: remaining ${JSON.stringify(v.remaining)}`)
  }
  return { ok: false, reason: `${id}: verification did not converge`, verify: last }
}

const UNITS = [
  ['B0', 'B0', `Do Phase B0 of the spec (section 18, lines ~1796-1804, plus checklist B0 and 13.4/15.4 for the exact agent-rule wording). Step 1 is probably already done (spec commit exists at HEAD; confirm git status is clean and plans/icon-draft and release-1.0-spec.md are gone). Record baselines (line counts per 6.7; sample output captured to the scratchpad; baseline benchmarks per 9.2 and Native AOT size, using a separate git worktree of the unchanged tree outside the repo if useful, or fall back to the CI-recorded sizes if the toolchain is missing, saying so); run gh auth status (STOP G1 if it fails); push and open the DRAFT PR exactly as step 3 says; update AGENTS.md Commits/credential bullets and docs/releasing.md guardrails/preflight lines 1, 2, 8/owner-inputs checklist per 13.4/15.4 and commit as one commit; tick checklist items with notes (put the baseline numbers in the notes since the PR description comes later). Do not file GitHub issues. Wait for CI to run on the draft PR once and note it.`,
   `Baselines recorded in the checklist notes; draft PR exists with a CI run; agent-rule edits committed and consistent between AGENTS.md and docs/releasing.md; no issues filed; checklist B0 ticked honestly.`],
  ['B1', 'B1a-parser', `Do Phase B1 steps 1 to 5 (section 18 lines ~1806-1819; read sections 7, 7.4, Appendix A, 11.2 TemplateParserTests/DiagnosticTests rows, S/O decisions about bracket levels): rewrite TemplateSyntax and TemplateParser, delete CSharpBlockScanner.cs, TemplateReader.cs, CSharpBlockScannerTests.cs with the minimal TemplateClassWriter edits exactly as step 2 describes (no layout changes yet; do not accept or edit any .verified file), rewrite TemplateParserTests, update DiagnosticTests for SLAP004 levels, run the focused tests, measure parser coverage per step 5. Tick matching checklist items. Push after committing so CI runs.`,
   `Solution builds with -warnaserror; TemplateParserTests and DiagnosticTests pass; every 11.2 row for them exists including adjacent-literal invariant and two linear-time guards; TemplateParser and TemplateSyntax at 100% line/branch (except a GetCloser default arm) measured; deleted types unreferenced in src; no .verified file edited; each commit builds.`],
  ['B1', 'B1b-directives', `Do Phase B1 steps 6 to 8 (section 18 lines ~1815-1819; read 4.2, 6.2, S49, S50, S51, O17, 11.2 DirectiveReaderTests/HintNameTests/DiagnosticTests rows, 15.4 AGENTS.md directive-values bullet): simplify DirectiveReader (no Roslyn checks, collapsed Read* methods, blank check, empty-derived-#type SLAP007; leave #encode for B2), delete SLAP001/SLAP002 descriptors, ClassNameNotDerived/NamespaceNotDerived and AnalyzerReleases.Unshipped rows, sanitize hint names in TemplateGenerator.CreateHintPrefix, update DirectiveReaderTests (B1 rows), HintNameTests (no-CS8785 rows), DiagnosticTests retirement test, add the AGENTS.md directive-values bullet. Run the focused tests and a -warnaserror build (no RS20xx). Tick checklist items; push.`,
   `-warnaserror build clean; TemplateParserTests, DiagnosticTests, DirectiveReaderTests, HintNameTests pass; Roslyn-validation grep from 6.7 finds only TemplateEncoding hits; DirectiveReader.cs <= 200 lines or noted; no .verified file edited; checklist B1 fully ticked with notes; CI ran on the push with expected failures noted.`],
  ['B2', 'B2a-writer-encode', `Do Phase B2 steps 1, 2, 3 and 5 plus the related test updates (section 18 lines ~1821-1831; read section 8 fully, 8.7, S39, S48, S54, O14, O16, 11.2, 11.3 where they touch these changes): C0 layout in TemplateClassWriter, #encode and presets removal (TemplateEncoding.cs deleted, DirectiveKind etc., constructor fallback, preset deletion in SupportTypes), the B2 rows of DirectiveReaderTests, DirectiveSpellCheckTests, RenderingTestHelper comment, Runtime.Tests preset/#encode changes and Templates/Encoding deletion, samples/HelloWorld (Encoder = null; output must be byte-identical with the B0 capture), generator test for // <auto-generated> first line and .g.cs hint names, AGENTS.md hard-line bullet, generated-code bullet, sample wording. Snapshots may stay unaccepted in this unit if the build/tests need them; keep every commit building. Tick items; push.`,
   `Build -warnaserror clean; sample output byte-identical to B0 capture; no TemplateEncoding or #encode left except the allowed S53 hits; auto-generated header test exists; no #pragma emitted; AGENTS.md bullets present.`],
  ['B2', 'B2b-culture-snapshots', `Do Phase B2 steps 4, 6 and 7 (section 18 lines ~1826-1831; read 4.11, S37, S43-S45, 11.2 snapshot rows, 11.3): implement TemplateOptions.Culture and the conversion helpers in SupportTypes.cs per 4.11 (all emitted types global::-qualified, XML docs warning-free, DocumentationModeTests), the S37 comment edit, "Culture" in Options_Properties_AreInitOnlyAndClassIsSealed, culture default-values test; update BlockSyntaxTests (keep brace-in-string row), LineMappingTests, TemplateSnapshotTests, TemplateOptionsTests, NameCollisionTests per 11.2; then regenerate snapshots and REVIEW EVERY DIFF BY HAND (never blindly accept): RenderAsync, both BracketsAndEmptyBlocks, MergedLiteralRuns, new BracketLevels and MultiLineCSharp, all eight support types; delete EscapedOpeners. Record a per-snapshot note (file, what changed, why) in the checklist notes for the PR. Run full generator suite and Runtime.Tests. Tick items; push.`,
   `Generator tests and Runtime.Tests pass; snapshot changes limited to those the spec allows ((a),(b),(c) for RenderAsync, support types only presets removed/culture/class summary/S37 line); per-snapshot notes recorded; DocumentationModeTests warning-free; directive-validation grep clean; EscapedOpeners deleted.`],
  ['B3', 'B3a-runtime', `Do Phase B3 step 1 and the runtime parts of step 3 (section 18 lines ~1833-1840; read 11.3 fully, 4.11, 11.2 where relevant): Templates/Syntax fixtures and SyntaxTests (with encoder rows moved from BlockSyntaxTests), Templates/Readme/levels.html and ReadmeExampleTests entry, CultureTests with null-throws row, Templates/Culture/*, invoice partial and models, TemplateDefaultsTests registrations, S37 wording and rename in ConversionTests and OutputTests. Fixtures must be warning-free under -warnaserror. Run both test projects and the sample diff. Tick items; push.`,
   `-warnaserror build; both test projects pass including every culture row; output-parity matrix unchanged; sample byte-identical; checklist items for runtime tests ticked.`],
  ['B3', 'B3b-consumers', `Do Phase B3 steps 2 to 4 (section 18 lines ~1836-1840; read 11.4, 8.7, 10, Appendix B): delete ScannerParity, BuildErrors/Slap001 and Slap002; add DelimiterLevels (Appendix B; verify every expected string against the ACTUAL render, don't trust the spec's strings unverified) and StrictAnalyzers (zero warnings; relaxations only in a [Program.cs] section) and BuildErrors/StrictAnalyzersControl, Cs1026; update Slap004, Cs0103, NativeAot (bracket levels, culture check, options not presets), DefaultNames (no #encode none), validate-package.ps1 (dotfile copy via Get-ChildItem -Force | Copy-Item -Recurse -Force, registrations), tests/AnglePlates.PackageTests/README.md, ci.yml comment. Then clear artifacts/package, pack 1.1.0-local.<yyyyMMdd>.<n> and run eng/validate-package.ps1 (use -SkipNativeAot only if the toolchain is missing, and say so). Tick items; push and check CI, noting ubuntu .editorconfig copy evidence (run ID) in the checklist.`,
   `Fresh pack + validate-package.ps1 passes with every consumer (NativeAot included or skip noted); StrictAnalyzers zero warnings; no Slap001/Slap002 folders; DelimiterLevels strings verified against real render; CI status for the push recorded.`],
  ['B4', 'B4a-docs-core', `Do Phase B4 step 1 for README.md, docs/templates.md, docs/diagnostics.md, docs/rendering.md (section 18 lines ~1842-1849; read 15 fully, 15.4 especially, 16.8, 4.1-4.11, 5, 15.7, O12, O16, O17). Use /docs for docs/, /humanizer for README and prose, and match the Author Voice in AGENTS.md (plain American English, concrete subjects). Defaults must name the mechanism that changes them (15.7). Keep implemented behavior separate from planned work; backtick keywords and type names; README links are absolute https URLs. Tick items; push.`,
   `Docs describe only 1.1 behavior; no #encode/preset mentions except the S53 diagnostics bullet; SLAP001/SLAP002 only as retired headings; culture documented with the property name; DocumentationLinkTests and ReadmeExampleTests pass; line endings and whitespace right.`],
  ['B4', 'B4b-docs-rest', `Do the rest of Phase B4 (section 18 lines ~1842-1849; 15.4, 15.7, 16.8): docs/development.md, docs/lexicon.md, docs/testing.md, docs/configuration.md, docs/releasing.md (versions and O16 wording), CHANGELOG.md, SECURITY.md, .github/ISSUE_TEMPLATE/bug.yml, Directory.Build.props VersionPrefix 1.1.0, exclusion.dic, AGENTS.md Scope bullet (all non-goals) and Author Voice bullet (S46), the README non-goals section parity, then the 15.7 pass (fix listed offenders after re-verifying them, rerun the phrase search and wider read, rerun the icon-draft check). Use /docs and /humanizer as routed. Run DocumentationLinkTests and ReadmeExampleTests; check whitespace and line endings. Tick items; push and check CI.`,
   `VersionPrefix is 1.1.0; every non-goal appears in both AGENTS.md Scope and README; 15.7 offenders fixed; version greps clean; nothing points at plans/icon-draft; link tests pass; CI status recorded.`],
  ['B5', 'B5', `Do Phase B5 entirely (section 18 lines ~1851-1879; read 6.7, 9.2, 11.5, and 15.7 as needed): run every leftover grep in step 1 and fix real hits (list allowed hits), measure the 6.7 table, rerun benchmarks and Native AOT size, measure coverage for both modules and the listed classes, final fresh pack with new <n> and validate-package.ps1, fill the draft PR description completely per step 6 (including the snapshot checklist covering every file in tests/AnglePlates.Tests/Snapshots, the before/after table, benchmarks, coverage, greps with judgments, template sections ticked only where true, and end with "🤖 Generated with [Claude Code](https://claude.com/claude-code)"), mark it ready with gh pr ready, then wait for CI and fix failures on the branch until all four legs are green (one rerun with gh run rerun --failed for infrastructure failures, then STOP). Tick checklist items honestly; leave STOP P1 (owner) unticked. Do NOT merge. Do not start B6.`,
   `All step-1 greps pass or allowed hits documented; 6.7 hard requirements hold; PR description complete and PR marked ready; CI green on all four legs or failure documented as unfixable; checklist B0-B5 ticked or noted; PR not merged.`],
]

const results = []
for (const [ph, id, task, accept] of UNITS) {
  const r = await unit(ph, id, task, accept)
  results.push({ id, ...r })
  if (!r.ok) {
    log(`HALT at ${id}: ${r.reason}`)
    return { halted: true, at: id, reason: r.reason, results }
  }
  log(`${id} verified`)
}
return { halted: false, results: results.map(r => ({ id: r.id, summary: r.impl && r.impl.summary, deviations: r.impl && r.impl.deviations })) }
