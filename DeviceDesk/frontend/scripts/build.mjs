import * as esbuild from 'esbuild'
import { cpSync, existsSync, mkdirSync, rmSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const root = join(__dirname, '..')
const dist = join(root, 'dist')
const assetsDir = join(dist, 'assets')
const watch = process.argv.includes('--watch')

function copyPublic() {
  const pub = join(root, 'public')
  if (!existsSync(pub)) return
  cpSync(pub, dist, { recursive: true })
}

const buildOptions = {
  entryPoints: [join(root, 'src', 'main.jsx')],
  bundle: true,
  outfile: join(assetsDir, 'bundle.js'),
  format: 'esm',
  jsx: 'automatic',
  loader: { '.js': 'jsx', '.jsx': 'jsx' },
  sourcemap: true,
  define: {
    'process.env.NODE_ENV': JSON.stringify(watch ? 'development' : 'production'),
  },
  plugins: [
    {
      name: 'copy-public',
      setup(build) {
        build.onEnd((result) => {
          if (result.errors.length > 0) return
          copyPublic()
        })
      },
    },
  ],
}

if (existsSync(dist)) {
  rmSync(dist, { recursive: true })
}
mkdirSync(assetsDir, { recursive: true })

if (watch) {
  const ctx = await esbuild.context(buildOptions)
  await ctx.watch()
  console.log('[frontend] esbuild watch started — output in dist/')
} else {
  await esbuild.build(buildOptions)
  console.log('[frontend] esbuild build finished — output in dist/')
}
