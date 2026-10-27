// Node host for the kernel WASM smoke. Reads the active historical catalog from CAUSA_DATA_DIR at run time,
// passes the three cases to the engine, and checks each state digest against the catalog manifest.
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const data = process.env.CAUSA_DATA_DIR ?? join(here, '..', '..', '..', 'causa-data')
const active = JSON.parse(readFileSync(join(data, 'catalog', 'active.json'), 'utf8'))
const catalog = JSON.parse(readFileSync(join(data, active.manifest), 'utf8'))
const args = []
for (const c of catalog.cases) {
  const dir = join(data, c.path)
  args.push(c.investigationId,
    readFileSync(join(dir, 'manifest.json'), 'utf8'),
    readFileSync(join(dir, 'events.ndjson'), 'utf8'),
    readFileSync(join(dir, 'snapshot.json'), 'utf8'))
}

const lines = []
const log = console.log
console.log = (...xs) => { const s = xs.join(' '); lines.push(s); log(s) }
const { dotnet } = await import(join(here, 'bin', 'Release', 'net10.0', 'wwwroot', '_framework', 'dotnet.js'))
const runtime = await dotnet
  .withEnvironmentVariable('MONO_INTERPRETER_OPTIONS', process.env.MONO_INTERPRETER_OPTIONS ?? '-ssa,-bblocks')
  .withApplicationArguments(...args)
  .create()
const code = await runtime.runMain()
console.log = log

let failures = code === 0 ? 0 : 1
for (const c of catalog.cases) {
  const line = lines.find(l => l.startsWith(`CASE ${c.investigationId} `))
  const ok = line && line.split(' ')[2] === 'OK' && line.split(' ')[4] === c.expectedStateSha256
  console.log(`${ok ? 'PASS' : 'FAIL'} ${c.caseKey} expected state ${c.expectedStateSha256}`)
  if (!ok) failures++
}
if (!lines.some(l => l.startsWith('FAULT OK'))) failures++
console.log(failures === 0 ? 'WASM SMOKE PASSED' : `WASM SMOKE FAILED (${failures})`)
process.exit(failures === 0 ? 0 : 1)
